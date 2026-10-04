namespace HUB.DashboardGateway.Dashboard;

/// <summary>A user's memberships across repositories plus the global-admin flag and owning organization (tenant = HUB workspace).</summary>
public sealed record UserMemberships(
    Guid UserId,
    bool IsGlobalAdmin,
    Guid OrgId,
    string OrgAlias,
    string OrgName,
    IReadOnlyList<RepositoryMembership> Repositories);
