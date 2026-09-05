using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.LeaveChannel;

/// <summary>Handles <see cref="LeaveChannelCommand"/>.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class LeaveChannelHandler(IChatDbContext db) : IRequestHandler<LeaveChannelCommand>
{
    /// <summary>Removes the acting user from the channel.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task Handle(LeaveChannelCommand request, CancellationToken ct)
    {
        var channel = await db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        channel.RemoveMember(request.ActingUserId);
        await db.SaveChangesAsync(ct);
    }
}
