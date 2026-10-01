using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.ChangeChannelVisibility;

/// <summary>Handles <see cref="ChangeChannelVisibilityCommand"/>: only the channel Owner may switch visibility.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class ChangeChannelVisibilityHandler(IChatDbContext db)
    : IRequestHandler<ChangeChannelVisibilityCommand, ChannelDto>
{
    /// <summary>Validates ownership, switches visibility, and returns the refreshed DTO.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated channel DTO (with the caller's role).</returns>
    public async Task<ChannelDto> Handle(ChangeChannelVisibilityCommand request, CancellationToken ct)
    {
        var channel = await db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        var caller = channel.Members.FirstOrDefault(m => m.UserId == request.ActingUserId)
            ?? throw new ForbiddenException("You are not a member of this channel.");

        if (caller.Role != ChannelMemberRole.Owner)
            throw new ForbiddenException("Only the channel owner can change visibility.");

        channel.ChangeVisibility(request.IsPrivate);
        await db.SaveChangesAsync(ct);

        return channel.ToDto(request.ActingUserId);
    }
}
