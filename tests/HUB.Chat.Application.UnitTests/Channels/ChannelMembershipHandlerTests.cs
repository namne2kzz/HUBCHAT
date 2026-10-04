using HUB.Chat.Application.Channels.Commands.AddChannelMember;
using HUB.Chat.Application.Channels.Commands.JoinChannel;
using HUB.Chat.Application.Channels.Commands.LeaveChannel;
using HUB.Chat.Application.Channels.Commands.MarkRead;
using HUB.Chat.Application.Channels.Commands.RemoveChannelMember;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using HUB.Chat.Infrastructure.Persistence;
using HUB.Shared.Contracts.Events;
using HUB.TestKit.Db;
using HUB.TestKit.Fakes;
using HUB.Chat.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Channels;

/// <summary>
/// Covers the handlers that add and remove channel members: joining, leaving, and the two
/// machine-to-machine variants DASHBOARD calls during sprint member sync.
/// </summary>
/// <remarks>
/// Idempotency is the theme. <c>AddChannelMember</c> and <c>RemoveChannelMember</c> are reached from
/// DASHBOARD's sprint sync, which replays the whole member list rather than diffing it — so a second
/// call with the same user is the ordinary case, not an error. The domain throws on a duplicate
/// <c>AddMember</c>, and each handler guards with <c>HasMember</c> before calling it; these tests pin
/// that guard, because losing it turns a routine sync into a 409.
/// </remarks>
public sealed class ChannelMembershipHandlerTests
{
    private static async Task<Channel> SeedChannelAsync(
        ChatDbContext context, Guid owner, ChannelType type = ChannelType.Public)
    {
        var channel = Channel.Create(Guid.NewGuid(), "General", type, owner);
        context.Channels.Add(channel);
        await context.SaveChangesAsync(CancellationToken.None);
        return channel;
    }

    /// <summary>Counts a channel's members from a fresh context, so the assertion reflects the database.</summary>
    private static async Task<int> MemberCountAsync(ChatDbContextLease lease, Guid channelId)
    {
        await using var verify = lease.NewContext();
        return await verify.Channels.AsNoTracking()
            .Where(c => c.Id == channelId)
            .SelectMany(c => c.Members)
            .CountAsync();
    }

    // ── Join ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task JoiningAMissingChannelIsNotFound()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        await Should.ThrowAsync<NotFoundException>(() => new JoinChannelHandler(lease.Context)
            .Handle(new JoinChannelCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task JoiningAddsTheCallerAsAnOrdinaryMember()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedChannelAsync(lease.Context, Guid.NewGuid());

        var joiner = Guid.NewGuid();
        await new JoinChannelHandler(lease.Context)
            .Handle(new JoinChannelCommand(channel.Id, joiner), CancellationToken.None);

        await using var verify = lease.NewContext();
        var stored = await verify.Channels.AsNoTracking()
            .Include(c => c.Members).SingleAsync(c => c.Id == channel.Id);

        // Member, not Admin: self-service joining must not confer any administrative rights.
        stored.Members.Single(m => m.UserId == joiner).Role.ShouldBe(ChannelMemberRole.Member);
    }

    [Fact]
    public async Task JoiningTwiceIsIdempotent()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedChannelAsync(lease.Context, Guid.NewGuid());

        var joiner  = Guid.NewGuid();
        var handler = new JoinChannelHandler(lease.Context);
        var command = new JoinChannelCommand(channel.Id, joiner);

        await handler.Handle(command, CancellationToken.None);
        await handler.Handle(command, CancellationToken.None);

        // A double-clicked Join button must not surface the domain's duplicate-member error.
        (await MemberCountAsync(lease, channel.Id)).ShouldBe(2);
    }

    // ── Leave ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task LeavingRemovesTheCaller()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = await SeedChannelAsync(lease.Context, owner);

        var member = Guid.NewGuid();
        channel.AddMember(member);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        await new LeaveChannelHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new LeaveChannelCommand(channel.Id, member), CancellationToken.None);

        (await MemberCountAsync(lease, channel.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task TheOnlyOwnerCannotLeave()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = await SeedChannelAsync(lease.Context, owner);
        channel.AddMember(Guid.NewGuid());
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        // The domain rule surfaces through the handler unchanged: letting the last owner out would leave
        // the channel with members nobody can administer.
        await Should.ThrowAsync<DomainException>(() => new LeaveChannelHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new LeaveChannelCommand(channel.Id, owner), CancellationToken.None));

        (await MemberCountAsync(lease, channel.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task LeavingAChannelYouAreNotInFails()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedChannelAsync(lease.Context, Guid.NewGuid());

        await Should.ThrowAsync<DomainException>(() => new LeaveChannelHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new LeaveChannelCommand(channel.Id, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task LeavingAMissingChannelIsNotFound()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        await Should.ThrowAsync<NotFoundException>(() => new LeaveChannelHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new LeaveChannelCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }

    // ── Add member (internal, from DASHBOARD sprint sync) ───────────────────

    [Fact]
    public async Task AddingAMemberReturnsTheirMembership()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedChannelAsync(lease.Context, Guid.NewGuid());

        var userId = Guid.NewGuid();
        var result = await new AddChannelMemberHandler(lease.Context, new FakeWorkspacePermissions())
            .Handle(new AddChannelMemberCommand(channel.Id, userId), CancellationToken.None);

        result.UserId.ShouldBe(userId);
        result.Role.ShouldBe(ChannelMemberRole.Member);
    }

    [Fact]
    public async Task AddingTheSameMemberTwiceIsIdempotentAndStillReturnsThem()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedChannelAsync(lease.Context, Guid.NewGuid());

        var userId  = Guid.NewGuid();
        var handler = new AddChannelMemberHandler(lease.Context, new FakeWorkspacePermissions());
        var command = new AddChannelMemberCommand(channel.Id, userId);

        await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        // Sprint sync replays the full member list on every change, so this path runs constantly. It has
        // to return the existing membership rather than throwing on the duplicate.
        second.UserId.ShouldBe(userId);
        (await MemberCountAsync(lease, channel.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task AddingAMemberToAMissingChannelIsNotFound()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        await Should.ThrowAsync<NotFoundException>(() => new AddChannelMemberHandler(lease.Context, new FakeWorkspacePermissions())
            .Handle(new AddChannelMemberCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task AddingAnExistingOwnerDoesNotDemoteThem()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = await SeedChannelAsync(lease.Context, owner);

        var result = await new AddChannelMemberHandler(lease.Context, new FakeWorkspacePermissions())
            .Handle(new AddChannelMemberCommand(channel.Id, owner), CancellationToken.None);

        // Sprint sync includes the creator in the member list it replays. Re-adding them as a plain
        // Member would strip the channel of its owner on the next sync.
        result.Role.ShouldBe(ChannelMemberRole.Owner);
    }

    // ── Remove member (internal) ────────────────────────────────────────────

    [Fact]
    public async Task RemovingAMemberTakesThemOut()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedChannelAsync(lease.Context, Guid.NewGuid());

        var userId = Guid.NewGuid();
        channel.AddMember(userId);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        await new RemoveChannelMemberHandler(lease.Context, new FakeWorkspacePermissions(), new RecordingEventPublisher())
            .Handle(new RemoveChannelMemberCommand(channel.Id, userId), CancellationToken.None);

        (await MemberCountAsync(lease, channel.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task RemovingSomebodyWhoIsNotAMemberSucceedsSilently()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedChannelAsync(lease.Context, Guid.NewGuid());

        // Documented as silently succeeding: DASHBOARD dropping a capacity member should not fail
        // because HUB never had them. The domain would throw, so the handler's HasMember guard is what
        // makes this work.
        await Should.NotThrowAsync(() => new RemoveChannelMemberHandler(lease.Context, new FakeWorkspacePermissions(), new RecordingEventPublisher())
            .Handle(new RemoveChannelMemberCommand(channel.Id, Guid.NewGuid()), CancellationToken.None));

        (await MemberCountAsync(lease, channel.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task RemovingTheSoleOwnerStillFails()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = await SeedChannelAsync(lease.Context, owner);
        channel.AddMember(Guid.NewGuid());
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        // The HasMember guard makes removal idempotent, but it must not also swallow the last-owner
        // invariant — an internal caller should not be able to strand a channel either.
        await Should.ThrowAsync<DomainException>(() => new RemoveChannelMemberHandler(lease.Context, new FakeWorkspacePermissions(), new RecordingEventPublisher())
            .Handle(new RemoveChannelMemberCommand(channel.Id, owner), CancellationToken.None));
    }

    // ── Add / remove member (public API — permission-checked) ───────────────

    [Fact]
    public async Task APlainUserCannotAddThemselvesToSomeoneElsesPrivateChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel  = await SeedChannelAsync(lease.Context, Guid.NewGuid(), ChannelType.Private);
        var stranger = Guid.NewGuid();

        // The exploit this closes: the public endpoint used to add whoever asked, so knowing a private
        // channel's id was enough to join it and read everything.
        await Should.ThrowAsync<ForbiddenException>(() =>
            new AddChannelMemberHandler(lease.Context, new FakeWorkspacePermissions())
                .Handle(new AddChannelMemberCommand(channel.Id, stranger, stranger), CancellationToken.None));

        (await MemberCountAsync(lease, channel.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task APlainMemberCannotRemoveOthers()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var member  = Guid.NewGuid();
        var channel = await SeedChannelAsync(lease.Context, owner);
        channel.AddMember(member);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        await Should.ThrowAsync<ForbiddenException>(() =>
            new RemoveChannelMemberHandler(lease.Context, new FakeWorkspacePermissions(), new RecordingEventPublisher())
                .Handle(new RemoveChannelMemberCommand(channel.Id, owner, member), CancellationToken.None));

        (await MemberCountAsync(lease, channel.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task ARefusedRemovalOfANonMemberIsStillForbidden()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedChannelAsync(lease.Context, Guid.NewGuid(), ChannelType.Private);

        // The permission check runs before the "not a member → no-op" shortcut; otherwise a stranger
        // could tell members from non-members by which ids return 204 vs 403.
        await Should.ThrowAsync<ForbiddenException>(() =>
            new RemoveChannelMemberHandler(lease.Context, new FakeWorkspacePermissions(), new RecordingEventPublisher())
                .Handle(new RemoveChannelMemberCommand(channel.Id, Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }

    [Theory]
    [InlineData(ChannelMemberRole.Owner)]
    [InlineData(ChannelMemberRole.Admin)]
    public async Task OwnersAndAdminsManageMembersWithoutAskingTheWorkspace(ChannelMemberRole role)
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = await SeedChannelAsync(lease.Context, owner);
        var manager = owner;
        if (role == ChannelMemberRole.Admin)
        {
            manager = Guid.NewGuid();
            channel.AddMember(manager, ChannelMemberRole.Admin);
            await lease.Context.SaveChangesAsync(CancellationToken.None);
        }

        var permissions = new FakeWorkspacePermissions();
        var newcomer    = Guid.NewGuid();

        await new AddChannelMemberHandler(lease.Context, permissions)
            .Handle(new AddChannelMemberCommand(channel.Id, newcomer, manager), CancellationToken.None);
        await new RemoveChannelMemberHandler(lease.Context, permissions, new RecordingEventPublisher())
            .Handle(new RemoveChannelMemberCommand(channel.Id, newcomer, manager), CancellationToken.None);

        // The channel role is enough, so the dashboard-gateway hop is never paid.
        permissions.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ManageChannelsInTheChannelsWorkspaceIsEnoughWithoutAChannelRole()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel     = await SeedChannelAsync(lease.Context, Guid.NewGuid());
        var workspaceMgr = Guid.NewGuid(); // not a channel member at all
        var permissions = new FakeWorkspacePermissions()
            .Grant(channel.WorkspaceId, WorkspacePermissionNames.ManageChannels);

        var added = await new AddChannelMemberHandler(lease.Context, permissions)
            .Handle(new AddChannelMemberCommand(channel.Id, Guid.NewGuid(), workspaceMgr), CancellationToken.None);

        added.Role.ShouldBe(ChannelMemberRole.Member);
    }

    [Fact]
    public async Task ManageChannelsInAnotherWorkspaceDoesNotCount()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel     = await SeedChannelAsync(lease.Context, Guid.NewGuid());
        var otherWsMgr  = Guid.NewGuid();
        var permissions = new FakeWorkspacePermissions()
            .Grant(Guid.NewGuid(), WorkspacePermissionNames.ManageChannels);

        // Workspace-scoped: managing channels in project A says nothing about project B's channels.
        await Should.ThrowAsync<ForbiddenException>(() =>
            new AddChannelMemberHandler(lease.Context, permissions)
                .Handle(new AddChannelMemberCommand(channel.Id, Guid.NewGuid(), otherWsMgr), CancellationToken.None));
    }

    // ── Realtime revoke (ChannelMemberRemoved via outbox) ───────────────────

    [Fact]
    public async Task RemovingAMemberAnnouncesTheRemoval()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var member  = Guid.NewGuid();
        var channel = await SeedChannelAsync(lease.Context, owner, ChannelType.Private);
        channel.AddMember(member);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var events = new RecordingEventPublisher();
        lease.Context.SavedChanges += (_, _) => events.MarkSaved();
        await new RemoveChannelMemberHandler(lease.Context, new FakeWorkspacePermissions(), events)
            .Handle(new RemoveChannelMemberCommand(channel.Id, member, owner), CancellationToken.None);

        // Realtime revokes the live subscription only on this event; without it the kicked user keeps
        // receiving the private channel until they reconnect.
        var e = events.Published.OfType<ChannelMemberRemoved>().ShouldHaveSingleItem();
        e.ChannelId.ShouldBe(channel.Id);
        e.UserId.ShouldBe(member);
        events.PublishedBeforeSave.ShouldBeTrue("the event must ride the outbox transaction with the removal");
    }

    [Fact]
    public async Task RemovingSomebodyWhoWasNotAMemberAnnouncesNothing()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedChannelAsync(lease.Context, Guid.NewGuid());
        var events  = new RecordingEventPublisher();

        await new RemoveChannelMemberHandler(lease.Context, new FakeWorkspacePermissions(), events)
            .Handle(new RemoveChannelMemberCommand(channel.Id, Guid.NewGuid()), CancellationToken.None);

        events.Published.ShouldBeEmpty();
    }

    [Fact]
    public async Task LeavingAnnouncesTheRemovalToo()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var member  = Guid.NewGuid();
        var channel = await SeedChannelAsync(lease.Context, Guid.NewGuid());
        channel.AddMember(member);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var events = new RecordingEventPublisher();
        await new LeaveChannelHandler(lease.Context, events)
            .Handle(new LeaveChannelCommand(channel.Id, member), CancellationToken.None);

        // The tab that clicked leave unsubscribes itself; the user's other tabs/devices rely on this event.
        events.Published.OfType<ChannelMemberRemoved>().ShouldHaveSingleItem().UserId.ShouldBe(member);
    }

    // ── Mark read ───────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkingReadStampsTheCallersMembership()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = await SeedChannelAsync(lease.Context, owner);

        var readAt = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
        await new MarkReadHandler(lease.Context)
            .Handle(new MarkReadCommand(channel.Id, owner, readAt), CancellationToken.None);

        await using var verify = lease.NewContext();
        var stored = await verify.Channels.AsNoTracking()
            .Include(c => c.Members).SingleAsync(c => c.Id == channel.Id);

        stored.Members.Single(m => m.UserId == owner).LastReadAt.ShouldBe(readAt);
    }

    [Fact]
    public async Task MarkingReadWithoutATimeUsesNow()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = await SeedChannelAsync(lease.Context, owner);

        var before = DateTime.UtcNow;
        await new MarkReadHandler(lease.Context)
            .Handle(new MarkReadCommand(channel.Id, owner, null), CancellationToken.None);

        await using var verify = lease.NewContext();
        var stored = await verify.Channels.AsNoTracking()
            .Include(c => c.Members).SingleAsync(c => c.Id == channel.Id);

        stored.Members.Single(m => m.UserId == owner).LastReadAt!.Value
            .ShouldBeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public async Task MarkingReadNeverMovesTheMarkerBackwards()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = await SeedChannelAsync(lease.Context, owner);

        var handler = new MarkReadHandler(lease.Context);
        var later   = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

        await handler.Handle(new MarkReadCommand(channel.Id, owner, later), CancellationToken.None);
        await handler.Handle(new MarkReadCommand(channel.Id, owner, later.AddMinutes(-30)), CancellationToken.None);

        // Two tabs, or a request that arrived late. Accepting the older stamp would make already-read
        // messages unread again.
        await using var verify = lease.NewContext();
        var stored = await verify.Channels.AsNoTracking()
            .Include(c => c.Members).SingleAsync(c => c.Id == channel.Id);

        stored.Members.Single(m => m.UserId == owner).LastReadAt.ShouldBe(later);
    }

    [Fact]
    public async Task ANonMemberCannotMarkAChannelRead()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedChannelAsync(lease.Context, Guid.NewGuid());

        await Should.ThrowAsync<ForbiddenException>(() => new MarkReadHandler(lease.Context)
            .Handle(new MarkReadCommand(channel.Id, Guid.NewGuid(), null), CancellationToken.None));
    }

    [Fact]
    public async Task MarkingAMissingChannelReadIsNotFound()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        await Should.ThrowAsync<NotFoundException>(() => new MarkReadHandler(lease.Context)
            .Handle(new MarkReadCommand(Guid.NewGuid(), Guid.NewGuid(), null), CancellationToken.None));
    }
}
