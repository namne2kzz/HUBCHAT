namespace HUB.Shared.Auth;

/// <summary>Constants for the service-to-service token used when HUB calls DASHBOARD's /internal API.</summary>
public static class ServiceTokenDefaults
{
    /// <summary>Header carrying the shared internal token. Must match DASHBOARD's ServiceTokenDefaults.Header.</summary>
    public const string Header = "X-Internal-Token";
}
