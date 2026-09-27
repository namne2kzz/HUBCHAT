using System.Net;
using System.Net.Http.Json;
using HUB.Chat.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace HUB.Chat.IntegrationTests.Api;

/// <summary>
/// Covers the <c>/internal/*</c> routes, which DASHBOARD's backend calls machine-to-machine with a shared
/// token instead of a JWT.
/// </summary>
/// <remarks>
/// These routes are <c>[AllowAnonymous]</c> and sit behind <c>InternalApiKeyMiddleware</c> alone. That
/// makes the middleware the only thing between the open internet and endpoints that force-add members and
/// archive channels — so the refusal cases are the substance of this file, not an afterthought.
///
/// The middleware runs before <c>UseHubAuth</c>, which is deliberate: an internal route should be rejected
/// on the token without ever touching JWT validation.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
[Trait(TestCategories.Category, TestCategories.RequiresDocker)]
public sealed class InternalApiTests(PostgresFixture database) : IAsyncLifetime
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

    private HttpClient WithToken(string? token)
    {
        var client = _factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Add("X-Internal-Token", token);
        return client;
    }

    private static object SprintPayload(Guid workspaceId, Guid sprintId) => new
    {
        workspaceId,
        sprintId,
        sprintName    = "Sprint 1",
        creatorUserId = Guid.NewGuid(),
    };

    // ── The token gate ──────────────────────────────────────────────────────

    public static TheoryData<string, string> InternalRoutes => new()
    {
        { "POST",   "/internal/channels/sprint" },
        { "POST",   "/internal/channels/00000000-0000-0000-0000-000000000001/members/00000000-0000-0000-0000-000000000002" },
        { "DELETE", "/internal/channels/00000000-0000-0000-0000-000000000001/members/00000000-0000-0000-0000-000000000002" },
        { "POST",   "/internal/channels/00000000-0000-0000-0000-000000000001/archive" },
    };

    [Theory]
    [MemberData(nameof(InternalRoutes))]
    public async Task AnInternalRouteWithNoTokenIsRefused(string method, string route)
    {
        using var client = WithToken(null);

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), route));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(InternalRoutes))]
    public async Task AnInternalRouteWithTheWrongTokenIsRefused(string method, string route)
    {
        using var client = WithToken("not-the-internal-token");

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), route));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ATokenThatOnlySharesAPrefixIsRefused()
    {
        // The comparison is ordinal and whole-string. A prefix match would let a truncated or guessed
        // value through, and these endpoints need no JWT once past this check.
        using var client = WithToken(ApiFactory.InternalToken[..10]);

        var response = await client.PostAsJsonAsync(
            "/internal/channels/sprint", SprintPayload(Guid.NewGuid(), Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ATokenDifferingOnlyInCaseIsRefused()
    {
        // StringComparison.Ordinal, so case matters. Pinned because a case-insensitive comparison would
        // quietly shrink the keyspace.
        using var client = WithToken(ApiFactory.InternalToken.ToUpperInvariant());

        var response = await client.PostAsJsonAsync(
            "/internal/channels/sprint", SprintPayload(Guid.NewGuid(), Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AJwtDoesNotSubstituteForTheInternalToken()
    {
        // An ordinary signed-in user must not be able to reach the machine-to-machine routes just by
        // being authenticated: these bypass every membership check the public API applies.
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokens.For(Guid.NewGuid()));

        var response = await client.PostAsJsonAsync(
            "/internal/channels/sprint", SprintPayload(Guid.NewGuid(), Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        client.Dispose();
    }

    [Fact]
    public async Task TheTokenGateDoesNotApplyToThePublicApi()
    {
        // The middleware matches on the /internal path segment only. If it ever guarded everything, the
        // whole public API would start demanding a token DASHBOARD alone knows.
        using var client = WithToken(null);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokens.For(Guid.NewGuid()));

        var response = await client.GetAsync($"/api/v1/channels?workspaceId={Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── With a valid token ──────────────────────────────────────────────────

    [Fact]
    public async Task CreatingASprintChannelSucceedsWithTheRightToken()
    {
        using var client = WithToken(ApiFactory.InternalToken);

        var response = await client.PostAsJsonAsync(
            "/internal/channels/sprint", SprintPayload(Guid.NewGuid(), Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var channel = await response.Content.ReadFromJsonAsync<ChannelResponse>();
        channel!.Name.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreatingTheSameSprintChannelTwiceReturnsTheSameChannel()
    {
        // DASHBOARD calls this whenever a sprint is opened, and it has no memory of whether it called
        // before — so the endpoint documents itself as idempotent. A second call creating a duplicate
        // would split one sprint's conversation across two channels.
        using var client = WithToken(ApiFactory.InternalToken);

        var workspaceId = Guid.NewGuid();
        var sprintId    = Guid.NewGuid();

        var first  = await client.PostAsJsonAsync("/internal/channels/sprint", SprintPayload(workspaceId, sprintId));
        var second = await client.PostAsJsonAsync("/internal/channels/sprint", SprintPayload(workspaceId, sprintId));

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        var firstChannel  = await first.Content.ReadFromJsonAsync<ChannelResponse>();
        var secondChannel = await second.Content.ReadFromJsonAsync<ChannelResponse>();

        secondChannel!.Id.ShouldBe(firstChannel!.Id);
    }

    [Fact]
    public async Task TheSameSprintIdInAnotherWorkspaceGetsItsOwnChannel()
    {
        // The link is scoped by workspace, so the same sprint id under a different DASHBOARD repository is
        // a different sprint. Sharing a channel across them would leak one workspace's discussion.
        using var client = WithToken(ApiFactory.InternalToken);

        var sprintId = Guid.NewGuid();

        var first  = await client.PostAsJsonAsync("/internal/channels/sprint", SprintPayload(Guid.NewGuid(), sprintId));
        var second = await client.PostAsJsonAsync("/internal/channels/sprint", SprintPayload(Guid.NewGuid(), sprintId));

        var firstChannel  = await first.Content.ReadFromJsonAsync<ChannelResponse>();
        var secondChannel = await second.Content.ReadFromJsonAsync<ChannelResponse>();

        secondChannel!.Id.ShouldNotBe(firstChannel!.Id);
    }

    [Fact]
    public async Task AddingAMemberIsIdempotent()
    {
        using var client = WithToken(ApiFactory.InternalToken);

        var created = await client.PostAsJsonAsync(
            "/internal/channels/sprint", SprintPayload(Guid.NewGuid(), Guid.NewGuid()));
        var channel = await created.Content.ReadFromJsonAsync<ChannelResponse>();

        var userId = Guid.NewGuid();
        var route  = $"/internal/channels/{channel!.Id}/members/{userId}";

        (await client.PostAsync(route, null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Sprint member sync replays the full member list, so the second call is the normal case rather
        // than an error.
        (await client.PostAsync(route, null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemovingSomebodyWhoIsNotAMemberStillSucceeds()
    {
        using var client = WithToken(ApiFactory.InternalToken);

        var created = await client.PostAsJsonAsync(
            "/internal/channels/sprint", SprintPayload(Guid.NewGuid(), Guid.NewGuid()));
        var channel = await created.Content.ReadFromJsonAsync<ChannelResponse>();

        // Documented as silently succeeding: DASHBOARD removing a capacity member should not fail because
        // HUB never had them.
        var response = await client.DeleteAsync($"/internal/channels/{channel!.Id}/members/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ArchivingIsIdempotent()
    {
        using var client = WithToken(ApiFactory.InternalToken);

        var created = await client.PostAsJsonAsync(
            "/internal/channels/sprint", SprintPayload(Guid.NewGuid(), Guid.NewGuid()));
        var channel = await created.Content.ReadFromJsonAsync<ChannelResponse>();

        var route = $"/internal/channels/{channel!.Id}/archive";

        (await client.PostAsync(route, null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PostAsync(route, null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ArchivingAChannelThatDoesNotExistIsNotFound()
    {
        using var client = WithToken(ApiFactory.InternalToken);

        var response = await client.PostAsync($"/internal/channels/{Guid.NewGuid()}/archive", null);

        // 404 rather than 204: DASHBOARD passing an id HUB has never seen is a real mismatch worth
        // surfacing, not something to swallow.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>The subset of ChannelDto these tests read.</summary>
    private sealed record ChannelResponse(Guid Id, Guid WorkspaceId, string Name, bool IsPrivate, bool IsArchived);
}
