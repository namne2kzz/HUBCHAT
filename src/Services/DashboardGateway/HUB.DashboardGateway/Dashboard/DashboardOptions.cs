namespace HUB.DashboardGateway.Dashboard;

/// <summary>Settings for calling DASHBOARD's internal service-to-service API.</summary>
public sealed class DashboardOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Dashboard";

    /// <summary>Base URL of DASHBOARD's internal API, e.g. http://host.docker.internal:5080/internal/v1/ (trailing slash required).</summary>
    public string InternalBaseUrl { get; init; } = string.Empty;
}

/// <summary>Holds the shared service-to-service token (bound from the "InternalApi" section).</summary>
public sealed class InternalApiOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "InternalApi";

    /// <summary>Shared token sent in the X-Internal-Token header. Must match DASHBOARD's InternalApi:Token.</summary>
    public string Token { get; init; } = string.Empty;
}
