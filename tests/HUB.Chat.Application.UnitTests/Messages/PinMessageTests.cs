using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Messages.Commands.PinMessage;
using HUB.Chat.Application.Messages.Queries.ListPinnedMessages;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using HUB.Shared.Contracts.Events;
using HUB.TestKit.Db;
using HUB.TestKit.Fakes;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Messages;

/// <summary>Covers pin/unpin (CF-10): member-gated, idempotent, announced, and the pinned list.</summary>
public sealed class PinMessageTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static async Task<(ChatDbContextLease Lease, Channel Channel, Message Message, Guid Member)> SeedAsync()
    {
        var lease = await ChatDbContextFactory.CreateAsync();
        var member = Guid.NewGuid();
        var channel = Channel.Create(Guid.NewGuid(), "General", ChannelType.Public, member);
        lease.Context.Channels.Add(channel);
        var message = Message.Post(channel.Id, member, "pin me", MessageFormat.Plain);
        lease.Context.Messages.Add(message);
        await lease.Context.SaveChangesAsync(Ct);
        return (lease, channel, message, member);
    }

    private static async Task<bool> IsPinnedAsync(ChatDbContextLease lease, Guid id)
    {
        await using var v = lease.NewContext();
        return (await v.Messages.AsNoTracking().SingleAsync(m => m.Id == id, Ct)).IsPinned;
    }

    [Fact]
    public async Task AMemberPinsAndItIsAnnounced()
    {
        var (lease, _, message, member) = await SeedAsync();
        await using var _ = lease;
        var events = new RecordingEventPublisher();

        await new PinMessageHandler(lease.Context, events).Handle(new PinMessageCommand(message.Id, true, member), Ct);

        (await IsPinnedAsync(lease, message.Id)).ShouldBeTrue();
        events.Published.OfType<MessagePinChanged>().ShouldHaveSingleItem().IsPinned.ShouldBeTrue();
    }

    [Fact]
    public async Task UnpinReversesItAndIsAnnounced()
    {
        var (lease, _, message, member) = await SeedAsync();
        await using var _ = lease;
        var events = new RecordingEventPublisher();
        var handler = new PinMessageHandler(lease.Context, events);

        await handler.Handle(new PinMessageCommand(message.Id, true, member), Ct);
        await handler.Handle(new PinMessageCommand(message.Id, false, member), Ct);

        (await IsPinnedAsync(lease, message.Id)).ShouldBeFalse();
        events.Published.OfType<MessagePinChanged>().Count().ShouldBe(2);
    }

    [Fact]
    public async Task PinningAnAlreadyPinnedMessageAnnouncesNothing()
    {
        var (lease, _, message, member) = await SeedAsync();
        await using var _ = lease;
        var events = new RecordingEventPublisher();
        var handler = new PinMessageHandler(lease.Context, events);

        await handler.Handle(new PinMessageCommand(message.Id, true, member), Ct);
        await handler.Handle(new PinMessageCommand(message.Id, true, member), Ct);

        // Idempotent: a double tap must not fire a second realtime push.
        events.Published.OfType<MessagePinChanged>().Count().ShouldBe(1);
    }

    [Fact]
    public async Task ANonMemberCannotPin()
    {
        var (lease, _, message, _) = await SeedAsync();
        await using var _d = lease;

        await Should.ThrowAsync<ForbiddenException>(() => new PinMessageHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new PinMessageCommand(message.Id, true, Guid.NewGuid()), Ct));
    }

    [Fact]
    public async Task ListPinnedReturnsPinnedMessagesNewestPinFirst()
    {
        var (lease, channel, first, member) = await SeedAsync();
        await using var _ = lease;
        var second = Message.Post(channel.Id, member, "also pin", MessageFormat.Plain);
        lease.Context.Messages.Add(second);
        await lease.Context.SaveChangesAsync(Ct);

        var handler = new PinMessageHandler(lease.Context, new RecordingEventPublisher());
        await handler.Handle(new PinMessageCommand(first.Id, true, member), Ct);
        await handler.Handle(new PinMessageCommand(second.Id, true, member), Ct);

        var pinned = await new ListPinnedMessagesHandler(lease.Context)
            .Handle(new ListPinnedMessagesQuery(channel.Id, member), Ct);

        pinned.Select(m => m.Body).ShouldBe(["also pin", "pin me"]);
        pinned.ShouldAllBe(m => m.IsPinned);
    }

    [Fact]
    public async Task CannotPinADeletedMessage()
    {
        var (lease, _, message, member) = await SeedAsync();
        await using var _ = lease;
        var tracked = await lease.Context.Messages.FirstAsync(m => m.Id == message.Id, Ct);
        tracked.SoftDelete();
        await lease.Context.SaveChangesAsync(Ct);

        await Should.ThrowAsync<HUB.Chat.Domain.Common.DomainException>(() =>
            new PinMessageHandler(lease.Context, new RecordingEventPublisher())
                .Handle(new PinMessageCommand(message.Id, true, member), Ct));
    }
}
