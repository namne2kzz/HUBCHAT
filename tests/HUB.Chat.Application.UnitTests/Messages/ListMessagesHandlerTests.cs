using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Models;
using HUB.Chat.Application.Messages.Queries.ListMessages;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using HUB.Chat.Infrastructure.Persistence;
using HUB.TestKit.Builders;
using HUB.TestKit.Db;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Messages;

/// <summary>
/// Covers <c>ListMessagesHandler</c>: the read-access rule, the limit clamp, what is filtered out of a
/// page, and the keyset cursor that walks backwards through history.
/// </summary>
/// <remarks>
/// Pagination is worth this much attention because its failures are quiet. A page that drops a message
/// or repeats one does not raise anything; it just shows the wrong history, and only to whoever
/// scrolled far enough. Timestamps here are set explicitly through
/// <see cref="EntityTimestamps.WithCreatedAt"/> rather than left to <c>UtcNow</c>, since rows created
/// in a loop otherwise share a timestamp and make the ordering ambiguous.
///
/// These run on SQLite. Ordering, filtering and the cursor arithmetic are provider-independent, so they
/// belong here; whether the <c>(channel_id, created_at)</c> index is actually used is a PostgreSQL
/// question and lives in HUB.Chat.IntegrationTests.
/// </remarks>
public sealed class ListMessagesHandlerTests
{
    private static readonly DateTime Base = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Adds a channel plus <paramref name="count"/> messages one minute apart, oldest first.</summary>
    private static async Task<Channel> SeedAsync(ChatDbContext context, Guid member, int count, ChannelType type = ChannelType.Public)
    {
        var channel = Channel.Create(Guid.NewGuid(), "General", type, member);
        context.Channels.Add(channel);

        for (var i = 0; i < count; i++)
        {
            context.Messages.Add(Message
                .Post(channel.Id, member, $"message {i}", MessageFormat.Plain)
                .WithCreatedAt(Base.AddMinutes(i)));
        }

        await context.SaveChangesAsync(CancellationToken.None);
        return channel;
    }

    // ── Read access ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ListingAMissingChannelIsNotFound()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var handler = new ListMessagesHandler(lease.Context);

        await Should.ThrowAsync<NotFoundException>(() => handler.Handle(
            new ListMessagesQuery(Guid.NewGuid(), Guid.NewGuid(), null, 20), CancellationToken.None));
    }

    [Fact]
    public async Task APublicChannelIsReadableByANonMember()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedAsync(lease.Context, Guid.NewGuid(), 3);

        var handler = new ListMessagesHandler(lease.Context);

        // Reading is deliberately looser than posting: a public channel is browsable by anybody in the
        // workspace, while posting to it still requires membership.
        var page = await handler.Handle(
            new ListMessagesQuery(channel.Id, Guid.NewGuid(), null, 20), CancellationToken.None);

        page.Items.Count.ShouldBe(3);
    }

    [Fact]
    public async Task APrivateChannelIsForbiddenToANonMember()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = await SeedAsync(lease.Context, Guid.NewGuid(), 3, ChannelType.Private);

        var handler = new ListMessagesHandler(lease.Context);

        await Should.ThrowAsync<ForbiddenException>(() => handler.Handle(
            new ListMessagesQuery(channel.Id, Guid.NewGuid(), null, 20), CancellationToken.None));
    }

    [Fact]
    public async Task APrivateChannelIsReadableByItsMember()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var member  = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, member, 3, ChannelType.Private);

        var handler = new ListMessagesHandler(lease.Context);

        var page = await handler.Handle(
            new ListMessagesQuery(channel.Id, member, null, 20), CancellationToken.None);

        page.Items.Count.ShouldBe(3);
    }

    // ── Limit clamp ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-500)]
    public async Task ANonPositiveLimitIsClampedToOne(int limit)
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var member  = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, member, 5);

        var handler = new ListMessagesHandler(lease.Context);

        // A zero or negative limit must not come back as an empty page: the client would read that as
        // "no history" and stop paging.
        var page = await handler.Handle(
            new ListMessagesQuery(channel.Id, member, null, limit), CancellationToken.None);

        page.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AnOversizedLimitIsClampedToOneHundred()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var member  = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, member, 105);

        var handler = new ListMessagesHandler(lease.Context);

        // The cap is what stops a client asking for a channel's whole history in one request.
        var page = await handler.Handle(
            new ListMessagesQuery(channel.Id, member, null, 5000), CancellationToken.None);

        page.Items.Count.ShouldBe(100);
    }

    // ── What a page excludes ────────────────────────────────────────────────

    [Fact]
    public async Task ThreadRepliesAreNotListedAlongsideTopLevelMessages()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var member  = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, member, 1);

        var parent = await lease.Context.Messages.FirstAsync(CancellationToken.None);
        lease.Context.Messages.Add(Message
            .Post(channel.Id, member, "a reply", MessageFormat.Plain, parentId: parent.Id)
            .WithCreatedAt(Base.AddMinutes(10)));
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new ListMessagesHandler(lease.Context);
        var page    = await handler.Handle(
            new ListMessagesQuery(channel.Id, member, null, 20), CancellationToken.None);

        // Replies belong to their thread, not to the channel timeline; listing them twice would show
        // every reply both inline and nested.
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(parent.Id);
    }

    [Fact]
    public async Task SoftDeletedMessagesAreExcluded()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var member  = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, member, 3);

        var victim = await lease.Context.Messages.OrderBy(m => m.CreatedAt).FirstAsync(CancellationToken.None);
        victim.SoftDelete();
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new ListMessagesHandler(lease.Context);
        var page    = await handler.Handle(
            new ListMessagesQuery(channel.Id, member, null, 20), CancellationToken.None);

        page.Items.Count.ShouldBe(2);
        page.Items.ShouldNotContain(m => m.Id == victim.Id);
    }

    [Fact]
    public async Task MessagesFromOtherChannelsAreExcluded()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var member = Guid.NewGuid();
        var mine   = await SeedAsync(lease.Context, member, 2);
        await SeedAsync(lease.Context, member, 7);

        var handler = new ListMessagesHandler(lease.Context);
        var page    = await handler.Handle(
            new ListMessagesQuery(mine.Id, member, null, 20), CancellationToken.None);

        page.Items.Count.ShouldBe(2);
        page.Items.ShouldAllBe(m => m.ChannelId == mine.Id);
    }

    // ── Ordering and the cursor ─────────────────────────────────────────────

    [Fact]
    public async Task APageIsOrderedNewestFirst()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var member  = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, member, 5);

        var handler = new ListMessagesHandler(lease.Context);
        var page    = await handler.Handle(
            new ListMessagesQuery(channel.Id, member, null, 20), CancellationToken.None);

        page.Items.Select(m => m.CreatedAt).ShouldBeInOrder(SortDirection.Descending);
        page.Items[0].Body.ShouldBe("message 4");
    }

    [Fact]
    public async Task AFullPageCarriesACursorAndAPartialOneDoesNot()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var member  = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, member, 5);

        var handler = new ListMessagesHandler(lease.Context);

        // The handler cannot know whether more rows exist without asking for another, so "page was
        // full" stands in for "there may be more". A client that trusts it gets one empty page at the
        // end, which is the accepted cost of not running a count.
        var full = await handler.Handle(
            new ListMessagesQuery(channel.Id, member, null, 5), CancellationToken.None);
        full.NextCursor.ShouldNotBeNull();

        var partial = await handler.Handle(
            new ListMessagesQuery(channel.Id, member, null, 20), CancellationToken.None);
        partial.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task TheCursorServesTheNextOlderPageWithoutOverlap()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var member  = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, member, 6);

        var handler = new ListMessagesHandler(lease.Context);

        var first = await handler.Handle(
            new ListMessagesQuery(channel.Id, member, null, 3), CancellationToken.None);
        var second = await handler.Handle(
            new ListMessagesQuery(channel.Id, member, first.NextCursor, 3), CancellationToken.None);

        first.Items.Select(m => m.Body).ShouldBe(["message 5", "message 4", "message 3"]);
        second.Items.Select(m => m.Body).ShouldBe(["message 2", "message 1", "message 0"]);

        // The two halves must not share a message. An off-by-one in the boundary comparison (`<=`
        // instead of `<`) would repeat the boundary row on every page.
        first.Items.Select(m => m.Id).Intersect(second.Items.Select(m => m.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task PagingAllTheWayThroughVisitsEveryMessageExactlyOnce()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var member  = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, member, 10);

        var handler = new ListMessagesHandler(lease.Context);

        var seen    = new List<Guid>();
        string? cursor = null;

        // Walk the whole history the way a scrolling client does, rather than checking one hop. This is
        // the shape that catches a cursor which drifts, stalls, or loses a row at a page boundary.
        for (var page = 0; page < 10; page++)
        {
            var result = await handler.Handle(
                new ListMessagesQuery(channel.Id, member, cursor, 3), CancellationToken.None);

            seen.AddRange(result.Items.Select(m => m.Id));

            if (result.NextCursor is null) break;
            cursor = result.NextCursor;
        }

        seen.Count.ShouldBe(10);
        seen.Distinct().Count().ShouldBe(10, "a message must not appear on two pages");
    }

    [Fact]
    public async Task AnUnparseableCursorFallsBackToTheFirstPage()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var member  = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, member, 4);

        var handler = new ListMessagesHandler(lease.Context);

        // A stale or hand-edited cursor should degrade to "start again", not fail the request. Before
        // BUG-001 was fixed, one class of malformed cursor threw out of TryDecode and became a 500.
        var page = await handler.Handle(
            new ListMessagesQuery(channel.Id, member, "!!!not-a-cursor!!!", 20), CancellationToken.None);

        page.Items.Count.ShouldBe(4);
    }

    [Fact]
    public async Task ACursorOutsideDateTimeRangeIsIgnoredRatherThanThrowing()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var member  = Guid.NewGuid();
        var channel = await SeedAsync(lease.Context, member, 4);

        var handler = new ListMessagesHandler(lease.Context);

        // Regression for BUG-001, asserted at the handler rather than only on MessageCursor, because
        // this is the path a request actually takes.
        var hostile = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{long.MaxValue}:{Guid.NewGuid()}"));

        var page = await handler.Handle(
            new ListMessagesQuery(channel.Id, member, hostile, 20), CancellationToken.None);

        page.Items.Count.ShouldBe(4);
    }

    [Fact]
    public async Task MessagesSharingATimestampAreAllReturnedExactlyOnce()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var member  = Guid.NewGuid();
        var channel = Channel.Create(Guid.NewGuid(), "General", ChannelType.Public, member);
        lease.Context.Channels.Add(channel);

        // Four messages on the same tick — a plausible outcome of a bulk insert or an import.
        var shared = Base;
        for (var i = 0; i < 4; i++)
        {
            lease.Context.Messages.Add(Message
                .Post(channel.Id, member, $"tied {i}", MessageFormat.Plain)
                .WithCreatedAt(shared));
        }
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new ListMessagesHandler(lease.Context);

        var seen    = new List<Guid>();
        string? cursor = null;

        for (var page = 0; page < 5; page++)
        {
            var result = await handler.Handle(
                new ListMessagesQuery(channel.Id, member, cursor, 2), CancellationToken.None);

            seen.AddRange(result.Items.Select(m => m.Id));

            if (result.NextCursor is null) break;
            cursor = result.NextCursor;
        }

        // The case the Id tie-breaker exists for. Comparing CreatedAt alone skips every message sharing
        // the boundary timestamp rather than just the boundary row, so these four would come back as
        // two and the rest would be unreachable on any page. Silent data loss: nothing errors, the
        // client simply never sees them.
        seen.Count.ShouldBe(4, "no message may be skipped because it shares a timestamp");
        seen.Distinct().Count().ShouldBe(4, "and none may be returned twice");
    }

    [Fact]
    public async Task PagingIsStableWhenEveryMessageSharesATimestamp()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var member  = Guid.NewGuid();
        var channel = Channel.Create(Guid.NewGuid(), "General", ChannelType.Public, member);
        lease.Context.Channels.Add(channel);

        // A whole page worth on one tick, so the boundary itself is a tie on every hop.
        for (var i = 0; i < 9; i++)
        {
            lease.Context.Messages.Add(Message
                .Post(channel.Id, member, $"tied {i}", MessageFormat.Plain)
                .WithCreatedAt(Base));
        }
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new ListMessagesHandler(lease.Context);

        var seen    = new List<Guid>();
        string? cursor = null;

        for (var page = 0; page < 10; page++)
        {
            var result = await handler.Handle(
                new ListMessagesQuery(channel.Id, member, cursor, 3), CancellationToken.None);

            seen.AddRange(result.Items.Select(m => m.Id));

            if (result.NextCursor is null) break;
            cursor = result.NextCursor;
        }

        // With Id as the sole discriminator the walk has to stay strictly monotonic: a `<=` instead of
        // `<` would repeat the boundary row forever and never terminate.
        seen.Count.ShouldBe(9);
        seen.Distinct().Count().ShouldBe(9);
    }
}
