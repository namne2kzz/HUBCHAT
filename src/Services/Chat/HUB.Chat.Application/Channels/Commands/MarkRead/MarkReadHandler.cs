using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Enums;
using HUB.Shared.Contracts.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.MarkRead;

/// <summary>Handles <see cref="MarkReadCommand"/>.</summary>
/// <param name="db">Chat persistence context.</param>
/// <param name="events">Integration event publisher (transactional outbox).</param>
public sealed class MarkReadHandler(IChatDbContext db, IIntegrationEventPublisher events) : IRequestHandler<MarkReadCommand>
{
    /// <summary>Moves the member's read point forward and announces it to the user's other sessions.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the read point is stored.</returns>
    /// <remarks>
    /// The client's time is capped at now: a read point in the future would silently mark messages that do
    /// not exist yet as read. Nothing is published (or saved) when the point does not move — the client calls
    /// this on every new message, so most calls are no-ops.
    /// </remarks>
    public async Task Handle(MarkReadCommand request, CancellationToken ct)
    {
        var channel = await db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        var member = channel.Members.FirstOrDefault(m => m.UserId == request.ActingUserId)
                     ?? throw new ForbiddenException("You are not a member of this channel.");

        var now    = DateTime.UtcNow;
        var readAt = request.ReadAt is { } at && at < now ? at.ToUniversalTime() : now;
        if (!member.MarkRead(readAt)) return;

        var isDirect = channel.Type is ChannelType.Dm or ChannelType.GroupDm;
        await events.PublishAsync(new ChannelReadMarked(channel.Id, request.ActingUserId, readAt, isDirect), ct);
        await db.SaveChangesAsync(ct);
    }
}
