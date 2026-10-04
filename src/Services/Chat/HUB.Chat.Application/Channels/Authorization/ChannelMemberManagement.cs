using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;

namespace HUB.Chat.Application.Channels.Authorization;

/// <summary>Who may add or remove other people's channel membership.</summary>
public static class ChannelMemberManagement
{
    /// <summary>
    /// Throws unless the acting user is the channel's Owner/Admin, or holds
    /// <see cref="WorkspacePermissionNames.ManageChannels"/> in the channel's workspace.
    /// </summary>
    /// <param name="channel">Channel with its members loaded.</param>
    /// <param name="actingUserId">The user performing the change.</param>
    /// <param name="permissions">Caller workspace permissions (consulted only when the channel role is not enough).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the caller is allowed.</returns>
    /// <exception cref="ForbiddenException">The caller may not manage this channel's members.</exception>
    /// <remarks>
    /// Mirrors HUB.VIEW's <c>canManage</c>. The channel role is checked first and is free; the workspace
    /// permission costs a call to dashboard-gateway, so it is only asked for when the role says no.
    /// </remarks>
    public static async Task EnsureCanManageMembersAsync(
        Channel channel, Guid actingUserId, IWorkspacePermissions permissions, CancellationToken ct)
    {
        var role = channel.Members.FirstOrDefault(m => m.UserId == actingUserId)?.Role;
        if (role is ChannelMemberRole.Owner or ChannelMemberRole.Admin)
            return;

        if (await permissions.CallerHasPermissionAsync(channel.WorkspaceId, WorkspacePermissionNames.ManageChannels, ct))
            return;

        throw new ForbiddenException("Only channel owners/admins or users with ManageChannels can manage members.");
    }
}
