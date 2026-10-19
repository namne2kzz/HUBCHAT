using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HUB.Chat.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace HUB.Chat.IntegrationTests.Api;

/// <summary>
/// Covers slug-collision handling for the find-or-create channel flows: a sprint channel or linked thread
/// whose name maps to an already-taken (WorkspaceId, Slug) is auto-renamed rather than failing with a 409.
/// </summary>
/// <remarks>
/// This only reproduces on real PostgreSQL: the auto-rename is driven by Npgsql raising
/// <c>UniqueConstraintViolationException</c> on the unique (WorkspaceId, Slug) index, which SQLite (used by
/// the unit suite) does not translate. DASHBOARD calls these with names it does not control for uniqueness,
/// so a 409 here would be an error the caller cannot act on.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
[Trait(TestCategories.Category, TestCategories.RequiresDocker)]
public sealed class SlugCollisionTests(PostgresFixture database) : IAsyncLifetime
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(userId));
        return client;
    }

    private HttpClient Internal()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Token", ApiFactory.InternalToken);
        return client;
    }

    [Fact]
    public async Task ASprintChannelIsAutoRenamedWhenItsSlugIsAlreadyTaken()
    {
        var workspace = Guid.NewGuid();

        // A user creates a channel that happens to own the slug a "Demo" sprint channel would want.
        using var user = As(Guid.NewGuid());
        (await user.PostAsJsonAsync("/api/v1/channels", new
        {
            workspaceId = workspace, name = "sprint-demo", type = 0, topic = (string?)null,
        })).StatusCode.ShouldBe(HttpStatusCode.Created);

        using var svc = Internal();
        var response = await svc.PostAsJsonAsync("/internal/channels/sprint", new
        {
            workspaceId = workspace, sprintId = Guid.NewGuid(), sprintName = "Demo", creatorUserId = Guid.NewGuid(),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var channel = await response.Content.ReadFromJsonAsync<ChannelResponse>();
        channel!.Name.ShouldNotBe("sprint-demo");
        channel.Name.ShouldStartWith("sprint-demo-"); // disambiguated, not 409
    }

    [Fact]
    public async Task ALinkedThreadIsAutoRenamedWhenItsSlugIsAlreadyTaken()
    {
        var workspace = Guid.NewGuid();
        using var user = As(Guid.NewGuid());

        (await user.PostAsJsonAsync("/api/v1/channels", new
        {
            workspaceId = workspace, name = "Fix login", type = 0, topic = (string?)null,
        })).StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await user.PostAsJsonAsync("/api/v1/channels/linked", new
        {
            workspaceId = workspace,
            linkType    = 0,                 // WorkItem
            externalId  = Guid.NewGuid(),
            externalKey = "WI-7",
            title       = "Fix login",
            url         = "http://dash/wi/7",
        });

        // Created under a disambiguated name rather than 409.
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var channel = await response.Content.ReadFromJsonAsync<ChannelResponse>();
        channel!.Name.ShouldNotBe("Fix login");
    }

    [Fact]
    public async Task TheSameSprintStillReturnsItsOwnChannelDespiteASlugClash()
    {
        var workspace = Guid.NewGuid();
        var sprintId  = Guid.NewGuid();
        using var svc = Internal();

        var first  = await (await svc.PostAsJsonAsync("/internal/channels/sprint", new
        { workspaceId = workspace, sprintId, sprintName = "Repeat", creatorUserId = Guid.NewGuid() }))
            .Content.ReadFromJsonAsync<ChannelResponse>();

        var second = await (await svc.PostAsJsonAsync("/internal/channels/sprint", new
        { workspaceId = workspace, sprintId, sprintName = "Repeat", creatorUserId = Guid.NewGuid() }))
            .Content.ReadFromJsonAsync<ChannelResponse>();

        // Idempotency still wins over the rename path: same sprint, same channel.
        second!.Id.ShouldBe(first!.Id);
    }

    private sealed record ChannelResponse(Guid Id, string Name);
}
