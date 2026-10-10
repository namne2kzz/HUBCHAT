using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Messages.Queries.ListMessagesAfter;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using HUB.TestKit.Builders;
using HUB.TestKit.Db;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Messages;

/// <summary>
/// Covers <c>ListMessagesAfterHandler</c> — the reconnect catch-up read: everything newer than the last
/// message the client has, oldest first, with nothing skipped or repeated.
/// </summary>
/// <remarks>
/// A gap here is silent: the client believes it is caught up and the missed messages only appear after a
/// full reload. Timestamps are pinned with <c>WithCreatedAt</c> so ordering does not depend on the clock.
/// </remarks>
public sealed class ListMessagesAfterHandlerTests
{
    private static readonly DateTime Base = new(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc);
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static async Task<(Channel Channel, List<Message> Messages, Guid Member)> SeedAsync(
        ChatDbContextLease lease, int count, ChannelType type = ChannelType.Public, Func<int, DateTime>? at = null)
    {
        var member  = Guid.NewGuid();
        var channel = Channel.Create(Guid.NewGuid(), "General", type, member);
        lease.Context.Channels.Add(channel);

        var messages = Enumerable.Range(0, count)
            .Select(i => Message.Post(channel.Id, member, $"m{i}", MessageFormat.Plain)
                .WithCreatedAt(at?.Invoke(i) ?? Base.AddMinutes(i)))
            .ToList();
        lease.Context.Messages.AddRange(messages);
        await lease.Context.SaveChangesAsync(Ct);
        return (channel, messages, member);
    }

    [Fact]
    public async Task ReturnsOnlyNewerMessagesOldestFirst()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var (channel, messages, member) = await SeedAsync(lease, 5);

        var result = await new ListMessagesAfterHandler(lease.Context)
            .Handle(new ListMessagesAfterQuery(channel.Id, member, messages[1].Id, 100), Ct);

        // Oldest first, so the client can append in order.
        result.Select(m => m.Body).ShouldBe(["m2", "m3", "m4"]);
    }

    [Fact]
    public async Task ACaughtUpClientGetsNothing()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var (channel, messages, member) = await SeedAsync(lease, 3);

        var result = await new ListMessagesAfterHandler(lease.Context)
            .Handle(new ListMessagesAfterQuery(channel.Id, member, messages[^1].Id, 100), Ct);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task PagingByTheLastIdReachesEveryMessageOnceEvenWithSharedTimestamps()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        // Every message on the same tick — a bulk insert. Seeking on CreatedAt alone would skip them all.
        var (channel, messages, member) = await SeedAsync(lease, 7, at: _ => Base);
        var anchor  = messages.OrderBy(m => m.Id).First();
        var handler = new ListMessagesAfterHandler(lease.Context);

        var seen  = new List<Guid>();
        var after = anchor.Id;
        while (true)
        {
            var page = await handler.Handle(new ListMessagesAfterQuery(channel.Id, member, after, 2), Ct);
            seen.AddRange(page.Select(m => m.Id));
            if (page.Count < 2) break;
            after = page[^1].Id;
        }

        seen.ShouldBeUnique();
        seen.Count.ShouldBe(6); // all but the anchor
    }

    [Fact]
    public async Task RepliesAndDeletedMessagesAreLeftOut()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var (channel, messages, member) = await SeedAsync(lease, 2);

        var reply   = Message.Post(channel.Id, member, "reply", parentId: messages[0].Id).WithCreatedAt(Base.AddMinutes(5));
        var deleted = Message.Post(channel.Id, member, "gone").WithCreatedAt(Base.AddMinutes(6));
        deleted.SoftDelete();
        lease.Context.Messages.AddRange(reply, deleted);
        await lease.Context.SaveChangesAsync(Ct);

        var result = await new ListMessagesAfterHandler(lease.Context)
            .Handle(new ListMessagesAfterQuery(channel.Id, member, messages[0].Id, 100), Ct);

        // Same shape as the main list (top-level, not deleted), so catch-up never shows what a reload would not.
        result.Select(m => m.Body).ShouldBe(["m1"]);
    }

    [Fact]
    public async Task AnAnchorFromAnotherChannelIsNotFound()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var (channel, _, member)  = await SeedAsync(lease, 1);
        var (_, otherMessages, _) = await SeedAsync(lease, 1);

        // Must not seek this channel by another channel's timeline position (or confirm that message exists).
        await Should.ThrowAsync<NotFoundException>(() => new ListMessagesAfterHandler(lease.Context)
            .Handle(new ListMessagesAfterQuery(channel.Id, member, otherMessages[0].Id, 100), Ct));
    }

    [Fact]
    public async Task ANonMemberCannotCatchUpOnAPrivateChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var (channel, messages, _) = await SeedAsync(lease, 2, ChannelType.Private);

        await Should.ThrowAsync<ForbiddenException>(() => new ListMessagesAfterHandler(lease.Context)
            .Handle(new ListMessagesAfterQuery(channel.Id, Guid.NewGuid(), messages[0].Id, 100), Ct));
    }
}
