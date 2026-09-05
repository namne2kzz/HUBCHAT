using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.ArchiveChannel;

/// <summary>Handles <see cref="ArchiveChannelCommand"/>: archives a channel; idempotent.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class ArchiveChannelHandler(IChatDbContext db) : IRequestHandler<ArchiveChannelCommand>
{
    /// <summary>Loads the channel and archives it if not already archived.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task Handle(ArchiveChannelCommand request, CancellationToken ct)
    {
        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        if (!channel.IsArchived)
        {
            channel.Archive();
            await db.SaveChangesAsync(ct);
        }
    }
}
