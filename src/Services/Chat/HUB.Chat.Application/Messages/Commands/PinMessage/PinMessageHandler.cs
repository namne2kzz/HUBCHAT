using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Shared.Contracts.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Messages.Commands.PinMessage;

/// <summary>Handles <see cref="PinMessageCommand"/>: any channel member may pin/unpin (Slack-style).</summary>
/// <param name="db">Chat persistence context.</param>
/// <param name="events">Integration event publisher (transactional outbox).</param>
public sealed class PinMessageHandler(IChatDbContext db, IIntegrationEventPublisher events) : IRequestHandler<PinMessageCommand>
{
    /// <summary>Pins/unpins the message and announces the change. No-op (no event) when already in that state.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the pin state is stored.</returns>
    /// <exception cref="NotFoundException">The message does not exist.</exception>
    /// <exception cref="ForbiddenException">The caller is not a member of the channel.</exception>
    public async Task Handle(PinMessageCommand request, CancellationToken ct)
    {
        var message = await db.Messages.FirstOrDefaultAsync(m => m.Id == request.MessageId, ct)
            ?? throw new NotFoundException("Message not found.");

        var isMember = await db.Channels.AsNoTracking()
            .AnyAsync(c => c.Id == message.ChannelId && c.Members.Any(m => m.UserId == request.ActingUserId), ct);
        if (!isMember) throw new ForbiddenException("You are not a member of this channel.");

        var changed = request.Pin ? message.Pin(request.ActingUserId) : message.Unpin();
        if (!changed) return;

        await events.PublishAsync(new MessagePinChanged(message.Id, message.ChannelId, message.IsPinned), ct);
        await db.SaveChangesAsync(ct);
    }
}
