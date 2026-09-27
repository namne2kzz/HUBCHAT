using HUB.DashboardGateway.Dashboard;
using HUB.TestKit.Fakes;
using Shouldly;
using Xunit;

namespace HUB.DashboardGateway.UnitTests;

public sealed class DashboardClientTests
{
    private static DashboardClient ClientWith(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://dashboard/internal/v1/") });

    [Fact]
    public async Task GetUserAsync_ParsesProfile()
    {
        var id = Guid.NewGuid();
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(
            $$"""
            { "id": "{{id}}", "name": "Nam", "email": "nam@x.com",
              "avatarClass": "bg-sky-600", "isGlobalAdmin": false, "isDeleted": false }
            """));

        var user = await ClientWith(handler).GetUserAsync(id, CancellationToken.None);

        user.ShouldNotBeNull();
        user!.Name.ShouldBe("Nam");
        user.AvatarClass.ShouldBe("bg-sky-600");
    }

    [Fact]
    public async Task GetUserAsync_ReturnsNull_On404()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.NotFound());

        var user = await ClientWith(handler).GetUserAsync(Guid.NewGuid(), CancellationToken.None);

        user.ShouldBeNull();
    }

    [Fact]
    public async Task GetMembershipsAsync_ParsesRepositoriesAndPermissions()
    {
        var userId = Guid.NewGuid();
        var repoId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(
            $$"""
            { "userId": "{{userId}}", "isGlobalAdmin": false,
              "repositories": [
                { "repositoryId": "{{repoId}}", "repositoryName": "Core", "repositoryCode": "DASH",
                  "isArchived": false, "roleId": "{{roleId}}", "roleName": "Maintainer",
                  "permissions": ["ViewRepository", "CreateWorkItem"], "defaultRole": "Backend" }
              ] }
            """));

        var result = await ClientWith(handler).GetMembershipsAsync(userId, CancellationToken.None);

        result.ShouldNotBeNull();
        var repo = result!.Repositories.ShouldHaveSingleItem();
        repo.RepositoryCode.ShouldBe("DASH");
        repo.Permissions.ShouldBe(new[] { "ViewRepository", "CreateWorkItem" });
    }
}
