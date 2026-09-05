using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.MarkRead;

/// <summary>Handles <see cref="MarkReadCommand"/>.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class MarkReadHandler(IChatDbContext db) : IRequestHandler<MarkReadCommand>
{
    /// <summary>Updates the member's last-read timestamp.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task Handle(MarkReadCommand request, CancellationToken ct)
    {
        var channel = await db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        var member = channel.Members.FirstOrDefault(m => m.UserId == request.ActingUserId)
                     ?? throw new ForbiddenException("You are not a member of this channel.");

        member.MarkRead(request.ReadAt ?? DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}
