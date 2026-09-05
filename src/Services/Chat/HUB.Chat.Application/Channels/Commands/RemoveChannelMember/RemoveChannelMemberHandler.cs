using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.RemoveChannelMember;

/// <summary>Handles <see cref="RemoveChannelMemberCommand"/>: removes a member from a channel; no-ops if not present.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class RemoveChannelMemberHandler(IChatDbContext db) : IRequestHandler<RemoveChannelMemberCommand>
{
    /// <summary>Loads the channel and removes the member if present.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task Handle(RemoveChannelMemberCommand request, CancellationToken ct)
    {
        var channel = await db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        if (channel.HasMember(request.UserId))
        {
            channel.RemoveMember(request.UserId);
            await db.SaveChangesAsync(ct);
        }
    }
}
