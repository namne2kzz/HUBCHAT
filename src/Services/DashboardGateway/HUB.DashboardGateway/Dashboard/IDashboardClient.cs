namespace HUB.DashboardGateway.Dashboard;

/// <summary>Typed client for DASHBOARD's read-only /internal/v1/* API.</summary>
public interface IDashboardClient
{
    /// <summary>Gets a single user's profile.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The profile, or null if the user does not exist (404).</returns>
    Task<UserProfile?> GetUserAsync(Guid userId, CancellationToken ct);

    /// <summary>Gets profiles for multiple users in one call.</summary>
    /// <param name="userIds">User ids (deduplicated by the server).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The profiles that exist.</returns>
    Task<IReadOnlyList<UserProfile>> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>Gets a user's repository memberships, roles and permissions.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The memberships, or null if the user does not exist (404).</returns>
    Task<UserMemberships?> GetMembershipsAsync(Guid userId, CancellationToken ct);

    /// <summary>Gets all member profiles for a repository (workspace).</summary>
    /// <param name="repositoryId">The repository id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of user profiles (empty array if repository has no members).</returns>
    Task<IReadOnlyList<UserProfile>> GetRepositoryMembersAsync(Guid repositoryId, CancellationToken ct);

    /// <summary>Gets a work item's context for linking a discussion thread.</summary>
    /// <param name="workItemId">The work item id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The context, or null if the work item does not exist (404).</returns>
    Task<WorkItemContext?> GetWorkItemAsync(Guid workItemId, CancellationToken ct);
}
