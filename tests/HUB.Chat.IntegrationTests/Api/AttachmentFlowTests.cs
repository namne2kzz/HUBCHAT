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
/// Drives attachments (CF-5) end to end: HTTP in, PostgreSQL out, integration events on the way.
/// </summary>
/// <remarks>
/// The unit suite covers the handler on SQLite, but only real PostgreSQL proves the attachment rows (an
/// owned collection on Message) persist and read back through the full HTTP path, that an image-only
/// message survives the empty-body rule, and that the Files/Media panel query translates on Npgsql.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
[Trait(TestCategories.Category, TestCategories.RequiresDocker)]
public sealed class AttachmentFlowTests(PostgresFixture database) : IAsyncLifetime
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
            name        = "Files",
            type,
            topic       = (string?)null,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ChannelResponse>())!.Id;
    }

    /// <summary>An attachment payload as the client sends it (Media file id + metadata snapshot).</summary>
    private static object Attachment(Guid fileId, int kind = 1, string name = "photo.png", string mime = "image/png") => new
    {
        fileId,
        kind,
        name,
        size   = 2048L,
        mime,
        width  = 800,
        height = 600,
    };

    private static object PostWith(string body, object[] attachments) => new
    {
        body,
        format           = 1,
        parentId         = (Guid?)null,
        mentionedUserIds = Array.Empty<Guid>(),
        attachments,
    };

    // ── Persistence round-trip ────────────────────────────────────────────────

    [Fact]
    public async Task AMessageWithAttachmentsPersistsAndReadsBack()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);

        var fileId = Guid.NewGuid();
        var posted = await client.PostAsJsonAsync(
            $"/api/v1/channels/{channelId}/messages", PostWith("look at this", [Attachment(fileId)]));
        posted.StatusCode.ShouldBe(HttpStatusCode.Created);

        var page = await (await client.GetAsync($"/api/v1/channels/{channelId}/messages"))
            .Content.ReadFromJsonAsync<CursorPageResponse>();

        var message = page!.Items.ShouldHaveSingleItem();
        var att = message.Attachments.ShouldHaveSingleItem();
        att.Kind.ShouldBe(1);
        att.Url.ShouldBe(fileId.ToString()); // Chat stores the Media file id as the reference
        att.Name.ShouldBe("photo.png");
        att.Width.ShouldBe(800);
    }

    [Fact]
    public async Task AnImageOnlyMessageWithNoTextIsAccepted()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);

        var posted = await client.PostAsJsonAsync(
            $"/api/v1/channels/{channelId}/messages", PostWith("", [Attachment(Guid.NewGuid())]));

        // The empty-body rule must yield to an attachment-only message.
        posted.StatusCode.ShouldBe(HttpStatusCode.Created);

        var page = await (await client.GetAsync($"/api/v1/channels/{channelId}/messages"))
            .Content.ReadFromJsonAsync<CursorPageResponse>();
        page!.Items.ShouldHaveSingleItem().Attachments.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AnEmptyMessageWithNoAttachmentsIsRejected()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);

        var posted = await client.PostAsJsonAsync(
            $"/api/v1/channels/{channelId}/messages", PostWith("   ", []));

        posted.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TooManyAttachmentsAreRejected()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);

        var eleven = Enumerable.Range(0, 11).Select(_ => Attachment(Guid.NewGuid())).ToArray();
        var posted = await client.PostAsJsonAsync(
            $"/api/v1/channels/{channelId}/messages", PostWith("spam", eleven));

        posted.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostingWithAttachmentsPublishesMessageSentCarryingThem()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);

        Events.Clear();
        await client.PostAsJsonAsync(
            $"/api/v1/channels/{channelId}/messages", PostWith("", [Attachment(Guid.NewGuid())]));

        // Realtime renders the image from the event alone, so it must carry the attachment, not just a preview.
        var sent = Events.OfType<MessageSent>().ShouldHaveSingleItem();
        sent.Attachments.ShouldNotBeNull();
        sent.Attachments!.Count.ShouldBe(1);
        sent.Preview.ShouldContain("📎");
    }

    // ── Files / media panel query (real Npgsql translation) ────────────────────

    [Fact]
    public async Task TheChannelAttachmentsEndpointReturnsNewestFirstAndFiltersByKind()
    {
        var author = Guid.NewGuid();
        using var client = As(author);
        var channelId = await CreateChannelAsync(client);

        await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages",
            PostWith("doc", [Attachment(Guid.NewGuid(), kind: 0, name: "report.pdf", mime: "application/pdf")]));
        await client.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages",
            PostWith("pic", [Attachment(Guid.NewGuid(), kind: 1, name: "late.png")]));

        var all = await client.GetFromJsonAsync<List<ChannelAttachmentResponse>>(
            $"/api/v1/channels/{channelId}/messages/attachments");
        all!.Count.ShouldBe(2);
        all[0].Name.ShouldBe("late.png"); // newest message first

        var images = await client.GetFromJsonAsync<List<ChannelAttachmentResponse>>(
            $"/api/v1/channels/{channelId}/messages/attachments?kind=1");
        images!.ShouldHaveSingleItem().Name.ShouldBe("late.png");

        var files = await client.GetFromJsonAsync<List<ChannelAttachmentResponse>>(
            $"/api/v1/channels/{channelId}/messages/attachments?kind=0");
        files!.ShouldHaveSingleItem().Name.ShouldBe("report.pdf");
    }

    [Fact]
    public async Task AStrangerCannotListAPrivateChannelsAttachments()
    {
        using var owner = As(Guid.NewGuid());
        var channelId = await CreateChannelAsync(owner, type: 1); // private

        using var stranger = As(Guid.NewGuid());
        var response = await stranger.GetAsync($"/api/v1/channels/{channelId}/messages/attachments");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private sealed record ChannelResponse(Guid Id);

    private sealed record AttachmentItem(Guid Id, int Kind, string Url, string Name, long Size, string Mime, int? Width, int? Height);

    private sealed record MessageItem(Guid Id, string Body, IReadOnlyList<AttachmentItem> Attachments);

    private sealed record CursorPageResponse(IReadOnlyList<MessageItem> Items, string? NextCursor);

    private sealed record ChannelAttachmentResponse(
        Guid Id, Guid MessageId, Guid AuthorId, int Kind, string FileId, string Name, long Size, string Mime, int? Width, int? Height, DateTime CreatedAt);
}
