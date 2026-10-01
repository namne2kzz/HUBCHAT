using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.UpdateChannel;

/// <summary>Handles <see cref="UpdateChannelCommand"/>.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class UpdateChannelHandler(IChatDbContext db) : IRequestHandler<UpdateChannelCommand, ChannelDto>
{
    /// <summary>Updates the channel and returns the refreshed DTO.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated channel DTO.</returns>
    public async Task<ChannelDto> Handle(UpdateChannelCommand request, CancellationToken ct)
    {
        var channel = await db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        var caller = channel.Members.FirstOrDefault(m => m.UserId == request.ActingUserId)
            ?? throw new ForbiddenException("You are not a member of this channel.");

        if (caller.Role == ChannelMemberRole.Member)
            throw new ForbiddenException("Only channel admins or owners can update the channel.");

        channel.UpdateInfo(request.Name, request.Topic);
        await db.SaveChangesAsync(ct);

        return channel.ToDto(request.ActingUserId);
    }
}
