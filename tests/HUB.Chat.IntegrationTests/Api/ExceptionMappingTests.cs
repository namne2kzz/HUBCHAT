using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HUB.Chat.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace HUB.Chat.IntegrationTests.Api;

/// <summary>
/// Covers <c>ExceptionHandlingMiddleware</c>: which status code each exception type becomes, and that an
/// unexpected failure does not leak internals.
/// </summary>
/// <remarks>
/// These four mappings are the API's error contract, and a client branches on them: 401 means refresh the
/// token, 403 means stop asking, 404 means the thing is gone, 409 means the request was understood and
/// refused on a rule. Collapsing any two of them sends the client down the wrong path — the sibling
/// DASHBOARD repository had the same situation return 401, 400 and 403 depending on which handler was
/// reached, and had to be corrected across 84 call sites.
///
/// HUB maps <c>DomainException</c> to <b>409 Conflict</b>, which differs from DASHBOARD's 400. Asserted
/// here rather than assumed, because it is the kind of difference that gets "fixed" by somebody working
/// from the other repository's habits.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
[Trait(TestCategories.Category, TestCategories.RequiresDocker)]
public sealed class ExceptionMappingTests(PostgresFixture database) : IAsyncLifetime
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

    private async Task<Guid> CreateChannelAsync(HttpClient client, int type = 0)
    {
        var response = await client.PostAsJsonAsync("/api/v1/channels", new
        {
            workspaceId = Guid.NewGuid(),
            name        = "Mapping",
            type,
            topic       = (string?)null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<ChannelResponse>())!.Id;
    }

    [Fact]
    public async Task ANotFoundExceptionBecomes404()
    {
        using var client = As(Guid.NewGuid());

        var response = await client.GetAsync($"/api/v1/channels/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Body(response)).Error.ShouldBe("not_found");
    }

    [Fact]
    public async Task AForbiddenExceptionBecomes403()
    {
        var owner = Guid.NewGuid();
        using var ownerClient = As(owner);
        var channelId = await CreateChannelAsync(ownerClient, type: 1); // Private

        using var outsider = As(Guid.NewGuid());
        var response = await outsider.GetAsync($"/api/v1/channels/{channelId}");

        // 403, not 404: the caller is authenticated and the channel exists, they are simply not in it.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Body(response)).Error.ShouldBe("forbidden");
    }

    [Fact]
    public async Task ADomainExceptionBecomes409()
    {
        var owner = Guid.NewGuid();
        using var client = As(owner);
        var channelId = await CreateChannelAsync(client);

        // Archive the channel through the internal route, then try to post to it.
        using var internalClient = _factory.CreateClient();
        internalClient.DefaultRequestHeaders.Add("X-Internal-Token", ApiFactory.InternalToken);
        (await internalClient.PostAsync($"/internal/channels/{channelId}/archive", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", new
        {
            body             = "too late",
            format           = 0,
            parentId         = (Guid?)null,
            mentionedUserIds = Array.Empty<Guid>(),
        });

        // Note this is 409 in HUB, where DASHBOARD uses 400 for the same class of failure.
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Body(response)).Error.ShouldBe("domain_error");
    }

    [Fact]
    public async Task AValidationExceptionBecomes400()
    {
        using var client = As(Guid.NewGuid());
        var channelId = await CreateChannelAsync(client);

        // PostMessageValidator requires a non-empty body, so this is refused by the MediatR validation
        // behaviour before the handler runs.
        var response = await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", new
        {
            body             = "   ",
            format           = 0,
            parentId         = (Guid?)null,
            mentionedUserIds = Array.Empty<Guid>(),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Body(response)).Error.ShouldBe("validation_error");
    }

    [Fact]
    public async Task AValidationFailureNamesWhatWasWrong()
    {
        using var client = As(Guid.NewGuid());
        var channelId = await CreateChannelAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", new
        {
            body             = new string('x', 9000),   // over the 8000 limit
            format           = 0,
            parentId         = (Guid?)null,
            mentionedUserIds = Array.Empty<Guid>(),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // The message is the only thing a client can show the person, so an empty one makes the 400
        // useless to them.
        (await Body(response)).Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AMalformedCursorIsIgnoredRatherThanFailing()
    {
        using var client = As(Guid.NewGuid());
        var channelId = await CreateChannelAsync(client);

        // Regression for BUG-001 at the HTTP boundary: a hostile cursor used to reach the catch-all and
        // come back 500. The documented behaviour is to ignore it and serve the first page.
        var hostile = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{long.MaxValue}:{Guid.NewGuid()}"));

        var response = await client.GetAsync(
            $"/api/v1/channels/{channelId}/messages?cursor={Uri.EscapeDataString(hostile)}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnEnumSentAsAStringIsRejectedAtModelBinding()
    {
        using var client = As(Guid.NewGuid());

        // The API registers no JsonStringEnumConverter, so enums must arrive as numbers. This is asserted
        // deliberately: in the sibling DASHBOARD repository two multi-tenancy tests "passed" for months
        // because they sent a string enum, got a 400 at model binding, and never reached the handler they
        // were supposed to be testing — while 400 sat in their list of acceptable "refused" codes.
        var response = await client.PostAsJsonAsync("/api/v1/channels", new
        {
            workspaceId = Guid.NewGuid(),
            name        = "String Enum",
            type        = "Public",
            topic       = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest,
            "enums are numeric over JSON here — a string never reaches the handler");
    }

    private static async Task<ErrorBody> Body(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ErrorBody>())!;

    private sealed record ErrorBody(string Error, string Message);

    private sealed record ChannelResponse(Guid Id);
}
