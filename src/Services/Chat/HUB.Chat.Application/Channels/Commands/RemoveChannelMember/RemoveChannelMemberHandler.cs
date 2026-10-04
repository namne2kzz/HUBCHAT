using HUB.Chat.Application.Channels.Authorization;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.Application.Channels.Commands.RemoveChannelMember;

/// <summary>Handles <see cref="RemoveChannelMemberCommand"/>: removes a member from a channel; no-ops if not present.</summary>
/// <param name="db">Chat persistence context.</param>
/// <param name="permissions">Caller workspace permissions (ManageChannels).</param>
public sealed class RemoveChannelMemberHandler(IChatDbContext db, IWorkspacePermissions permissions)
    : IRequestHandler<RemoveChannelMemberCommand>
{
    /// <summary>Loads the channel, checks the caller may manage members (public API), and removes the member if present.</summary>
    /// <param name="request">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the member has been removed (or was not present).</returns>
    /// <exception cref="ForbiddenException">A public-API caller who may not manage this channel's members.</exception>
    public async Task Handle(RemoveChannelMemberCommand request, CancellationToken ct)
    {
        var channel = await db.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, ct)
            ?? throw new NotFoundException("Channel not found.");

        // Checked before the "not a member" no-op, so a stranger cannot probe membership through it.
        if (request.ActingUserId is { } actor)
            await ChannelMemberManagement.EnsureCanManageMembersAsync(channel, actor, permissions, ct);

        if (channel.HasMember(request.UserId))
        {
            channel.RemoveMember(request.UserId);
            await db.SaveChangesAsync(ct);
        }
    }
}
