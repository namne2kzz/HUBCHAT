using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.JoinChannel;

/// <summary>Handles <see cref="JoinChannelCommand"/>.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class JoinChannelHandler(IChatDbContext db) : IRequestHandler<JoinChannelCommand>
{
    /// <summary>Loads the channel (with members) and adds the acting user; idempotent-ish via domain guard.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task Handle(JoinChannelCommand request, CancellationToken ct)
    {
        var channel = await db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        if (!channel.HasMember(request.ActingUserId))
        {
            channel.AddMember(request.ActingUserId, ChannelMemberRole.Member);
            await db.SaveChangesAsync(ct);
        }
    }
}
