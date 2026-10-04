namespace HUB.Chat.Application.Common.Interfaces;

/// <summary>
/// Reads the <b>calling user's</b> workspace-level (DASHBOARD repository) permissions.
/// </summary>
/// <remarks>
/// Deliberately scoped to the caller: the implementation relays the caller's own token to the
/// dashboard-gateway, so chat-service can never ask about another user's privileges.
/// </remarks>
public interface IWorkspacePermissions
{
    /// <summary>Checks whether the calling user holds a permission in a workspace.</summary>
    /// <param name="workspaceId">Workspace (= DASHBOARD repository) id.</param>
    /// <param name="permission">Permission name, e.g. <see cref="WorkspacePermissionNames.ManageChannels"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// True when granted. False when not granted <b>or</b> when the permission source is unreachable —
    /// callers treat "unknown" as "no" (fail closed).
    /// </returns>
    Task<bool> CallerHasPermissionAsync(Guid workspaceId, string permission, CancellationToken ct);
}
