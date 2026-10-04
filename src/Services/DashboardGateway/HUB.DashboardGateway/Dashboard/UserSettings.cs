namespace HUB.DashboardGateway.Dashboard;

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
