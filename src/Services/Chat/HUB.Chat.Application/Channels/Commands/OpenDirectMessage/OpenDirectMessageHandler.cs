using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.OpenDirectMessage;

/// <summary>Handles <see cref="OpenDirectMessageCommand"/>: find-or-create a two-member DM channel.</summary>
/// <param name="db">Chat persistence context.</param>
public sealed class OpenDirectMessageHandler(IChatDbContext db) : IRequestHandler<OpenDirectMessageCommand, ChannelDto>
{
    /// <summary>Returns the existing canonical DM (both users as members) or creates one.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The DM channel DTO, with <c>OtherUserId</c> set to the target.</returns>
    public async Task<ChannelDto> Handle(OpenDirectMessageCommand request, CancellationToken ct)
    {
        if (request.TargetUserId == request.ActingUserId)
            throw new DomainException("You cannot start a direct message with yourself.");

        // Canonical DM: any DM channel that has BOTH users as members (workspace-independent).
        var existing = await db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Type == ChannelType.Dm
                && c.Members.Any(m => m.UserId == request.ActingUserId)
                && c.Members.Any(m => m.UserId == request.TargetUserId), ct);

        if (existing is not null)
            return existing.ToDto(request.ActingUserId, otherUserId: request.TargetUserId);

        // Deterministic, unique per-pair name — the display name is resolved client-side via OtherUserId.
        var (a, b) = request.ActingUserId.CompareTo(request.TargetUserId) < 0
            ? (request.ActingUserId, request.TargetUserId)
            : (request.TargetUserId, request.ActingUserId);
        var name = $"dm-{a}-{b}";

        var channel = Channel.Create(request.WorkspaceId, name, ChannelType.Dm, request.ActingUserId, string.Empty);
        channel.AddMember(request.TargetUserId, ChannelMemberRole.Member);
        db.Channels.Add(channel);
        await db.SaveChangesAsync(ct);

        return channel.ToDto(request.ActingUserId, otherUserId: request.TargetUserId);
    }
}
