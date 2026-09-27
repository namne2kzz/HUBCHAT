using HUB.Chat.Application.Channels.Queries.GetChannel;
using HUB.Chat.Application.Channels.Queries.ListChannelMembers;
using HUB.Chat.Application.Channels.Queries.ListChannels;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using HUB.Chat.Infrastructure.Persistence;
using HUB.TestKit.Db;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Channels;

/// <summary>
/// Covers the channel read models: which channels a person is shown, which they may open, and who is
/// listed as a member.
/// </summary>
/// <remarks>
/// These queries decide what a person can see, so the filters are the security boundary. A private
/// channel appearing in a list, or its member list being readable by an outsider, leaks who is talking
/// to whom even when the messages stay hidden.
/// </remarks>
public sealed class ChannelQueryHandlerTests
{
    private static Channel Seed(ChatDbContext context, Guid workspace, Guid owner,
        string name = "General", ChannelType type = ChannelType.Public)
    {
        var channel = Channel.Create(workspace, name, type, owner);
        context.Channels.Add(channel);
        return channel;
    }

    // ── List channels ───────────────────────────────────────────────────────

    [Fact]
    public async Task PublicChannelsInTheWorkspaceAreListedEvenToNonMembers()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var workspace = Guid.NewGuid();
        Seed(lease.Context, workspace, Guid.NewGuid(), "Open");
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new ListChannelsHandler(lease.Context)
            .Handle(new ListChannelsQuery(workspace, Guid.NewGuid()), CancellationToken.None);

        // Discoverability is the point of a public channel: you have to see it before you can join it.
        var channel = result.ShouldHaveSingleItem();
        channel.Name.ShouldBe("Open");
        channel.IsMember.ShouldBeFalse();
    }

    [Fact]
    public async Task PrivateChannelsAreHiddenFromNonMembers()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var workspace = Guid.NewGuid();
        Seed(lease.Context, workspace, Guid.NewGuid(), "Secret", ChannelType.Private);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new ListChannelsHandler(lease.Context)
            .Handle(new ListChannelsQuery(workspace, Guid.NewGuid()), CancellationToken.None);

        // Not just unopenable — invisible. The name of a private channel is itself information.
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task PrivateChannelsAreListedToTheirMembers()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var workspace = Guid.NewGuid();
        var member    = Guid.NewGuid();
        Seed(lease.Context, workspace, member, "Secret", ChannelType.Private);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new ListChannelsHandler(lease.Context)
            .Handle(new ListChannelsQuery(workspace, member), CancellationToken.None);

        result.ShouldHaveSingleItem().IsMember.ShouldBeTrue();
    }

    [Fact]
    public async Task ChannelsFromAnotherWorkspaceAreNotListed()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var mine = Guid.NewGuid();
        Seed(lease.Context, mine, Guid.NewGuid(), "Mine");
        Seed(lease.Context, Guid.NewGuid(), Guid.NewGuid(), "Theirs");
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new ListChannelsHandler(lease.Context)
            .Handle(new ListChannelsQuery(mine, Guid.NewGuid()), CancellationToken.None);

        // A workspace is a DASHBOARD repository, so this filter is the tenant boundary.
        result.ShouldHaveSingleItem().Name.ShouldBe("Mine");
    }

    [Fact]
    public async Task DirectMessagesAreListedRegardlessOfWorkspace()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var me = Guid.NewGuid();

        Seed(lease.Context, Guid.NewGuid(), me, "dm-elsewhere", ChannelType.Dm);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        // DMs are person-to-person rather than workspace-scoped, so they follow the user across
        // repositories. Filtering them by workspace would make a conversation vanish when the person
        // switched repos.
        var result = await new ListChannelsHandler(lease.Context)
            .Handle(new ListChannelsQuery(Guid.NewGuid(), me), CancellationToken.None);

        result.ShouldHaveSingleItem().Type.ShouldBe(ChannelType.Dm);
    }

    [Fact]
    public async Task SomebodyElsesDirectMessagesAreNotListed()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        Seed(lease.Context, Guid.NewGuid(), Guid.NewGuid(), "dm-theirs", ChannelType.Dm);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new ListChannelsHandler(lease.Context)
            .Handle(new ListChannelsQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ADirectMessageReportsTheOtherParticipant()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var me    = Guid.NewGuid();
        var them  = Guid.NewGuid();
        var dm    = Seed(lease.Context, Guid.NewGuid(), me, "dm", ChannelType.Dm);
        dm.AddMember(them);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new ListChannelsHandler(lease.Context)
            .Handle(new ListChannelsQuery(Guid.NewGuid(), me), CancellationToken.None);

        // The client renders a DM under the other person's name, which it resolves from this id — the
        // stored channel name is a machine-generated "dm-{a}-{b}".
        result.ShouldHaveSingleItem().OtherUserId.ShouldBe(them);
    }

    [Fact]
    public async Task ChannelsAreListedByName()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var workspace = Guid.NewGuid();
        Seed(lease.Context, workspace, Guid.NewGuid(), "Zebra");
        Seed(lease.Context, workspace, Guid.NewGuid(), "Alpha");
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new ListChannelsHandler(lease.Context)
            .Handle(new ListChannelsQuery(workspace, Guid.NewGuid()), CancellationToken.None);

        result.Select(c => c.Name).ShouldBe(["Alpha", "Zebra"]);
    }

    [Fact]
    public async Task TheListOmitsTheCallersRole()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var workspace = Guid.NewGuid();
        var owner     = Guid.NewGuid();
        Seed(lease.Context, workspace, owner);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new ListChannelsHandler(lease.Context)
            .Handle(new ListChannelsQuery(workspace, owner), CancellationToken.None);

        // Another BUG-002 site: the projection sets IsMember and OtherUserId but never MyRole, so the
        // sidebar cannot tell an owned channel from one merely joined without fetching each in turn.
        result.ShouldHaveSingleItem().MyRole.ShouldBeNull("known gap — see BUG-002");
    }

    // ── Get one channel ─────────────────────────────────────────────────────

    [Fact]
    public async Task GettingAMissingChannelIsNotFound()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        await Should.ThrowAsync<NotFoundException>(() => new GetChannelHandler(lease.Context)
            .Handle(new GetChannelQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task APublicChannelIsReadableByAnyone()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = Seed(lease.Context, Guid.NewGuid(), Guid.NewGuid());
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new GetChannelHandler(lease.Context)
            .Handle(new GetChannelQuery(channel.Id, Guid.NewGuid()), CancellationToken.None);

        result.Id.ShouldBe(channel.Id);
        result.IsMember.ShouldBeFalse();
        result.MyRole.ShouldBeNull();
    }

    [Fact]
    public async Task APrivateChannelIsForbiddenToOutsiders()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = Seed(lease.Context, Guid.NewGuid(), Guid.NewGuid(), "Secret", ChannelType.Private);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        await Should.ThrowAsync<ForbiddenException>(() => new GetChannelHandler(lease.Context)
            .Handle(new GetChannelQuery(channel.Id, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetReportsTheCallersRole()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = Seed(lease.Context, Guid.NewGuid(), owner);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new GetChannelHandler(lease.Context)
            .Handle(new GetChannelQuery(channel.Id, owner), CancellationToken.None);

        // This handler does project MyRole, which is what makes its absence elsewhere inconsistent
        // rather than merely unimplemented.
        result.MyRole.ShouldBe(ChannelMemberRole.Owner);
        result.IsMember.ShouldBeTrue();
    }

    [Fact]
    public async Task TheMemberCountReflectsEverybodyInTheChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = Seed(lease.Context, Guid.NewGuid(), owner);
        channel.AddMember(Guid.NewGuid());
        channel.AddMember(Guid.NewGuid());
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new GetChannelHandler(lease.Context)
            .Handle(new GetChannelQuery(channel.Id, owner), CancellationToken.None);

        result.MemberCount.ShouldBe(3);
    }

    // ── List members ────────────────────────────────────────────────────────

    [Fact]
    public async Task MembersOfAPublicChannelAreListedToAnyone()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = Seed(lease.Context, Guid.NewGuid(), owner);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new ListChannelMembersHandler(lease.Context)
            .Handle(new ListChannelMembersQuery(channel.Id, Guid.NewGuid()), CancellationToken.None);

        result.ShouldHaveSingleItem().UserId.ShouldBe(owner);
    }

    [Fact]
    public async Task MembersOfAPrivateChannelAreHiddenFromOutsiders()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = Seed(lease.Context, Guid.NewGuid(), Guid.NewGuid(), "Secret", ChannelType.Private);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        // Who is in a private channel is itself sensitive, separately from what they said.
        await Should.ThrowAsync<ForbiddenException>(() => new ListChannelMembersHandler(lease.Context)
            .Handle(new ListChannelMembersQuery(channel.Id, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task MembersOfAPrivateChannelAreListedToItsMembers()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = Seed(lease.Context, Guid.NewGuid(), owner, "Secret", ChannelType.Private);
        channel.AddMember(Guid.NewGuid());
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new ListChannelMembersHandler(lease.Context)
            .Handle(new ListChannelMembersQuery(channel.Id, owner), CancellationToken.None);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MembersAreListedInJoinOrder()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = Seed(lease.Context, Guid.NewGuid(), owner);
        var second  = Guid.NewGuid();
        channel.AddMember(second);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new ListChannelMembersHandler(lease.Context)
            .Handle(new ListChannelMembersQuery(channel.Id, owner), CancellationToken.None);

        result[0].UserId.ShouldBe(owner, "the creator joined first");
        result[1].UserId.ShouldBe(second);
    }

    [Fact]
    public async Task ListingMembersOfAMissingChannelIsNotFound()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        await Should.ThrowAsync<NotFoundException>(() => new ListChannelMembersHandler(lease.Context)
            .Handle(new ListChannelMembersQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task AMemberEntryCarriesItsRole()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner   = Guid.NewGuid();
        var channel = Seed(lease.Context, Guid.NewGuid(), owner);
        var admin   = Guid.NewGuid();
        channel.AddMember(admin, ChannelMemberRole.Admin);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var result = await new ListChannelMembersHandler(lease.Context)
            .Handle(new ListChannelMembersQuery(channel.Id, owner), CancellationToken.None);

        result.Single(m => m.UserId == admin).Role.ShouldBe(ChannelMemberRole.Admin);
        result.Single(m => m.UserId == owner).Role.ShouldBe(ChannelMemberRole.Owner);
    }
}
