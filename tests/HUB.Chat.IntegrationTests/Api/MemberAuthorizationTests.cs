using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace HUB.Chat.IntegrationTests.Api;

/// <summary>
/// The add/remove-member endpoints over real HTTP: who may change someone else's membership.
/// </summary>
/// <remarks>
/// Before this was enforced, both endpoints trusted any signed-in caller: a user could add themselves to
/// another team's private channel (and read it) or remove anyone from any channel. These tests replay
/// exactly those requests and expect 403.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
[Trait(TestCategories.Category, TestCategories.RequiresDocker)]
public sealed class MemberAuthorizationTests(PostgresFixture database) : IAsyncLifetime
{
    private ApiFactory _factory = null!;

    /// <inheritdoc />
    public Task InitializeAsync()
    {
        _factory = new ApiFactory(database);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private HttpClient As(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.For(userId));
        return client;
    }

    private static async Task<ChannelResponse> CreatePrivateChannelAsync(HttpClient owner)
    {
        var response = await owner.PostAsJsonAsync("/api/v1/channels", new
        {
            workspaceId = Guid.NewGuid(),
            name        = $"secret-{Guid.NewGuid():N}"[..20],
            type        = 1, // Private
            topic       = (string?)null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ChannelResponse>())!;
    }

    [Fact]
    public async Task AStrangerCannotAddThemselvesToAPrivateChannel()
    {
        using var owner = As(Guid.NewGuid());
        var channel = await CreatePrivateChannelAsync(owner);

        var strangerId = Guid.NewGuid();
        using var stranger = As(strangerId);
        var response = await stranger.PutAsync($"/api/v1/channels/{channel.Id}/members/{strangerId}", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        // And the read that the join would have unlocked stays closed.
        (await stranger.GetAsync($"/api/v1/channels/{channel.Id}/messages")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AStrangerCannotRemoveTheOwnersColleague()
    {
        var ownerId     = Guid.NewGuid();
        var colleagueId = Guid.NewGuid();
        using var owner = As(ownerId);
        var channel = await CreatePrivateChannelAsync(owner);
        (await owner.PutAsync($"/api/v1/channels/{channel.Id}/members/{colleagueId}", null)).EnsureSuccessStatusCode();

        using var stranger = As(Guid.NewGuid());
        var response = await stranger.DeleteAsync($"/api/v1/channels/{channel.Id}/members/{colleagueId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var members = await owner.GetFromJsonAsync<List<MemberResponse>>($"/api/v1/channels/{channel.Id}/members");
        members!.Select(m => m.UserId).ShouldContain(colleagueId);
    }

    [Fact]
    public async Task AWorkspaceManagerWithoutAChannelRoleCanAddMembers()
    {
        using var owner = As(Guid.NewGuid());
        var channel = await CreatePrivateChannelAsync(owner);

        var managerId = Guid.NewGuid();
        _factory.WorkspacePermissions.Grant(channel.WorkspaceId, WorkspacePermissionNames.ManageChannels);
        using var manager = As(managerId);

        var response = await manager.PutAsync($"/api/v1/channels/{channel.Id}/members/{Guid.NewGuid()}", null);

        // Parity with HUB.VIEW, which shows the add button to ManageChannels holders.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheInternalSprintSyncStillAddsWithoutAUser()
    {
        using var owner = As(Guid.NewGuid());
        var channel = await CreatePrivateChannelAsync(owner);

        // The trusted service-to-service path (DASHBOARD sprint capacity sync) must keep working.
        using var internalClient = _factory.CreateClient();
        internalClient.DefaultRequestHeaders.Add(HUB.Shared.Auth.ServiceTokenDefaults.Header, ApiFactory.InternalToken);
        var response = await internalClient.PostAsync($"/internal/channels/{channel.Id}/members/{Guid.NewGuid()}", null);

        response.IsSuccessStatusCode.ShouldBeTrue();
    }

    private sealed record ChannelResponse(Guid Id, Guid WorkspaceId);

    private sealed record MemberResponse(Guid UserId);
}
