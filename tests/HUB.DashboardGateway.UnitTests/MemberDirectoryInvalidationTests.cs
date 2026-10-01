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
}
