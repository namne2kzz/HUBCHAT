using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HUB.Chat.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace HUB.Chat.IntegrationTests.Api;

/// <summary>
/// Covers the authentication boundary on the Chat API: which requests are refused before reaching a
/// handler, and that token validation is genuinely switched on.
/// </summary>
/// <remarks>
/// The token-rejection cases matter more than they look. A test suite that only ever sends valid tokens
/// cannot tell a working validator from one that accepts anything well-formed — the requests all succeed
/// either way. So the wrong key, the wrong issuer and an expired token are each asserted explicitly.
///
/// HUB does not issue tokens; DASHBOARD does, and HUB validates them against a shared HMAC secret. There
/// is no login endpoint to obtain a real one from, so <see cref="TestTokens"/> signs its own with the
/// secret the factory configures.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
[Trait(TestCategories.Category, TestCategories.RequiresDocker)]
public sealed class AuthenticationTests(PostgresFixture database) : IAsyncLifetime
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

    private HttpClient Anonymous() => _factory.CreateClient();

    private HttpClient As(Guid userId, string? token = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token ?? TestTokens.For(userId));
        return client;
    }

    // ── No credentials ──────────────────────────────────────────────────────

    public static TheoryData<string, string> ProtectedRoutes => new()
    {
        { "GET",  "/api/v1/channels?workspaceId=00000000-0000-0000-0000-000000000001" },
        { "POST", "/api/v1/channels" },
        { "GET",  "/api/v1/channels/00000000-0000-0000-0000-000000000001" },
        { "GET",  "/api/v1/channels/00000000-0000-0000-0000-000000000001/members" },
        { "GET",  "/api/v1/channels/00000000-0000-0000-0000-000000000001/messages" },
        { "POST", "/api/v1/channels/00000000-0000-0000-0000-000000000001/messages" },
        { "POST", "/api/v1/messages/00000000-0000-0000-0000-000000000001/reactions" },
        { "POST", "/api/v1/channels/00000000-0000-0000-0000-000000000001/members" },
        { "POST", "/api/v1/channels/00000000-0000-0000-0000-000000000001/read" },
    };

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task EveryProtectedRouteRefusesAnAnonymousRequest(string method, string route)
    {
        using var client = Anonymous();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), route));

        // 401 specifically, not "some error": the distinction is what tells a client to refresh its token
        // rather than to give up or to fix its payload.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Bad credentials ─────────────────────────────────────────────────────

    [Fact]
    public async Task ATokenSignedWithTheWrongKeyIsRefused()
    {
        var user = Guid.NewGuid();
        using var client = As(user, TestTokens.SignedWithTheWrongKey(user));

        var response = await client.GetAsync($"/api/v1/channels?workspaceId={Guid.NewGuid()}");

        // The whole SSO model rests on this: HUB trusts a token only because DASHBOARD's secret signed it.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ATokenFromAnotherIssuerIsRefused()
    {
        var user = Guid.NewGuid();
        using var client = As(user, TestTokens.FromTheWrongIssuer(user));

        var response = await client.GetAsync($"/api/v1/channels?workspaceId={Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnExpiredTokenIsRefused()
    {
        var user = Guid.NewGuid();
        using var client = As(user, TestTokens.For(user, expires: DateTime.UtcNow.AddMinutes(-10)));

        var response = await client.GetAsync($"/api/v1/channels?workspaceId={Guid.NewGuid()}");

        // ClockSkew is configured to zero, so an expired token is expired with no grace period.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AMalformedBearerValueIsRefused()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        var response = await client.GetAsync($"/api/v1/channels?workspaceId={Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Good credentials ────────────────────────────────────────────────────

    [Fact]
    public async Task AValidTokenReachesTheHandler()
    {
        using var client = As(Guid.NewGuid());

        var response = await client.GetAsync($"/api/v1/channels?workspaceId={Guid.NewGuid()}");

        // A workspace with no channels is an empty list, not an error — which also shows the request went
        // all the way through MediatR and EF rather than stopping at the auth filter.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<object>>()).ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingCredentialsGive401WhileAnAuthenticatedRefusalGives403()
    {
        // Two calls to the same endpoint, to pin the distinction that DASHBOARD had to retrofit across
        // 84 call sites: unauthenticated is 401, authenticated-but-not-allowed is 403. Conflating them
        // sends the client off to refresh a token that was never the problem.
        var owner    = Guid.NewGuid();
        var outsider = Guid.NewGuid();

        using var ownerClient = As(owner);
        var created = await ownerClient.PostAsJsonAsync("/api/v1/channels", new
        {
            workspaceId = Guid.NewGuid(),
            name        = "Private Room",
            type        = 1,            // Private — numeric because the API registers no JsonStringEnumConverter
            topic       = (string?)null,
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var channel   = await created.Content.ReadFromJsonAsync<ChannelResponse>();
        var channelId = channel!.Id;

        using var anonymous = Anonymous();
        (await anonymous.GetAsync($"/api/v1/channels/{channelId}"))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var outsiderClient = As(outsider);
        (await outsiderClient.GetAsync($"/api/v1/channels/{channelId}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheUidClaimIsWhatIdentifiesTheCaller()
    {
        // The creator becomes owner of the channel they create, and ChannelDto reports the acting user's
        // own role. So a response saying "you are the Owner" is the API confirming it resolved the caller
        // from the uid claim — if it had read the wrong claim, or none, the id would not match a
        // membership row and MyRole would come back null.
        var user = Guid.NewGuid();
        using var client = As(user);

        var created = await client.PostAsJsonAsync("/api/v1/channels", new
        {
            workspaceId = Guid.NewGuid(),
            name        = "Mine",
            type        = 0,
            topic       = (string?)null,
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var channel = await created.Content.ReadFromJsonAsync<ChannelResponse>();
        channel!.IsMember.ShouldBeTrue();

        // Fetching the same channel with the same token reports Owner, which is the proof that the uid
        // claim identified the caller: the id resolved to a membership row with the Owner role.
        using var sameUser = As(user);
        var refetched = await sameUser.GetAsync($"/api/v1/channels/{channel.Id}");
        refetched.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await refetched.Content.ReadFromJsonAsync<ChannelResponse>())!
            .MyRole.ShouldBe(2, "the creator is the channel Owner (ChannelMemberRole.Owner)");

        // A different caller on the same public channel is readable but not a member, which rules out
        // MyRole simply being populated for everybody.
        using var other = As(Guid.NewGuid());
        var seenByOther = await (await other.GetAsync($"/api/v1/channels/{channel.Id}"))
            .Content.ReadFromJsonAsync<ChannelResponse>();

        seenByOther!.IsMember.ShouldBeFalse();
        seenByOther.MyRole.ShouldBeNull();
    }

    [Fact]
    public async Task TheCreateResponseOmitsMyRoleEvenThoughTheCreatorIsOwner()
    {
        // BUG-002, asserted as it currently behaves rather than as it should.
        //
        // ChannelMappings.ToDto() takes myRole as an optional parameter defaulting to null, and
        // CreateChannelHandler calls ToDto() without it — so the POST response says IsMember: true and
        // MyRole: null, while a GET of the same channel with the same token says MyRole: 2. Three other
        // handlers have the same shape: UpdateChannel and both branches of OpenLinkedThread.
        //
        // A client that reads MyRole to decide whether to show owner controls (rename, archive, transfer)
        // shows none of them until it refetches. When somebody threads myRole through those four calls,
        // this test fails and points at the bug entry to close.
        var user = Guid.NewGuid();
        using var client = As(user);

        var created = await client.PostAsJsonAsync("/api/v1/channels", new
        {
            workspaceId = Guid.NewGuid(),
            name        = "Role Gap",
            type        = 0,
            topic       = (string?)null,
        });

        var channel = await created.Content.ReadFromJsonAsync<ChannelResponse>();

        channel!.IsMember.ShouldBeTrue("ToDto hardcodes IsMember: true");
        channel.MyRole.ShouldBeNull("known gap — see BUG-002 in .claude/self-test/bugs.md");
    }

    /// <summary>The subset of ChannelDto these tests read.</summary>
    /// <remarks><c>MyRole</c> is read as an int because the API serialises enums numerically.</remarks>
    private sealed record ChannelResponse(Guid Id, Guid WorkspaceId, bool IsMember, int? MyRole);
}
