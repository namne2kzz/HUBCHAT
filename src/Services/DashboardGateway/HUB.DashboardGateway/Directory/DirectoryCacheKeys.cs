namespace HUB.DashboardGateway.Directory;

/// <summary>
/// Single source of truth for every Redis key the directory cache writes.
/// </summary>
/// <remarks>
/// Both <see cref="DirectoryService"/> (which writes them) and the invalidation consumer (which drops
/// them) build keys from here. A literal duplicated across those two would let them drift apart, and
/// the invalidation would then miss silently — the cached value simply survives to its TTL, with
/// nothing in the logs to show for it.
/// </remarks>
public static class DirectoryCacheKeys
{
    /// <summary>Key prefix identifying HUB inside the Redis instance it shares with DASHBOARD.</summary>
    public const string AppPrefix = "hub";

    /// <summary>Key holding one user's DASHBOARD profile.</summary>
    /// <param name="userId">The user id.</param>
    /// <returns>Fully qualified cache key.</returns>
    public static string User(Guid userId) => $"{AppPrefix}:dir:user:{userId}";

    /// <summary>Key holding one user's repository memberships.</summary>
    /// <param name="userId">The user id.</param>
    /// <returns>Fully qualified cache key.</returns>
    public static string Memberships(Guid userId) => $"{AppPrefix}:dir:member:{userId}";

    /// <summary>Key holding one repository's member profile list.</summary>
    /// <param name="repositoryId">The repository (workspace) id.</param>
    /// <returns>Fully qualified cache key.</returns>
    public static string WorkspaceMembers(Guid repositoryId) => $"{AppPrefix}:dir:repo:{repositoryId}:members";

    /// <summary>Key holding one work item's context.</summary>
    /// <param name="workItemId">The work item id.</param>
    /// <returns>Fully qualified cache key.</returns>
    public static string WorkItem(Guid workItemId) => $"{AppPrefix}:dir:workitem:{workItemId}";

    /// <summary>Key holding one user's display-preference settings.</summary>
    /// <param name="userId">The user id.</param>
    /// <returns>Fully qualified cache key.</returns>
    public static string UserSettings(Guid userId) => $"{AppPrefix}:dir:settings:{userId}";
}
