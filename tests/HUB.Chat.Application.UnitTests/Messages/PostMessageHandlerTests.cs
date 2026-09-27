using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Messages.Commands.PostMessage;
using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using HUB.Shared.Contracts.Events;
using HUB.TestKit.Db;
using HUB.TestKit.Fakes;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Messages;

/// <summary>
/// Covers <c>PostMessageHandler</c>: the membership and writability gates, and the integration events
/// it puts on the outbox.
/// </summary>
/// <remarks>
/// The event assertions carry most of the weight here. Realtime fan-out and mention notifications are
/// separate services that learn about a message only through <c>MessageSent</c> and
/// <c>UserMentioned</c>; a handler that saves the row but publishes the wrong number of events leaves
/// the message visible on refresh and silently missing from every live client. Nothing in the domain
/// tests can see that.
/// </remarks>
public sealed class PostMessageHandlerTests
{
    private static Channel ChannelWith(Guid member, bool archived = false)
    {
        var channel = Channel.Create(Guid.NewGuid(), "General", ChannelType.Public, member);
        if (archived) channel.Archive();
        return channel;
    }

    [Fact]
    public async Task PostingToAMissingChannelIsNotFound()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var handler = new PostMessageHandler(lease.Context, new RecordingEventPublisher());

        var command = new PostMessageCommand(
            Guid.NewGuid(), "hello", MessageFormat.Plain, null, [], Guid.NewGuid());

        await Should.ThrowAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task ANonMemberIsForbidden()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = ChannelWith(Guid.NewGuid());
        lease.Context.Channels.Add(channel);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new PostMessageHandler(lease.Context, new RecordingEventPublisher());

        // A public channel is readable by anyone but writable only by members — posting is the
        // stricter of the two, so a stranger has to be refused here even though they may read.
        var command = new PostMessageCommand(
            channel.Id, "hello", MessageFormat.Plain, null, [], Guid.NewGuid());

        await Should.ThrowAsync<ForbiddenException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task AnArchivedChannelRejectsNewMessages()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author  = Guid.NewGuid();
        var channel = ChannelWith(author, archived: true);
        lease.Context.Channels.Add(channel);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new PostMessageHandler(lease.Context, new RecordingEventPublisher());
        var command = new PostMessageCommand(channel.Id, "hello", MessageFormat.Plain, null, [], author);

        // Membership is not enough: archiving closes the channel to its own members too.
        await Should.ThrowAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task ARefusedPostPersistsNothing()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var channel = ChannelWith(Guid.NewGuid());
        lease.Context.Channels.Add(channel);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var events  = new RecordingEventPublisher();
        var handler = new PostMessageHandler(lease.Context, events);

        await Should.ThrowAsync<ForbiddenException>(() => handler.Handle(
            new PostMessageCommand(channel.Id, "hello", MessageFormat.Plain, null, [], Guid.NewGuid()),
            CancellationToken.None));

        // Asserted from a separate context: the point is that nothing reached the database, which the
        // original change tracker could not tell us.
        await using var verify = lease.NewContext();
        (await verify.Messages.CountAsync()).ShouldBe(0);
        events.Published.ShouldBeEmpty("a refused post must not announce itself on the bus");
    }

    [Fact]
    public async Task AMemberCanPostAndTheMessageIsPersisted()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author  = Guid.NewGuid();
        var channel = ChannelWith(author);
        lease.Context.Channels.Add(channel);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new PostMessageHandler(lease.Context, new RecordingEventPublisher());
        var result  = await handler.Handle(
            new PostMessageCommand(channel.Id, "hello", MessageFormat.Markdown, null, [], author),
            CancellationToken.None);

        result.Body.ShouldBe("hello");

        await using var verify = lease.NewContext();
        var stored = await verify.Messages.AsNoTracking().SingleAsync();
        stored.Id.ShouldBe(result.Id);
        stored.AuthorId.ShouldBe(author);
        stored.ChannelId.ShouldBe(channel.Id);
    }

    [Fact]
    public async Task PostingPublishesExactlyOneMessageSent()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author  = Guid.NewGuid();
        var channel = ChannelWith(author);
        lease.Context.Channels.Add(channel);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var events  = new RecordingEventPublisher();
        var handler = new PostMessageHandler(lease.Context, events);

        var result = await handler.Handle(
            new PostMessageCommand(channel.Id, "hello", MessageFormat.Plain, null, [], author),
            CancellationToken.None);

        var sent = events.Single<MessageSent>();
        sent.MessageId.ShouldBe(result.Id);
        sent.ChannelId.ShouldBe(channel.Id);
        sent.AuthorId.ShouldBe(author);
    }

    [Fact]
    public async Task EachMentionGetsItsOwnUserMentionedEvent()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author  = Guid.NewGuid();
        var channel = ChannelWith(author);
        lease.Context.Channels.Add(channel);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var events  = new RecordingEventPublisher();
        var handler = new PostMessageHandler(lease.Context, events);

        Guid[] mentioned = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

        await handler.Handle(
            new PostMessageCommand(channel.Id, "hi all", MessageFormat.Plain, null, mentioned, author),
            CancellationToken.None);

        // One notification per mentioned person. Publishing a single event for the whole list would
        // leave two of the three never notified.
        var raised = events.OfType<UserMentioned>();
        raised.Count.ShouldBe(3);
        raised.Select(e => e.MentionedUserId).ShouldBe(mentioned, ignoreOrder: true);
    }

    [Fact]
    public async Task ADuplicatedMentionIsOnlyNotifiedOnce()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author  = Guid.NewGuid();
        var channel = ChannelWith(author);
        lease.Context.Channels.Add(channel);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var events  = new RecordingEventPublisher();
        var handler = new PostMessageHandler(lease.Context, events);

        var target = Guid.NewGuid();

        await handler.Handle(
            new PostMessageCommand(channel.Id, "@you @you", MessageFormat.Plain, null, [target, target], author),
            CancellationToken.None);

        // The domain de-duplicates mentions, and the handler publishes from that de-duplicated list
        // rather than from the command — so naming somebody twice does not notify them twice.
        events.OfType<UserMentioned>().ShouldHaveSingleItem().MentionedUserId.ShouldBe(target);
    }

    [Fact]
    public async Task TheMessageSentPreviewIsCappedAt140Characters()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author  = Guid.NewGuid();
        var channel = ChannelWith(author);
        lease.Context.Channels.Add(channel);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var events  = new RecordingEventPublisher();
        var handler = new PostMessageHandler(lease.Context, events);

        var body = new string('x', 300);

        await handler.Handle(
            new PostMessageCommand(channel.Id, body, MessageFormat.Plain, null, [], author),
            CancellationToken.None);

        // The preview rides along on the event for notification text; the stored body stays whole.
        events.Single<MessageSent>().Preview.Length.ShouldBe(140);

        await using var verify = lease.NewContext();
        (await verify.Messages.AsNoTracking().SingleAsync()).Body.Length.ShouldBe(300);
    }

    [Fact]
    public async Task ABodyExactlyAtThePreviewLimitIsNotTruncated()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author  = Guid.NewGuid();
        var channel = ChannelWith(author);
        lease.Context.Channels.Add(channel);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var events  = new RecordingEventPublisher();
        var handler = new PostMessageHandler(lease.Context, events);

        var body = new string('y', 140);

        await handler.Handle(
            new PostMessageCommand(channel.Id, body, MessageFormat.Plain, null, [], author),
            CancellationToken.None);

        // The boundary itself, because `<=` and `<` both look right when reading the code.
        events.Single<MessageSent>().Preview.ShouldBe(body);
    }

    [Fact]
    public async Task EventsArePublishedBeforeTheSaveCommits()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author  = Guid.NewGuid();
        var channel = ChannelWith(author);
        lease.Context.Channels.Add(channel);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var events  = new RecordingEventPublisher();
        var handler = new PostMessageHandler(lease.Context, events);

        // SavedChanges fires after the save completes, which is the ordering this asserts on.
        lease.Context.SavedChanges += (_, _) => events.MarkSaved();

        await handler.Handle(
            new PostMessageCommand(channel.Id, "hello", MessageFormat.Plain, null, [Guid.NewGuid()], author),
            CancellationToken.None);

        // This is the transactional-outbox contract, and it is invisible in production until a crash
        // lands between the commit and the publish. Publishing first means the event is written in the
        // same transaction as the row, so either both survive or neither does. A handler reordered to
        // publish after SaveChanges would still pass every other test in this file.
        events.PublishedBeforeSave.ShouldBeTrue(
            "publishing after SaveChanges puts the event outside the outbox transaction, where it can be lost");
    }

    [Fact]
    public async Task AThreadReplyKeepsItsParent()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author  = Guid.NewGuid();
        var channel = ChannelWith(author);
        lease.Context.Channels.Add(channel);

        var parent = Message.Post(channel.Id, author, "top level", MessageFormat.Plain);
        lease.Context.Messages.Add(parent);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new PostMessageHandler(lease.Context, new RecordingEventPublisher());

        var reply = await handler.Handle(
            new PostMessageCommand(channel.Id, "reply", MessageFormat.Plain, parent.Id, [], author),
            CancellationToken.None);

        await using var verify = lease.NewContext();
        var stored = await verify.Messages.AsNoTracking().SingleAsync(m => m.Id == reply.Id);
        stored.ParentId.ShouldBe(parent.Id);
    }
}
