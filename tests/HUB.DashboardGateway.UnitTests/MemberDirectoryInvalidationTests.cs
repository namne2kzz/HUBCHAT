using HUB.DashboardGateway.Consumers;
using HUB.DashboardGateway.Dashboard;
using HUB.DashboardGateway.Directory;
using HUB.TestKit.Fakes;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.IntegrationEvents;
using Shouldly;
using Xunit;

namespace HUB.DashboardGateway.UnitTests;

/// <summary>
/// Covers the event-driven half of the directory cache.
///
/// The TTLs alone were the bug these tests guard: a member removed in DASHBOARD kept passing HUB's
/// membership check for up to three minutes, and a member added stayed invisible for up to five. What
/// matters is not that eviction happens at all, but that it drops exactly the two entries the change
/// invalidated — evicting too little leaves the stale read, evicting too much throws away profile and
/// settings entries that a membership change never touched.
/// </summary>
public sealed class MemberDirectoryInvalidationTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static UserProfile Profile(Guid id) => new(id, "Nam", "nam@x.com", "bg-sky-600", false, false);

    private static UserMemberships Memberships(Guid id) =>
        new(id, false, Guid.NewGuid(), "acme", "Acme", []);

    [Fact]
    public async Task InvalidateMembership_ForcesTheNextMemberListReadToPullAgain()
    {
        var repoId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var dashboard = Substitute.For<IDashboardClient>();
        dashboard.GetRepositoryMembersAsync(repoId, Arg.Any<CancellationToken>())
                 .Returns([Profile(userId)]);

        var service = new DirectoryService(dashboard, new FakeDistributedCache());

        await service.GetWorkspaceMembersAsync(repoId, Ct);
        await service.GetWorkspaceMembersAsync(repoId, Ct);
        await dashboard.Received(1).GetRepositoryMembersAsync(repoId, Arg.Any<CancellationToken>());

        await service.InvalidateMembershipAsync(repoId, userId, Ct);
        await service.GetWorkspaceMembersAsync(repoId, Ct);

        await dashboard.Received(2).GetRepositoryMembersAsync(repoId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateMembership_ForcesTheNextMembershipReadToPullAgain()
    {
        var repoId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var dashboard = Substitute.For<IDashboardClient>();
        dashboard.GetMembershipsAsync(userId, Arg.Any<CancellationToken>()).Returns(Memberships(userId));

        var service = new DirectoryService(dashboard, new FakeDistributedCache());

        await service.GetMembershipsAsync(userId, Ct);
        await service.InvalidateMembershipAsync(repoId, userId, Ct);
        await service.GetMembershipsAsync(userId, Ct);

        // The access-revocation case: without this pull the user keeps the permissions they just lost.
        await dashboard.Received(2).GetMembershipsAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateMembership_LeavesProfileAndSettingsCached()
    {
        var repoId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var dashboard = Substitute.For<IDashboardClient>();
        dashboard.GetUserAsync(userId, Arg.Any<CancellationToken>()).Returns(Profile(userId));

        var service = new DirectoryService(dashboard, new FakeDistributedCache());

        await service.GetUserAsync(userId, Ct);
        await service.InvalidateMembershipAsync(repoId, userId, Ct);
        await service.GetUserAsync(userId, Ct);

        // A membership change says nothing about the user's name or avatar, so re-pulling them would
        // be wasted load on DASHBOARD.
        await dashboard.Received(1).GetUserAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateMembership_OnlyTouchesTheRepositoryThatChanged()
    {
        var changedRepo = Guid.NewGuid();
        var otherRepo   = Guid.NewGuid();
        var userId      = Guid.NewGuid();

        var dashboard = Substitute.For<IDashboardClient>();
        dashboard.GetRepositoryMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                 .Returns([Profile(userId)]);

        var service = new DirectoryService(dashboard, new FakeDistributedCache());

        await service.GetWorkspaceMembersAsync(changedRepo, Ct);
        await service.GetWorkspaceMembersAsync(otherRepo, Ct);

        await service.InvalidateMembershipAsync(changedRepo, userId, Ct);
        await service.GetWorkspaceMembersAsync(otherRepo, Ct);

        await dashboard.Received(1).GetRepositoryMembersAsync(otherRepo, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consumer_InvalidatesTheRepositoryAndUserCarriedByTheEvent()
    {
        var repoId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var directory = Substitute.For<IDirectoryService>();
        var consumer  = new MemberDirectoryChangedConsumer(
            directory, NullLogger<MemberDirectoryChangedConsumer>.Instance);

        var context = Substitute.For<ConsumeContext<MemberDirectoryChangedEvent>>();
        context.Message.Returns(new MemberDirectoryChangedEvent(repoId, userId));
        context.CancellationToken.Returns(Ct);

        await consumer.Consume(context);

        await directory.Received(1).InvalidateMembershipAsync(repoId, userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consumer_IsIdempotent_SoRedeliveryIsHarmless()
    {
        var repoId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var dashboard = Substitute.For<IDashboardClient>();
        dashboard.GetRepositoryMembersAsync(repoId, Arg.Any<CancellationToken>())
                 .Returns([Profile(userId)]);

        var service  = new DirectoryService(dashboard, new FakeDistributedCache());
        var consumer = new MemberDirectoryChangedConsumer(
            service, NullLogger<MemberDirectoryChangedConsumer>.Instance);

        var context = Substitute.For<ConsumeContext<MemberDirectoryChangedEvent>>();
        context.Message.Returns(new MemberDirectoryChangedEvent(repoId, userId));
        context.CancellationToken.Returns(Ct);

        // MassTransit delivers at-least-once, so a redelivery must not throw — evicting an absent key
        // is a no-op, which is why this consumer needs no deduplication.
        await consumer.Consume(context);
        await Should.NotThrowAsync(() => consumer.Consume(context));
    }

    // ── DirectoryEntryChangedEvent: profile / settings / work item ───────────

    private static ConsumeContext<DirectoryEntryChangedEvent> EntryContext(DirectoryEntryKind kind, Guid id)
    {
        var context = Substitute.For<ConsumeContext<DirectoryEntryChangedEvent>>();
        context.Message.Returns(new DirectoryEntryChangedEvent(kind, id));
        context.CancellationToken.Returns(Ct);
        return context;
    }

    private static DirectoryEntryChangedConsumer EntryConsumer(IDirectoryService directory) =>
        new(directory, NullLogger<DirectoryEntryChangedConsumer>.Instance);

    [Fact]
    public async Task ProfileEvent_ForcesTheNextProfileReadToPullAgain()
    {
        var userId = Guid.NewGuid();

        var dashboard = Substitute.For<IDashboardClient>();
        dashboard.GetUserAsync(userId, Arg.Any<CancellationToken>()).Returns(Profile(userId));

        var service = new DirectoryService(dashboard, new FakeDistributedCache());

        await service.GetUserAsync(userId, Ct);
        await EntryConsumer(service).Consume(EntryContext(DirectoryEntryKind.UserProfile, userId));
        await service.GetUserAsync(userId, Ct);

        // IsGlobalAdmin and IsDeleted ride on this entry, so a stale copy is a privilege problem, not
        // just a wrong display name.
        await dashboard.Received(2).GetUserAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SettingsEvent_EvictsSettingsAndLeavesTheProfileAlone()
    {
        var userId = Guid.NewGuid();

        var dashboard = Substitute.For<IDashboardClient>();
        dashboard.GetUserAsync(userId, Arg.Any<CancellationToken>()).Returns(Profile(userId));
        dashboard.GetUserSettingsAsync(userId, Arg.Any<CancellationToken>())
                 .Returns(new UserSettings(new Dictionary<string, string?> { ["ui.timezone"] = "UTC" }));

        var service = new DirectoryService(dashboard, new FakeDistributedCache());

        await service.GetUserAsync(userId, Ct);
        await service.GetUserSettingsAsync(userId, Ct);

        await EntryConsumer(service).Consume(EntryContext(DirectoryEntryKind.UserSettings, userId));

        await service.GetUserSettingsAsync(userId, Ct);
        await service.GetUserAsync(userId, Ct);

        await dashboard.Received(2).GetUserSettingsAsync(userId, Arg.Any<CancellationToken>());
        await dashboard.Received(1).GetUserAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WorkItemEvent_EvictsOnlyThatWorkItem()
    {
        var changed = Guid.NewGuid();
        var other   = Guid.NewGuid();

        var dashboard = Substitute.For<IDashboardClient>();
        dashboard.GetWorkItemAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                 .Returns(c => new WorkItemContext(c.Arg<Guid>(), "DASH-1", "Title", "Active", Guid.NewGuid(), "DASH"));

        var service = new DirectoryService(dashboard, new FakeDistributedCache());

        await service.GetWorkItemAsync(changed, Ct);
        await service.GetWorkItemAsync(other, Ct);

        await EntryConsumer(service).Consume(EntryContext(DirectoryEntryKind.WorkItem, changed));

        await service.GetWorkItemAsync(changed, Ct);
        await service.GetWorkItemAsync(other, Ct);

        await dashboard.Received(2).GetWorkItemAsync(changed, Arg.Any<CancellationToken>());
        await dashboard.Received(1).GetWorkItemAsync(other, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnknownKind_IsLoggedRatherThanThrown()
    {
        var directory = Substitute.For<IDirectoryService>();

        // A kind DASHBOARD might add before this consumer handles it: retrying cannot help, so the
        // message must not be dead-lettered.
        await Should.NotThrowAsync(() =>
            EntryConsumer(directory).Consume(EntryContext((DirectoryEntryKind)99, Guid.NewGuid())));
    }

    // ── Batch profile lookup ────────────────────────────────────────────────

    [Fact]
    public async Task GetUsers_PullsOnlyTheIdsNotAlreadyCached()
    {
        var cachedUser = Guid.NewGuid();
        var freshUser  = Guid.NewGuid();

        var dashboard = Substitute.For<IDashboardClient>();
        dashboard.GetUserAsync(cachedUser, Arg.Any<CancellationToken>()).Returns(Profile(cachedUser));
        dashboard.GetUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns(c => c.Arg<IReadOnlyCollection<Guid>>().Select(Profile).ToList());

        var service = new DirectoryService(dashboard, new FakeDistributedCache());

        // Warmed through the single-user path — the batch path must reuse that entry, which is the
        // whole point of keying per user instead of per batch.
        await service.GetUserAsync(cachedUser, Ct);

        var result = await service.GetUsersAsync([cachedUser, freshUser], Ct);

        result.Select(p => p.Id).ShouldBe(new[] { cachedUser, freshUser }, ignoreOrder: true);
        await dashboard.Received(1).GetUsersAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(freshUser)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsers_SkipsTheUpstreamCallEntirelyWhenEverythingIsCached()
    {
        var userId = Guid.NewGuid();

        var dashboard = Substitute.For<IDashboardClient>();
        dashboard.GetUserAsync(userId, Arg.Any<CancellationToken>()).Returns(Profile(userId));

        var service = new DirectoryService(dashboard, new FakeDistributedCache());

        await service.GetUserAsync(userId, Ct);
        await service.GetUsersAsync([userId], Ct);

        await dashboard.DidNotReceive().GetUsersAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsers_WarmsTheSameKeysTheSingleUserPathReads()
    {
        var userId = Guid.NewGuid();

        var dashboard = Substitute.For<IDashboardClient>();
        dashboard.GetUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns([Profile(userId)]);

        var service = new DirectoryService(dashboard, new FakeDistributedCache());

        await service.GetUsersAsync([userId], Ct);
        var single = await service.GetUserAsync(userId, Ct);

        single!.Name.ShouldBe("Nam");
        await dashboard.DidNotReceive().GetUserAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsers_ProfileEventEvictsAnEntryWarmedByTheBatchPath()
    {
        var userId = Guid.NewGuid();

        var dashboard = Substitute.For<IDashboardClient>();
        dashboard.GetUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                 .Returns([Profile(userId)]);

        var service = new DirectoryService(dashboard, new FakeDistributedCache());

        await service.GetUsersAsync([userId], Ct);
        await EntryConsumer(service).Consume(EntryContext(DirectoryEntryKind.UserProfile, userId));
        await service.GetUsersAsync([userId], Ct);

        // A batch-shaped cache key could not be reached by a single user's invalidation; per-user keys
        // can, and this is the test that would fail if someone changed that.
        await dashboard.Received(2).GetUsersAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsers_ReturnsEmptyWithoutCallingUpstream_WhenNoIdsGiven()
    {
        var dashboard = Substitute.For<IDashboardClient>();
        var service   = new DirectoryService(dashboard, new FakeDistributedCache());

        (await service.GetUsersAsync([], Ct)).ShouldBeEmpty();

        await dashboard.DidNotReceive().GetUsersAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }
}
