using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Messages.Commands.AddReaction;
using HUB.Chat.Application.Messages.Commands.RemoveReaction;
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
/// Covers <c>RemoveReactionHandler</c>: un-reacting removes only the caller's own reaction, and announces
/// the removal once so open clients update.
/// </summary>
public sealed class RemoveReactionHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static async Task<(ChatDbContextLease Lease, Message Message, Guid Alice, Guid Bob)> SeedWithReactionsAsync()
    {
        var lease = await ChatDbContextFactory.CreateAsync();
        var alice = Guid.NewGuid();
        var bob   = Guid.NewGuid();
        var channel = Channel.Create(Guid.NewGuid(), "General", ChannelType.Public, alice);
        channel.AddMember(bob);
        lease.Context.Channels.Add(channel);

        var message = Message.Post(channel.Id, alice, "react to me", MessageFormat.Plain);
        message.AddReaction(alice, "👍");
        message.AddReaction(bob, "👍");
        lease.Context.Messages.Add(message);
        await lease.Context.SaveChangesAsync(Ct);
        return (lease, message, alice, bob);
    }

    private static async Task<List<(Guid UserId, string Emoji)>> ReactionsAsync(ChatDbContextLease lease, Guid messageId)
    {
        await using var verify = lease.NewContext();
        var rows = await verify.Messages.AsNoTracking()
            .Where(m => m.Id == messageId)
            .SelectMany(m => m.Reactions)
            .Select(r => new { r.UserId, r.Emoji })
            .ToListAsync(Ct);
        return [.. rows.Select(r => (r.UserId, r.Emoji))];
    }

    [Fact]
    public async Task RemovesOnlyTheCallersReactionAndAnnouncesIt()
    {
        var (lease, message, alice, bob) = await SeedWithReactionsAsync();
        await using var _ = lease;
        var events = new RecordingEventPublisher();

        await new RemoveReactionHandler(lease.Context, events)
            .Handle(new RemoveReactionCommand(message.Id, "👍", bob), Ct);

        // Bob's thumbs-up goes; Alice's identical one stays — the command can only ever touch the caller's.
        (await ReactionsAsync(lease, message.Id)).ShouldBe([(alice, "👍")]);

        var e = events.Published.OfType<ReactionRemoved>().ShouldHaveSingleItem();
        e.MessageId.ShouldBe(message.Id);
        e.ChannelId.ShouldBe(message.ChannelId);
        e.UserId.ShouldBe(bob);
        e.Emoji.ShouldBe("👍");
    }

    [Fact]
    public async Task RemovingAReactionYouDoNotHaveIsASilentNoOp()
    {
        var (lease, message, _, bob) = await SeedWithReactionsAsync();
        await using var __ = lease;
        var events = new RecordingEventPublisher();

        await new RemoveReactionHandler(lease.Context, events)
            .Handle(new RemoveReactionCommand(message.Id, "🎉", bob), Ct);

        // A double click or retry must not push a phantom removal to every open client.
        events.Published.ShouldBeEmpty();
        (await ReactionsAsync(lease, message.Id)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task AMissingMessageIsNotFound()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        await Should.ThrowAsync<NotFoundException>(() => new RemoveReactionHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new RemoveReactionCommand(Guid.NewGuid(), "👍", Guid.NewGuid()), Ct));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("👍", true)]
    public void EmojiIsRequiredForBothAddAndRemove(string emoji, bool valid)
    {
        new AddReactionValidator().Validate(new AddReactionCommand(Guid.NewGuid(), emoji, Guid.NewGuid())).IsValid.ShouldBe(valid);
        new RemoveReactionValidator().Validate(new RemoveReactionCommand(Guid.NewGuid(), emoji, Guid.NewGuid())).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void AnOverlongEmojiIsRejectedBeforeItReachesTheDatabase()
    {
        // The column is 64 chars; this used to surface as a 500 from the database.
        var tooLong = new string('x', 65);
        new AddReactionValidator().Validate(new AddReactionCommand(Guid.NewGuid(), tooLong, Guid.NewGuid())).IsValid.ShouldBeFalse();
    }
}
