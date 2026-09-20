namespace HUB.DashboardGateway.Dashboard;

/// <summary>Minimal user profile returned by DASHBOARD /internal/v1/users/{id}. Avatar is a CSS class, not a URL.</summary>
public sealed record UserProfile(
    Guid Id,
    string Name,
    string Email,
    string AvatarClass,
    bool IsGlobalAdmin,
    bool IsDeleted);

/// <summary>A repository the user belongs to, with granted role and permission names.</summary>
public sealed record RepositoryMembership(
    Guid RepositoryId,
    string RepositoryName,
    string RepositoryCode,
    bool IsArchived,
    Guid RoleId,
    string RoleName,
    IReadOnlyList<string> Permissions,
    string DefaultRole);

/// <summary>A user's memberships across repositories plus the global-admin flag and owning organization (tenant = HUB workspace).</summary>
public sealed record UserMemberships(
    Guid UserId,
    bool IsGlobalAdmin,
    Guid OrgId,
    string OrgAlias,
    string OrgName,
    IReadOnlyList<RepositoryMembership> Repositories);

/// <summary>Context of a DASHBOARD work item, used to link/label a HUB discussion thread.</summary>
public sealed record WorkItemContext(
    Guid Id,
    string Key,
    string Title,
    string State,
    Guid RepositoryId,
    string RepositoryCode);

/// <summary>
/// Flat key → value map of a user's persisted display preferences from DASHBOARD.
/// Missing keys mean "use app default". Values are always strings; callers cast to their domain type.
/// </summary>
public sealed record UserSettings(IReadOnlyDictionary<string, string?> Values)
{
    /// <summary>Returns the raw value for <paramref name="key"/>, or null if not set.</summary>
    /// <param name="key">The setting key, e.g. <c>"ui.date-format"</c>.</param>
    /// <returns>The stored string value, or null.</returns>
    public string? Get(string key) => Values.TryGetValue(key, out var v) ? v : null;
}
