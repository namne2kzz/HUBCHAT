using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.TransferOwnership;

/// <summary>Handles <see cref="TransferOwnershipCommand"/>: only the current Owner may transfer ownership.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class TransferOwnershipHandler(IChatDbContext db)
    : IRequestHandler<TransferOwnershipCommand, ChannelDto>
{
    /// <summary>Validates ownership, promotes the new owner (demoting the caller to Admin), and returns the refreshed DTO.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated channel DTO with the caller's new role (Admin).</returns>
    public async Task<ChannelDto> Handle(TransferOwnershipCommand request, CancellationToken ct)
    {
        var channel = await db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        var caller = channel.Members.FirstOrDefault(m => m.UserId == request.ActingUserId)
            ?? throw new ForbiddenException("You are not a member of this channel.");

        if (caller.Role != ChannelMemberRole.Owner)
            throw new ForbiddenException("Only the channel owner can transfer ownership.");

        channel.TransferOwnership(request.ActingUserId, request.NewOwnerUserId);
        await db.SaveChangesAsync(ct);

        return channel.ToDto(myRole: caller.Role);
    }
}
