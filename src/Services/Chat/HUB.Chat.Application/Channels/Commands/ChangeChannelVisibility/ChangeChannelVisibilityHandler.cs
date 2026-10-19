using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Enums;
using HUB.Shared.Contracts.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.ChangeChannelVisibility;

/// <summary>Handles <see cref="ChangeChannelVisibilityCommand"/>: only the channel Owner may switch visibility.</summary>
/// <param name="db">Chat persistence context.</param>
/// <param name="events">Integration event publisher (transactional outbox).</param>
public sealed class ChangeChannelVisibilityHandler(IChatDbContext db, IIntegrationEventPublisher events)
    : IRequestHandler<ChangeChannelVisibilityCommand, ChannelDto>
{
    /// <summary>Validates ownership, switches visibility, revokes non-member watchers on a lock-down, and returns the DTO.</summary>
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

        var wasPublic = !channel.IsPrivate;
        channel.ChangeVisibility(request.IsPrivate);

        // Going public → private: anyone who was watching the (previously open) channel but is not a member
        // must lose their live subscription now. Realtime enforces this from the member list below.
        if (wasPublic && request.IsPrivate)
            await events.PublishAsync(
                new ChannelMadePrivate(channel.Id, [.. channel.Members.Select(m => m.UserId)]), ct);

        await db.SaveChangesAsync(ct);

        return channel.ToDto(request.ActingUserId);
    }
}
