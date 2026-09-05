using HUB.DashboardGateway.Dashboard;
using HUB.DashboardGateway.Directory;
using NSubstitute;
using Shouldly;
using Xunit;

namespace HUB.DashboardGateway.Tests;

public sealed class DirectoryServiceTests
{
    [Fact]
    public async Task GetUserAsync_PullsOnce_ThenServesFromCache()
    {
        var ct = CancellationToken.None;
        var userId = Guid.NewGuid();
        var profile = new UserProfile(userId, "Nam", "nam@x.com", "bg-sky-600", false, false);

        var dashboard = Substitute.For<IDashboardClient>();
        dashboard.GetUserAsync(userId, Arg.Any<CancellationToken>()).Returns(profile);

        var service = new DirectoryService(dashboard, new FakeDistributedCache());

        var first  = await service.GetUserAsync(userId, ct);
        var second = await service.GetUserAsync(userId, ct);

        first!.Name.ShouldBe("Nam");
        second!.Name.ShouldBe("Nam");
        await dashboard.Received(1).GetUserAsync(userId, Arg.Any<CancellationToken>()); // cached second time
    }

    [Fact]
    public async Task GetMembershipsAsync_ReturnsNull_WhenUserMissing()
    {
        var ct = CancellationToken.None;
        var userId = Guid.NewGuid();

        var dashboard = Substitute.For<IDashboardClient>();
        dashboard.GetMembershipsAsync(userId, Arg.Any<CancellationToken>()).Returns((UserMemberships?)null);

        var service = new DirectoryService(dashboard, new FakeDistributedCache());

        var result = await service.GetMembershipsAsync(userId, ct);

        result.ShouldBeNull();
    }
}
