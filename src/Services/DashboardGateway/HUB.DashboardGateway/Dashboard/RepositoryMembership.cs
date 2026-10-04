namespace HUB.DashboardGateway.Dashboard;

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
