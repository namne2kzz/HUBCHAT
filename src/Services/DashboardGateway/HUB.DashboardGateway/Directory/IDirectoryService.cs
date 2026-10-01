using HUB.DashboardGateway.Dashboard;

namespace HUB.DashboardGateway.Directory;

/// <summary>Read-through cache over <see cref="IDashboardClient"/> — serves user profile + membership data to HUB services.</summary>
public interface IDirectoryService
{
    /// <summary>Gets a user profile, from cache when fresh, otherwise pulling from DASHBOARD.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The profile, or null if the user does not exist.</returns>
    Task<UserProfile?> GetUserAsync(Guid userId, CancellationToken ct);

    /// <summary>Gets a user's memberships, from cache when fresh (short TTL), otherwise pulling from DASHBOARD.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The memberships, or null if the user does not exist.</returns>
    Task<UserMemberships?> GetMembershipsAsync(Guid userId, CancellationToken ct);

    /// <summary>Gets all member profiles for a workspace (repository), from cache or DASHBOARD.</summary>
    /// <param name="repositoryId">The repository (workspace) id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of member profiles (empty if repository not found).</returns>
    Task<IReadOnlyList<UserProfile>> GetWorkspaceMembersAsync(Guid repositoryId, CancellationToken ct);

    /// <summary>Gets a work item's context (cached), or null if it does not exist.</summary>
    /// <param name="workItemId">The work item id.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WorkItemContext?> GetWorkItemAsync(Guid workItemId, CancellationToken ct);

    /// <summary>
    /// Gets a user's persisted display-preference settings from DASHBOARD.
    /// Cached with a short TTL (30 s) so changes made in DASHBOARD propagate quickly.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The settings, or null if the user does not exist.</returns>
    Task<UserSettings?> GetUserSettingsAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Drops the cached directory entries invalidated by a membership change in DASHBOARD.
    /// </summary>
    /// <remarks>
    /// Evicts the repository's member list and the affected user's memberships. Without this, a user
    /// removed in DASHBOARD keeps passing HUB's membership check until the 3-minute TTL expires, and a
    /// newly added member stays invisible for up to five.
    /// </remarks>
    /// <param name="repositoryId">The repository whose membership changed.</param>
    /// <param name="userId">The affected user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once both entries have been evicted.</returns>
    Task InvalidateMembershipAsync(Guid repositoryId, Guid userId, CancellationToken ct);
}
