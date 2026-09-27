using HUB.Chat.Application.Channels.Commands.ArchiveChannel;
using HUB.Chat.Application.Channels.Commands.ChangeChannelVisibility;
using HUB.Chat.Application.Channels.Commands.CreateChannel;
using HUB.Chat.Application.Channels.Commands.TransferOwnership;
using HUB.Chat.Application.Channels.Commands.UpdateChannel;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using HUB.Chat.Infrastructure.Persistence;
using HUB.TestKit.Db;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Channels;

/// <summary>
/// Covers the administrative channel commands and the role each one requires.
/// </summary>
/// <remarks>
/// Three privilege levels are in play and the handlers do not all use the same one: updating name and
/// topic needs Admin or Owner, while changing visibility and transferring ownership need Owner
/// specifically. Those differences are the substance here — a check copied from the wrong handler would
/// let any admin flip a private channel public, which is a confidentiality change, not a cosmetic one.
/// </remarks>
public sealed class ChannelAdminHandlerTests
{
    private static async Task<Channel> SeedAsync(
        ChatDbContext context, Guid owner, ChannelType type = ChannelType.Public)
    {
        var channel = Channel.Create(Guid.NewGuid(), "General", type, owner);
        context.Channels.Add(channel);
        await context.SaveChangesAsync(CancellationToken.None);
        return channel;
    }

    private static async Task<Channel> ReloadAsync(ChatDbContextLease lease, Guid channelId)
    {
        await using var verify = lease.NewContext();
        return await verify.Channels.AsNoTracking().Include(c => c.Members).SingleAsync(c => c.Id == channelId);
    }

    // ── Create ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatingAChannelMakesTheCallerItsOwner()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var creator = Guid.NewGuid();
        var result  = await new CreateChannelHandler(lease.Context).Handle(
            new CreateChannelCommand(Guid.NewGuid(), "Engineering", ChannelType.Public, "All things eng", creator),
            CancellationToken.None);

        result.Name.ShouldBe("Engineering");
        result.Slug.ShouldBe("engineering");
        result.Topic.ShouldBe("All things eng");

        var stored = await ReloadAsync(lease, result.Id);
        stored.Members.ShouldHaveSingleItem().Role.ShouldBe(ChannelMemberRole.Owner);
    }

    [Fact]
    public async Task CreatingAPrivateChannelMarksItPrivate()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var result = await new CreateChannelHandler(lease.Context).Handle(
            new CreateChannelCommand(Guid.NewGuid(), "Secret", ChannelType.Private, "", Guid.NewGuid()),
            CancellationToken.None);

        result.IsPrivate.ShouldBeTrue();
        result.Type.ShouldBe(ChannelType.Private);
    }

    [Fact]
    public async Task CreatingAChannelWithAnEmptyNameFails()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        await Should.ThrowAsync<DomainException>(() => new CreateChannelHandler(lease.Context).Handle(
            new CreateChannelCommand(Guid.NewGuid(), "   ", ChannelType.Public, "", Guid.NewGuid()),
            CancellationToken.None));
    }

    [Fact]
    public async Task TheCreateResponseOmitsTheCallersRole()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var result = await new CreateChannelHandler(lease.Context).Handle(
            new CreateChannelCommand(Guid.NewGuid(), "Role Gap", ChannelType.Public, "", Guid.NewGuid()),
            CancellationToken.None);

        // BUG-002, asserted as it behaves rather than as it should. CreateChannelHandler calls ToDto()
        // without threading myRole, so the response says IsMember: true and MyRole: null even though the
        // caller is the Owner. UpdateChannel and both OpenLinkedThread branches have the same shape.
        // See .claude/self-test/bugs.md; when somebody fixes it, this test fails and points there.
        result.IsMember.ShouldBeTrue();
        result.MyRole.ShouldBeNull("known gap — see BUG-002");
    }

    // ── Update name and topic: Admin or Owner ───────────────────────────────

    [Fact]
    public async Task AnOwnerCanRenameAChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, owner);

        var result = await new UpdateChannelHandler(lease.Context).Handle(
            new UpdateChannelCommand(channel.Id, "Renamed", "New topic", owner), CancellationToken.None);

        result.Name.ShouldBe("Renamed");
        result.Slug.ShouldBe("renamed");
        result.Topic.ShouldBe("New topic");
    }

    [Fact]
    public async Task AnAdminCanRenameAChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedAsync(lease.Context, Guid.NewGuid());

        var admin = Guid.NewGuid();
        channel.AddMember(admin, ChannelMemberRole.Admin);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new UpdateChannelHandler(lease.Context).Handle(
            new UpdateChannelCommand(channel.Id, "By Admin", null, admin), CancellationToken.None);

        result.Name.ShouldBe("By Admin");
    }

    [Fact]
    public async Task AnOrdinaryMemberCannotRenameAChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedAsync(lease.Context, Guid.NewGuid());

        var member = Guid.NewGuid();
        channel.AddMember(member);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        await Should.ThrowAsync<ForbiddenException>(() => new UpdateChannelHandler(lease.Context).Handle(
            new UpdateChannelCommand(channel.Id, "Nope", null, member), CancellationToken.None));

        (await ReloadAsync(lease, channel.Id)).Name.ShouldBe("General");
    }

    [Fact]
    public async Task ANonMemberCannotRenameAChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedAsync(lease.Context, Guid.NewGuid());

        await Should.ThrowAsync<ForbiddenException>(() => new UpdateChannelHandler(lease.Context).Handle(
            new UpdateChannelCommand(channel.Id, "Nope", null, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task UpdatingAMissingChannelIsNotFound()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        await Should.ThrowAsync<NotFoundException>(() => new UpdateChannelHandler(lease.Context).Handle(
            new UpdateChannelCommand(Guid.NewGuid(), "X", null, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task UpdatingOnlyTheTopicKeepsTheName()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, owner);

        var result = await new UpdateChannelHandler(lease.Context).Handle(
            new UpdateChannelCommand(channel.Id, null, "Only the topic", owner), CancellationToken.None);

        // A partial update is the normal case from a settings form that only changed one field.
        result.Name.ShouldBe("General");
        result.Topic.ShouldBe("Only the topic");
    }

    // ── Change visibility: Owner only ───────────────────────────────────────

    [Fact]
    public async Task AnOwnerCanMakeAChannelPrivate()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, owner);

        var result = await new ChangeChannelVisibilityHandler(lease.Context).Handle(
            new ChangeChannelVisibilityCommand(channel.Id, true, owner), CancellationToken.None);

        result.IsPrivate.ShouldBeTrue();
        result.Type.ShouldBe(ChannelType.Private);
        result.MyRole.ShouldBe(ChannelMemberRole.Owner);
    }

    [Fact]
    public async Task AnAdminCannotChangeVisibility()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedAsync(lease.Context, Guid.NewGuid());

        var admin = Guid.NewGuid();
        channel.AddMember(admin, ChannelMemberRole.Admin);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        // This is the difference that matters between the two checks: an Admin may rename a channel but
        // must not be able to make a private one public. Reusing UpdateChannel's check here would be a
        // confidentiality bug, not a permissions nitpick.
        await Should.ThrowAsync<ForbiddenException>(() =>
            new ChangeChannelVisibilityHandler(lease.Context).Handle(
                new ChangeChannelVisibilityCommand(channel.Id, false, admin), CancellationToken.None));
    }

    [Fact]
    public async Task ANonMemberCannotChangeVisibility()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedAsync(lease.Context, Guid.NewGuid());

        await Should.ThrowAsync<ForbiddenException>(() =>
            new ChangeChannelVisibilityHandler(lease.Context).Handle(
                new ChangeChannelVisibilityCommand(channel.Id, true, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task ADirectMessageCannotChangeVisibility()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, owner, ChannelType.Dm);

        await Should.ThrowAsync<DomainException>(() =>
            new ChangeChannelVisibilityHandler(lease.Context).Handle(
                new ChangeChannelVisibilityCommand(channel.Id, false, owner), CancellationToken.None));
    }

    // ── Transfer ownership: Owner only ──────────────────────────────────────

    [Fact]
    public async Task AnOwnerCanHandOverToAMember()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, owner);

        var successor = Guid.NewGuid();
        channel.AddMember(successor);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        await new TransferOwnershipHandler(lease.Context).Handle(
            new TransferOwnershipCommand(channel.Id, successor, owner), CancellationToken.None);

        var stored = await ReloadAsync(lease, channel.Id);
        stored.Members.Single(m => m.UserId == successor).Role.ShouldBe(ChannelMemberRole.Owner);
        stored.Members.Single(m => m.UserId == owner).Role.ShouldBe(ChannelMemberRole.Admin);
    }

    [Fact]
    public async Task TheTransferResponseReportsTheCallersNewRole()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, owner);

        var successor = Guid.NewGuid();
        channel.AddMember(successor);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new TransferOwnershipHandler(lease.Context).Handle(
            new TransferOwnershipCommand(channel.Id, successor, owner), CancellationToken.None);

        // The handler resolves `caller` before the transfer and reads `caller.Role` after it. Because that
        // is a reference to the tracked membership rather than a copy, the response reports Admin — the
        // role the caller actually holds now. Worth pinning: the client hides owner-only controls straight
        // away instead of showing them until the next refetch, and a refactor that snapshotted the role
        // early would silently regress that.
        result.MyRole.ShouldBe(ChannelMemberRole.Admin);

        var stored = await ReloadAsync(lease, channel.Id);
        stored.Members.Single(m => m.UserId == owner).Role.ShouldBe(ChannelMemberRole.Admin,
            "the response agrees with what was stored");
    }

    [Fact]
    public async Task AnAdminCannotTransferOwnership()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedAsync(lease.Context, Guid.NewGuid());

        var admin  = Guid.NewGuid();
        var target = Guid.NewGuid();
        channel.AddMember(admin, ChannelMemberRole.Admin);
        channel.AddMember(target);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        // Otherwise an admin could appoint themselves, or anyone else, owner.
        await Should.ThrowAsync<ForbiddenException>(() => new TransferOwnershipHandler(lease.Context).Handle(
            new TransferOwnershipCommand(channel.Id, target, admin), CancellationToken.None));
    }

    [Fact]
    public async Task OwnershipCannotBeTransferredToANonMember()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, owner);

        await Should.ThrowAsync<DomainException>(() => new TransferOwnershipHandler(lease.Context).Handle(
            new TransferOwnershipCommand(channel.Id, Guid.NewGuid(), owner), CancellationToken.None));
    }

    [Fact]
    public async Task TransferringToYourselfFails()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, owner);

        await Should.ThrowAsync<DomainException>(() => new TransferOwnershipHandler(lease.Context).Handle(
            new TransferOwnershipCommand(channel.Id, owner, owner), CancellationToken.None));
    }

    // ── Archive ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ArchivingClosesTheChannelToNewMessages()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedAsync(lease.Context, Guid.NewGuid());

        await new ArchiveChannelHandler(lease.Context)
            .Handle(new ArchiveChannelCommand(channel.Id), CancellationToken.None);

        (await ReloadAsync(lease, channel.Id)).IsArchived.ShouldBeTrue();
    }

    [Fact]
    public async Task ArchivingTwiceIsIdempotent()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedAsync(lease.Context, Guid.NewGuid());

        var handler = new ArchiveChannelHandler(lease.Context);
        var command = new ArchiveChannelCommand(channel.Id);

        await handler.Handle(command, CancellationToken.None);
        await Should.NotThrowAsync(() => handler.Handle(command, CancellationToken.None));

        // DASHBOARD calls this when a sprint closes and has no memory of whether it called before.
        (await ReloadAsync(lease, channel.Id)).IsArchived.ShouldBeTrue();
    }

    [Fact]
    public async Task ArchivingAMissingChannelIsNotFound()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        await Should.ThrowAsync<NotFoundException>(() => new ArchiveChannelHandler(lease.Context)
            .Handle(new ArchiveChannelCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
