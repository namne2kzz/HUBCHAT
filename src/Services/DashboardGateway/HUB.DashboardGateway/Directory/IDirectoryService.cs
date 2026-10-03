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

    /// <summary>Gets several user profiles at once, serving whichever are already cached.</summary>
    /// <remarks>
    /// Caches per user rather than per batch: a batch-shaped key would miss on every different id
    /// combination and could never be invalidated by a single user's change. Only the ids missing from
    /// cache are fetched, in one upstream call.
    /// </remarks>
    /// <param name="userIds">The user ids; duplicates are ignored.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Profiles for the ids that exist, in no guaranteed order.</returns>
    Task<IReadOnlyList<UserProfile>> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

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

    /// <summary>Drops one cached profile entry.</summary>
    /// <remarks>
    /// Covers display name, avatar, admin flag and active state, all of which are served from the same
    /// cached <see cref="UserProfile"/>. The admin and active flags make this a privilege concern, not
    /// just a cosmetic one: a deactivated or demoted account would otherwise look unchanged here for up
    /// to fifteen minutes.
    /// </remarks>
    /// <param name="userId">The user whose profile changed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the entry has been evicted.</returns>
    Task InvalidateUserProfileAsync(Guid userId, CancellationToken ct);

    /// <summary>Drops one cached user-settings entry.</summary>
    /// <param name="userId">The user whose settings changed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the entry has been evicted.</returns>
    Task InvalidateUserSettingsAsync(Guid userId, CancellationToken ct);

    /// <summary>Drops one cached work-item context entry.</summary>
    /// <param name="workItemId">The work item whose title, state or existence changed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the entry has been evicted.</returns>
    Task InvalidateWorkItemAsync(Guid workItemId, CancellationToken ct);
}
