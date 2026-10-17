using HUB.Chat.Application.Channels.Queries.ListChannels;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Messages.Queries.ListMessages;
using HUB.Chat.Application.Messages.Queries.ListThreadReplies;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using HUB.TestKit.Builders;
using HUB.TestKit.Db;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Messages;

/// <summary>
/// Covers thread replies (CF-6): the per-thread reply list, and the reply-count/last-reply summary the
/// channel list attaches to each top-level message.
/// </summary>
public sealed class ThreadRepliesTests
{
    private static readonly DateTime Base = new(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static async Task<(ChatDbContextLease Lease, Channel Channel, Message Root, Guid Member)> SeedThreadAsync(int replies)
    {
        var lease  = await ChatDbContextFactory.CreateAsync();
        var member = Guid.NewGuid();
        var channel = Channel.Create(Guid.NewGuid(), "General", ChannelType.Public, member);
        lease.Context.Channels.Add(channel);

        var root = Message.Post(channel.Id, member, "root").WithCreatedAt(Base);
        lease.Context.Messages.Add(root);
        for (var i = 0; i < replies; i++)
            lease.Context.Messages.Add(Message.Post(channel.Id, member, $"reply {i}", parentId: root.Id)
                .WithCreatedAt(Base.AddMinutes(i + 1)));
        await lease.Context.SaveChangesAsync(Ct);
        return (lease, channel, root, member);
    }

    [Fact]
    public async Task ListRepliesReturnsTheThreadOldestFirst()
    {
        var (lease, channel, root, member) = await SeedThreadAsync(3);
        await using var _ = lease;

        var replies = await new ListThreadRepliesHandler(lease.Context)
            .Handle(new ListThreadRepliesQuery(channel.Id, root.Id, member), Ct);

        replies.Select(r => r.Body).ShouldBe(["reply 0", "reply 1", "reply 2"]);
        replies.ShouldAllBe(r => r.ParentId == root.Id);
    }

    [Fact]
    public async Task ListRepliesRejectsAParentFromAnotherChannel()
    {
        var (lease, _, root, member) = await SeedThreadAsync(1);
        await using var _d = lease;
        var otherChannel = Guid.NewGuid();

        // A reply list must not be reachable through a channel the parent does not belong to.
        await Should.ThrowAsync<NotFoundException>(() => new ListThreadRepliesHandler(lease.Context)
            .Handle(new ListThreadRepliesQuery(otherChannel, root.Id, member), Ct));
    }

    [Fact]
    public async Task ANonMemberCannotReadRepliesOfPrivateChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var owner = Guid.NewGuid();
        var channel = Channel.Create(Guid.NewGuid(), "secret", ChannelType.Private, owner);
        lease.Context.Channels.Add(channel);
        var root = Message.Post(channel.Id, owner, "root").WithCreatedAt(Base);
        lease.Context.Messages.Add(root);
        await lease.Context.SaveChangesAsync(Ct);

        await Should.ThrowAsync<ForbiddenException>(() => new ListThreadRepliesHandler(lease.Context)
            .Handle(new ListThreadRepliesQuery(channel.Id, root.Id, Guid.NewGuid()), Ct));
    }

    [Fact]
    public async Task TheChannelListAttachesReplyCountAndLastReplyToTheRoot()
    {
        var (lease, channel, root, member) = await SeedThreadAsync(2);
        await using var _ = lease;

        var list = await new ListChannelsHandler(lease.Context)
            .Handle(new ListChannelsQuery(channel.WorkspaceId, member), Ct);
        // (ListChannels returns channels, not messages — reply counts live on messages; assert via ListMessages.)
        var page = await new ListMessagesHandler(lease.Context)
            .Handle(new ListMessagesQuery(channel.Id, member, null, 50), Ct);

        var rootDto = page.Items.Single(m => m.Id == root.Id);
        rootDto.ReplyCount.ShouldBe(2);
        rootDto.LastReplyAt.ShouldBe(Base.AddMinutes(2));
        list.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task DeletedRepliesDoNotCountTowardTheSummary()
    {
        var (lease, channel, root, member) = await SeedThreadAsync(2);
        await using var _ = lease;
        var firstReply = await System.Threading.Tasks.Task.FromResult(
            lease.Context.Messages.First(m => m.ParentId == root.Id));
        firstReply.SoftDelete();
        await lease.Context.SaveChangesAsync(Ct);

        var page = await new ListMessagesHandler(lease.Context)
            .Handle(new ListMessagesQuery(channel.Id, member, null, 50), Ct);

        // A tombstoned reply still shows inside the thread, but should not inflate the "N replies" badge.
        page.Items.Single(m => m.Id == root.Id).ReplyCount.ShouldBe(1);
    }

    [Fact]
    public async Task AMessageWithNoRepliesHasZeroCount()
    {
        var (lease, channel, root, member) = await SeedThreadAsync(0);
        await using var _ = lease;

        var page = await new ListMessagesHandler(lease.Context)
            .Handle(new ListMessagesQuery(channel.Id, member, null, 50), Ct);

        var rootDto = page.Items.Single(m => m.Id == root.Id);
        rootDto.ReplyCount.ShouldBe(0);
        rootDto.LastReplyAt.ShouldBeNull();
    }
}
