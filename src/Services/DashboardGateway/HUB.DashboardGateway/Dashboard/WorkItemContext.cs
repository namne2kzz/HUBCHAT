namespace HUB.DashboardGateway.Dashboard;

/// <summary>Context of a DASHBOARD work item, used to link/label a HUB discussion thread.</summary>
public sealed record WorkItemContext(
    Guid Id,
    string Key,
    string Title,
    string State,
    Guid RepositoryId,
    string RepositoryCode);
