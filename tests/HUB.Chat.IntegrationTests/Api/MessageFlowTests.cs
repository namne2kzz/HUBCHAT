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
/// Drives the message endpoints end to end: HTTP in, PostgreSQL out, integration events on the way.
/// </summary>
/// <remarks>
/// The unit suite covers the same handlers on SQLite, which is faster but cannot speak to the real
/// <c>jsonb</c> mentions column, real index behaviour, or Npgsql's translation. This file is where the
/// paging and mention round-trip are exercised against the database the service actually runs on.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
[Trait(TestCategories.Category, TestCategories.RequiresDocker)]
public sealed class MessageFlowTests(PostgresFixture database) : IAsyncLifetime
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

    private async Task<Guid> CreateChannelAsync(HttpClient client, int type = 0)
    {
        var response = await client.PostAsJsonAsync("/api/v1/channels", new
        {
            workspaceId = Guid.NewGuid(),
            name        = "Flow",
            type,
            topic       = (string?)null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<ChannelResponse>())!.Id;
    }

    private static object Post(string body, Guid[]? mentions = null, Guid? parentId = null) => new
    {
        body,
        format           = 1,                       // Markdown — numeric, no JsonStringEnumConverter
        parentId,
        mentionedUserIds = mentions ?? [],
    };

    // ── Posting ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AMemberCanPostAndReadTheMessageBack()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);

        var posted = await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", Post("hello world"));
        posted.StatusCode.ShouldBe(HttpStatusCode.Created);

        var listed = await client.GetAsync($"/api/v1/channels/{channelId}/messages");
        listed.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await listed.Content.ReadFromJsonAsync<CursorPageResponse>();
        page!.Items.ShouldHaveSingleItem().Body.ShouldBe("hello world");
    }

    [Fact]
    public async Task ANonMemberCannotPostToAPublicChannel()
    {
        using var owner = As(Guid.NewGuid());
        var channelId = await CreateChannelAsync(owner);

        using var stranger = As(Guid.NewGuid());
        var response = await stranger.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", Post("hi"));

        // Public means readable, not writable — posting still requires joining.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ANonMemberCanReadAPublicChannelButNotAPrivateOne()
    {
        using var owner = As(Guid.NewGuid());
        var publicId  = await CreateChannelAsync(owner, type: 0);
        var privateId = await CreateChannelAsync(owner, type: 1);

        using var stranger = As(Guid.NewGuid());

        (await stranger.GetAsync($"/api/v1/channels/{publicId}/messages"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await stranger.GetAsync($"/api/v1/channels/{privateId}/messages"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task JoiningAChannelThenPostingSucceeds()
    {
        using var owner = As(Guid.NewGuid());
        var channelId = await CreateChannelAsync(owner);

        var joiner = Guid.NewGuid();
        using var client = As(joiner);

        (await client.PostAsync($"/api/v1/channels/{channelId}/members", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", Post("now I'm in")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    // ── Mentions through the real jsonb column ──────────────────────────────

    [Fact]
    public async Task MentionsSurviveTheRoundTripThroughJsonb()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);

        Guid[] mentioned = [Guid.NewGuid(), Guid.NewGuid()];

        await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", Post("hi both", mentioned));

        var page = await (await client.GetAsync($"/api/v1/channels/{channelId}/messages"))
            .Content.ReadFromJsonAsync<CursorPageResponse>();

        // The unit suite rewrites this column to TEXT on SQLite, so this is the only place the real jsonb
        // mapping is proven through the full HTTP path.
        page!.Items.ShouldHaveSingleItem().Mentions.ShouldBe(mentioned);
    }

    [Fact]
    public async Task PostingPublishesMessageSentAndOneUserMentionedPerMention()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);

        Events.Clear();

        Guid[] mentioned = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", Post("@all", mentioned));

        // Realtime fan-out and mention notifications are separate services that only learn about a
        // message through these events. A request that saves the row but publishes nothing leaves the
        // message invisible to every live client until a refresh.
        Events.OfType<MessageSent>().ShouldHaveSingleItem().ChannelId.ShouldBe(channelId);
        Events.OfType<UserMentioned>().Count.ShouldBe(3);
    }

    [Fact]
    public async Task ARefusedPostPublishesNothing()
    {
        using var owner = As(Guid.NewGuid());
        var channelId = await CreateChannelAsync(owner);

        Events.Clear();

        using var stranger = As(Guid.NewGuid());
        var response = await stranger.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", Post("nope"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        Events.Published.ShouldBeEmpty("a rejected request must not announce itself on the bus");
    }

    // ── Paging against real PostgreSQL ──────────────────────────────────────

    [Fact]
    public async Task ThePageIsNewestFirstAndTheCursorWalksBackwards()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);

        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", Post($"message {i}"));

        var first = await (await client.GetAsync($"/api/v1/channels/{channelId}/messages?limit=2"))
            .Content.ReadFromJsonAsync<CursorPageResponse>();

        first!.Items.Count.ShouldBe(2);
        first.Items[0].Body.ShouldBe("message 4", "newest first");
        first.NextCursor.ShouldNotBeNull();

        var second = await (await client.GetAsync(
                $"/api/v1/channels/{channelId}/messages?limit=2&cursor={Uri.EscapeDataString(first.NextCursor!)}"))
            .Content.ReadFromJsonAsync<CursorPageResponse>();

        second!.Items.Select(m => m.Id)
            .Intersect(first.Items.Select(m => m.Id))
            .ShouldBeEmpty("pages must not overlap");
    }

    [Fact]
    public async Task ThreadRepliesDoNotAppearInTheChannelTimeline()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);

        var parentResponse = await client.PostAsJsonAsync(
            $"/api/v1/channels/{channelId}/messages", Post("top level"));
        var parent = await parentResponse.Content.ReadFromJsonAsync<MessageResponse>();

        await client.PostAsJsonAsync(
            $"/api/v1/channels/{channelId}/messages", Post("a reply", parentId: parent!.Id));

        var page = await (await client.GetAsync($"/api/v1/channels/{channelId}/messages"))
            .Content.ReadFromJsonAsync<CursorPageResponse>();

        page!.Items.ShouldHaveSingleItem().Id.ShouldBe(parent.Id);
    }

    [Fact]
    public async Task AnOversizedLimitIsCappedByTheServer()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);

        for (var i = 0; i < 3; i++)
            await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", Post($"m{i}"));

        // The clamp is what stops a client pulling a channel's whole history in one request; asserted over
        // HTTP because the query string is where an arbitrary number actually arrives.
        var page = await (await client.GetAsync($"/api/v1/channels/{channelId}/messages?limit=100000"))
            .Content.ReadFromJsonAsync<CursorPageResponse>();

        page!.Items.Count.ShouldBe(3);
    }

    // ── Reconnect catch-up (messages after an anchor) ───────────────────────

    [Fact]
    public async Task CatchUpReturnsWhatWasPostedAfterTheAnchorInOrder()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);

        var ids = new List<Guid>();
        foreach (var body in new[] { "seen", "missed-1", "missed-2", "missed-3" })
        {
            var r = await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", Post(body));
            ids.Add((await r.Content.ReadFromJsonAsync<MessageResponse>())!.Id);
        }

        // Page of 2 then the rest — the client loops on the last id, so the seek must be exact on uuid ordering.
        var first = await client.GetFromJsonAsync<List<MessageResponse>>(
            $"/api/v1/channels/{channelId}/messages/after/{ids[0]}?limit=2");
        var rest  = await client.GetFromJsonAsync<List<MessageResponse>>(
            $"/api/v1/channels/{channelId}/messages/after/{first![^1].Id}?limit=2");

        first.Select(m => m.Body).Concat(rest!.Select(m => m.Body))
             .ShouldBe(["missed-1", "missed-2", "missed-3"]);
    }

    [Fact]
    public async Task CatchUpWithAnAnchorFromAnotherChannelIs404()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);
        var otherId   = await CreateChannelAsync(client);
        var posted    = await client.PostAsJsonAsync($"/api/v1/channels/{otherId}/messages", Post("elsewhere"));
        var foreignId = (await posted.Content.ReadFromJsonAsync<MessageResponse>())!.Id;

        var response = await client.GetAsync($"/api/v1/channels/{channelId}/messages/after/{foreignId}");

        // 404 tells the client to reload from the newest page rather than trust a bogus position.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── Reactions ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ReactingIsIdempotentOverHttp()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);

        var posted  = await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", Post("react to me"));
        var message = await posted.Content.ReadFromJsonAsync<MessageResponse>();

        var route = $"/api/v1/messages/{message!.Id}/reactions";

        (await client.PostAsJsonAsync(route, new { emoji = ":+1:" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync(route, new { emoji = ":+1:" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var page = await (await client.GetAsync($"/api/v1/channels/{channelId}/messages"))
            .Content.ReadFromJsonAsync<CursorPageResponse>();

        // A double-click must not double the count.
        page!.Items.ShouldHaveSingleItem().Reactions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ReactingToAMessageThatDoesNotExistIsNotFound()
    {
        using var client = As(Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            $"/api/v1/messages/{Guid.NewGuid()}/reactions", new { emoji = ":+1:" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private sealed record ChannelResponse(Guid Id);

    private sealed record MessageResponse(Guid Id, string Body);

    private sealed record ReactionResponse(string Emoji, Guid UserId);

    private sealed record MessageItem(
        Guid Id, Guid ChannelId, Guid? ParentId, string Body,
        IReadOnlyList<Guid> Mentions, IReadOnlyList<ReactionResponse> Reactions);

    private sealed record CursorPageResponse(IReadOnlyList<MessageItem> Items, string? NextCursor);
}
