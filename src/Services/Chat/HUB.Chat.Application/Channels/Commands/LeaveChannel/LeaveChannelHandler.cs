using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Shared.Contracts.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.LeaveChannel;

/// <summary>Handles <see cref="LeaveChannelCommand"/>.</summary>
/// <param name="db">Chat persistence context.</param>
/// <param name="events">Integration event publisher (transactional outbox).</param>
public sealed class LeaveChannelHandler(IChatDbContext db, IIntegrationEventPublisher events) : IRequestHandler<LeaveChannelCommand>
{
    /// <summary>Removes the acting user from the channel and announces it so their other tabs/devices stop receiving it.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the user has left.</returns>
    public async Task Handle(LeaveChannelCommand request, CancellationToken ct)
    {
        var channel = await db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        channel.RemoveMember(request.ActingUserId);

        // The tab that clicked "leave" unsubscribes itself, but the user's other tabs and devices are still
        // in the group — the same revoke path as a kick takes them out.
        await events.PublishAsync(new ChannelMemberRemoved(channel.Id, request.ActingUserId), ct);
        await db.SaveChangesAsync(ct);
    }
}
