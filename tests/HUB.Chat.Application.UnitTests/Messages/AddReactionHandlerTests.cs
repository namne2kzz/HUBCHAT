using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Messages.Commands.AddReaction;
using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using HUB.Chat.Infrastructure.Persistence;
using HUB.Shared.Contracts.Events;
using HUB.TestKit.Db;
using HUB.TestKit.Fakes;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Messages;

/// <summary>
/// Covers <c>AddReactionHandler</c>, which reacts to a message after checking channel membership.
/// </summary>
/// <remarks>
/// The membership check is the interesting part. The command names a message, not a channel, so the
/// handler has to resolve the message's channel and then ask whether the caller belongs to it. Skipping
/// that would let anyone who learns a message id react to it — including in a private channel they
/// cannot read, which also reveals that the message exists.
/// </remarks>
public sealed class AddReactionHandlerTests
{
    private static async Task<(Channel Channel, Message Message)> SeedAsync(
        ChatDbContext context, Guid author, ChannelType type = ChannelType.Public)
    {
        var channel = Channel.Create(Guid.NewGuid(), "General", type, author);
        context.Channels.Add(channel);

        var message = Message.Post(channel.Id, author, "react to me", MessageFormat.Plain);
        context.Messages.Add(message);

        await context.SaveChangesAsync(CancellationToken.None);
        return (channel, message);
    }

    private static async Task<int> ReactionCountAsync(ChatDbContextLease lease, Guid messageId)
    {
        await using var verify = lease.NewContext();
        return await verify.Messages.AsNoTracking()
            .Where(m => m.Id == messageId)
            .SelectMany(m => m.Reactions)
            .CountAsync();
    }

    [Fact]
    public async Task ReactingToAMissingMessageIsNotFound()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        await Should.ThrowAsync<NotFoundException>(() => new AddReactionHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new AddReactionCommand(Guid.NewGuid(), ":+1:", Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task AMemberCanReact()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author = Guid.NewGuid();
        var (_, message) = await SeedAsync(lease.Context, author);

        await new AddReactionHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new AddReactionCommand(message.Id, ":+1:", author), CancellationToken.None);

        (await ReactionCountAsync(lease, message.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task ANonMemberCannotReactEvenInAPublicChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var (_, message) = await SeedAsync(lease.Context, Guid.NewGuid());

        // Reading a public channel is open; contributing to it is not. A reaction is a contribution, and
        // it is attributed to the person by name.
        await Should.ThrowAsync<ForbiddenException>(() => new AddReactionHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new AddReactionCommand(message.Id, ":+1:", Guid.NewGuid()), CancellationToken.None));

        (await ReactionCountAsync(lease, message.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task ANonMemberCannotReactInAPrivateChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var (_, message) = await SeedAsync(lease.Context, Guid.NewGuid(), ChannelType.Private);

        await Should.ThrowAsync<ForbiddenException>(() => new AddReactionHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new AddReactionCommand(message.Id, ":+1:", Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task ReactingTwiceWithTheSameEmojiIsIdempotent()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author = Guid.NewGuid();
        var (_, message) = await SeedAsync(lease.Context, author);

        var handler = new AddReactionHandler(lease.Context, new RecordingEventPublisher());
        var command = new AddReactionCommand(message.Id, ":+1:", author);

        await handler.Handle(command, CancellationToken.None);
        await handler.Handle(command, CancellationToken.None);

        // A double-click must not double the count. The domain ignores the duplicate rather than
        // throwing, so the handler needs no guard of its own.
        (await ReactionCountAsync(lease, message.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task OnePersonCanReactWithSeveralEmoji()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author = Guid.NewGuid();
        var (_, message) = await SeedAsync(lease.Context, author);

        var handler = new AddReactionHandler(lease.Context, new RecordingEventPublisher());
        await handler.Handle(new AddReactionCommand(message.Id, ":+1:", author), CancellationToken.None);
        await handler.Handle(new AddReactionCommand(message.Id, ":tada:", author), CancellationToken.None);

        (await ReactionCountAsync(lease, message.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task SeveralMembersCanReactWithTheSameEmoji()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author = Guid.NewGuid();
        var (channel, message) = await SeedAsync(lease.Context, author);

        var second = Guid.NewGuid();
        channel.AddMember(second);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new AddReactionHandler(lease.Context, new RecordingEventPublisher());
        await handler.Handle(new AddReactionCommand(message.Id, ":+1:", author), CancellationToken.None);
        await handler.Handle(new AddReactionCommand(message.Id, ":+1:", second), CancellationToken.None);

        (await ReactionCountAsync(lease, message.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task AnEmptyEmojiIsRejected()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author = Guid.NewGuid();
        var (_, message) = await SeedAsync(lease.Context, author);

        await Should.ThrowAsync<DomainException>(() => new AddReactionHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new AddReactionCommand(message.Id, "   ", author), CancellationToken.None));
    }

    [Fact]
    public async Task ADeletedMessageCannotBeReactedTo()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author = Guid.NewGuid();
        var (_, message) = await SeedAsync(lease.Context, author);

        message.SoftDelete();
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        await Should.ThrowAsync<DomainException>(() => new AddReactionHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new AddReactionCommand(message.Id, ":+1:", author), CancellationToken.None));
    }

    [Fact]
    public async Task AJoinedMemberCanThenReact()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var (channel, message) = await SeedAsync(lease.Context, Guid.NewGuid());

        var joiner = Guid.NewGuid();
        channel.AddMember(joiner);
        await lease.Context.SaveChangesAsync(CancellationToken.None);

        // Confirms the check reads live membership rather than something captured when the message was
        // written — somebody who joins later can react to older messages.
        await Should.NotThrowAsync(() => new AddReactionHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new AddReactionCommand(message.Id, ":+1:", joiner), CancellationToken.None));
    }

    // ── Realtime sync (ReactionAdded via outbox) ────────────────────────────

    [Fact]
    public async Task ANewReactionIsAnnouncedOnceWithItsChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author = Guid.NewGuid();
        var (channel, message) = await SeedAsync(lease.Context, author);
        var events = new RecordingEventPublisher();

        await new AddReactionHandler(lease.Context, events)
            .Handle(new AddReactionCommand(message.Id, ":+1:", author), CancellationToken.None);

        // ChannelId is what realtime routes on — without it the push has no group to go to.
        var e = events.Published.OfType<ReactionAdded>().ShouldHaveSingleItem();
        e.MessageId.ShouldBe(message.Id);
        e.ChannelId.ShouldBe(channel.Id);
        e.UserId.ShouldBe(author);
        e.Emoji.ShouldBe(":+1:");
    }

    [Fact]
    public async Task ARepeatedReactionIsNotAnnouncedAgain()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var author = Guid.NewGuid();
        var (_, message) = await SeedAsync(lease.Context, author);
        var events  = new RecordingEventPublisher();
        var handler = new AddReactionHandler(lease.Context, events);

        await handler.Handle(new AddReactionCommand(message.Id, ":+1:", author), CancellationToken.None);
        await handler.Handle(new AddReactionCommand(message.Id, ":+1:", author), CancellationToken.None);

        // A double click changes nothing, so it must not push a duplicate to every open client.
        events.Published.OfType<ReactionAdded>().Count().ShouldBe(1);
    }
}
