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
/// Concurrent duplicates against real PostgreSQL: idempotent send (<c>ClientMessageId</c>) and the
/// find-or-create endpoints (linked thread, DM).
/// </summary>
/// <remarks>
/// These only mean something here. The guarantees rest on unique indexes and on Npgsql reporting a
/// violation as SQLSTATE 23505, which <c>ChatDbContext</c> translates — SQLite in the unit suite has
/// neither the same index semantics nor that error. Each test fires the same request in parallel so the
/// lookup-then-insert window is actually contended; before MP-3 these produced duplicates or 500s.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
[Trait(TestCategories.Category, TestCategories.RequiresDocker)]
public sealed class IdempotencyTests(PostgresFixture database) : IAsyncLifetime
{
    private const int Parallel = 8;

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

    private static async Task<Guid> CreateChannelAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/channels", new
        {
            workspaceId = Guid.NewGuid(),
            name        = $"idem-{Guid.NewGuid():N}"[..20],
            type        = 0,
            topic       = (string?)null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private static object Send(string body, Guid? clientMessageId) => new
    {
        body,
        format           = 1,
        parentId         = (Guid?)null,
        mentionedUserIds = Array.Empty<Guid>(),
        clientMessageId,
    };

    // ── PostMessage ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentSendsWithOneClientKeyCreateOneMessage()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);
        var key       = Guid.NewGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, Parallel).Select(_ =>
            client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", Send("once", key))));

        // Every caller sees success — the losers of the insert race get the winner's message, not a 409/500.
        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Created);
        var ids = await Task.WhenAll(responses.Select(async r => (await r.Content.ReadFromJsonAsync<IdResponse>())!.Id));
        ids.Distinct().ShouldHaveSingleItem();

        var page = await client.GetFromJsonAsync<PageResponse>($"/api/v1/channels/{channelId}/messages");
        page!.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ASequentialRetryPublishesMessageSentOnce()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);
        var key       = Guid.NewGuid();

        var first  = await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", Send("retry me", key));
        var second = await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", Send("retry me", key));

        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        var firstId = (await first.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        (await second.Content.ReadFromJsonAsync<IdResponse>())!.Id.ShouldBe(firstId);

        // (Concurrent losers do record a publish here — the recorder sits in front of the outbox; in
        // production their outbox rows roll back with the failed insert. Sequential replays publish nothing.)
        Events.OfType<MessageSent>().Count(e => e.MessageId == firstId).ShouldBe(1);
    }

    // ── Find-or-create ──────────────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentOpensOfOneLinkedResourceYieldOneThreadWithEveryOpenerAsMember()
    {
        var workspaceId = Guid.NewGuid();
        var workItemId  = Guid.NewGuid();
        var users       = Enumerable.Range(0, Parallel).Select(_ => Guid.NewGuid()).ToList();

        var responses = await Task.WhenAll(users.Select(u =>
        {
            var client = As(u);
            return client.PostAsJsonAsync("/api/v1/channels/linked", new
            {
                workspaceId,
                linkType    = 0, // WorkItem
                externalId  = workItemId,
                externalKey = "WI-42",
                title       = "Fix login",
                url         = "https://dashboard/wi/42",
            });
        }));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        var ids = await Task.WhenAll(responses.Select(async r => (await r.Content.ReadFromJsonAsync<IdResponse>())!.Id));
        var threadId = ids.Distinct().ShouldHaveSingleItem();

        // A loser must still end up in the winner's thread, not just be handed its id.
        using var reader = As(users[0]);
        var members = await reader.GetFromJsonAsync<List<MemberResponse>>($"/api/v1/channels/{threadId}/members");
        members!.Select(m => m.UserId).ShouldBe(users, ignoreOrder: true);
    }

    [Fact]
    public async Task BothUsersOpeningTheirDmAtOnceGetTheSameChannel()
    {
        var workspaceId = Guid.NewGuid();
        var alice = Guid.NewGuid();
        var bob   = Guid.NewGuid();

        var calls = Enumerable.Range(0, Parallel).Select(i =>
        {
            var (me, them) = i % 2 == 0 ? (alice, bob) : (bob, alice);
            return As(me).PostAsync($"/api/v1/channels/dm/{them}?workspaceId={workspaceId}", null);
        });
        var responses = await Task.WhenAll(calls);

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        var ids = await Task.WhenAll(responses.Select(async r => (await r.Content.ReadFromJsonAsync<IdResponse>())!.Id));
        ids.Distinct().ShouldHaveSingleItem();
    }

    private sealed record IdResponse(Guid Id);

    private sealed record PageResponse(List<IdResponse> Items);

    private sealed record MemberResponse(Guid UserId);
}
