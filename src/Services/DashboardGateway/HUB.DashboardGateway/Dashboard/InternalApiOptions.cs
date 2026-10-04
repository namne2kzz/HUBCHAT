namespace HUB.DashboardGateway.Dashboard;

/// <summary>Holds the shared service-to-service token (bound from the "InternalApi" section).</summary>
public sealed class InternalApiOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "InternalApi";

    /// <summary>Shared token sent in the X-Internal-Token header. Must match DASHBOARD's InternalApi:Token.</summary>
    public string Token { get; init; } = string.Empty;
}
