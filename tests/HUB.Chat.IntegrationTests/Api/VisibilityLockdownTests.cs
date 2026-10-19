using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HUB.Chat.IntegrationTests.Infrastructure;
using HUB.Shared.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace HUB.Chat.IntegrationTests.Api;

/// <summary>
/// Covers locking a public channel down to private end to end: the owner flips visibility and the
/// service announces the member list so realtime can drop non-member watchers.
/// </summary>
/// <remarks>
/// The bug this guards: a public channel admitted anyone to its realtime group, and making it private
/// used to save the row without telling realtime, so the people watching kept receiving messages. The
/// unit test proves the handler publishes the event; this proves it survives the full HTTP + Postgres path.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
[Trait(TestCategories.Category, TestCategories.RequiresDocker)]
public sealed class VisibilityLockdownTests(PostgresFixture database) : IAsyncLifetime
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

    private RecordingEventCollector Events => _factory.Services.GetRequiredService<RecordingEventCollector>();

    private async Task<Guid> CreatePublicChannelAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/channels", new
        {
            workspaceId = Guid.NewGuid(), name = "open", type = 0, topic = (string?)null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ChannelResponse>())!.Id;
    }

    [Fact]
    public async Task MakingAPublicChannelPrivateAnnouncesTheMemberList()
    {
        var owner = Guid.NewGuid();
        using var client = As(owner);
        var channelId = await CreatePublicChannelAsync(client);

        // A second member joins so the lock-down event carries more than just the owner.
        var member = Guid.NewGuid();
        using var memberClient = As(member);
        (await memberClient.PostAsync($"/api/v1/channels/{channelId}/members", null)).EnsureSuccessStatusCode();

        Events.Clear();

        var response = await client.PatchAsJsonAsync($"/api/v1/channels/{channelId}/visibility", new { isPrivate = true });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var locked = Events.OfType<ChannelMadePrivate>().ShouldHaveSingleItem();
        locked.ChannelId.ShouldBe(channelId);
        locked.MemberIds.ShouldBe(new[] { owner, member }, ignoreOrder: true);
    }

    [Fact]
    public async Task MakingAPrivateChannelPublicAnnouncesNoLockdown()
    {
        var owner = Guid.NewGuid();
        using var client = As(owner);
        // Create private, then open it up.
        var created = await client.PostAsJsonAsync("/api/v1/channels", new
        {
            workspaceId = Guid.NewGuid(), name = "secret", type = 1, topic = (string?)null,
        });
        var channelId = (await created.Content.ReadFromJsonAsync<ChannelResponse>())!.Id;

        Events.Clear();
        (await client.PatchAsJsonAsync($"/api/v1/channels/{channelId}/visibility", new { isPrivate = false }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Opening up grants access, never revokes it — no lock-down event.
        Events.OfType<ChannelMadePrivate>().ShouldBeEmpty();
    }

    [Fact]
    public async Task OnlyTheOwnerCanChangeVisibility()
    {
        using var owner = As(Guid.NewGuid());
        var channelId = await CreatePublicChannelAsync(owner);

        var member = Guid.NewGuid();
        using var memberClient = As(member);
        (await memberClient.PostAsync($"/api/v1/channels/{channelId}/members", null)).EnsureSuccessStatusCode();

        Events.Clear();
        var response = await memberClient.PatchAsJsonAsync($"/api/v1/channels/{channelId}/visibility", new { isPrivate = true });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        Events.OfType<ChannelMadePrivate>().ShouldBeEmpty("a refused change must not revoke anyone");
    }

    private sealed record ChannelResponse(Guid Id);
}
