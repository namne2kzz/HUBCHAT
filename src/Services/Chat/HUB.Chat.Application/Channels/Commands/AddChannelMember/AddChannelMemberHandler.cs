using HUB.Chat.Application.Channels.Authorization;
using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Enums;
using HUB.Shared.Contracts.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.AddChannelMember;

/// <summary>Handles <see cref="AddChannelMemberCommand"/>: idempotently adds a member to a channel.</summary>
/// <param name="db">Chat persistence context.</param>
/// <param name="permissions">Caller workspace permissions (ManageChannels).</param>
/// <param name="events">Integration event publisher (transactional outbox).</param>
public sealed class AddChannelMemberHandler(IChatDbContext db, IWorkspacePermissions permissions, IIntegrationEventPublisher events)
    : IRequestHandler<AddChannelMemberCommand, ChannelMemberDto>
{
    /// <summary>Loads the channel, checks the caller may manage members (public API), and adds the user if not already present.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The membership DTO for the user.</returns>
    /// <exception cref="ForbiddenException">A public-API caller who may not manage this channel's members.</exception>
    /// <remarks>
    /// Without the check, any signed-in user could add themselves to someone else's private channel and
    /// read it. Internal (sprint sync) calls carry no actor and are trusted.
    /// </remarks>
    public async Task<ChannelMemberDto> Handle(AddChannelMemberCommand request, CancellationToken ct)
    {
        var channel = await db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        if (request.AddedByUserId is { } actor)
            await ChannelMemberManagement.EnsureCanManageMembersAsync(channel, actor, permissions, ct);

        if (!channel.HasMember(request.UserId))
        {
            channel.AddMember(request.UserId, ChannelMemberRole.Member);

            // Realtime pushes the channel to the user on this event (outbox: only if the add commits),
            // so it appears in their sidebar without a reload. Mirror of ChannelMemberRemoved.
            await events.PublishAsync(new ChannelMemberAdded(channel.Id, request.UserId), ct);
            await db.SaveChangesAsync(ct);
        }

        var member = channel.Members.First(m => m.UserId == request.UserId);
        return new ChannelMemberDto(member.UserId, member.Role, member.Muted, member.JoinedAt);
    }
}
