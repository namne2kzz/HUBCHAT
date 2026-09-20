using System.Net;
using System.Net.Http.Json;

namespace HUB.DashboardGateway.Dashboard;

/// <summary>HTTP implementation of <see cref="IDashboardClient"/> over DASHBOARD's /internal/v1/* API.</summary>
/// <param name="http">The configured HttpClient (base address + internal token header set at registration).</param>
public sealed class DashboardClient(HttpClient http) : IDashboardClient
{
    /// <inheritdoc />
    public async Task<UserProfile?> GetUserAsync(Guid userId, CancellationToken ct)
    {
        using var response = await http.GetAsync($"users/{userId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserProfile>(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserProfile>> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return [];
        var ids = string.Join(',', userIds);
        var result = await http.GetFromJsonAsync<List<UserProfile>>($"users?ids={ids}", ct);
        return result ?? [];
    }

    /// <inheritdoc />
    public async Task<UserMemberships?> GetMembershipsAsync(Guid userId, CancellationToken ct)
    {
        using var response = await http.GetAsync($"users/{userId}/memberships", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserMemberships>(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserProfile>> GetRepositoryMembersAsync(Guid repositoryId, CancellationToken ct)
    {
        var result = await http.GetFromJsonAsync<List<UserProfile>>($"repositories/{repositoryId}/members", ct);
        return result ?? [];
    }

    /// <inheritdoc />
    public async Task<WorkItemContext?> GetWorkItemAsync(Guid workItemId, CancellationToken ct)
    {
        using var response = await http.GetAsync($"work-items/{workItemId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkItemContext>(ct);
    }

    /// <inheritdoc />
    public async Task<UserSettings?> GetUserSettingsAsync(Guid userId, CancellationToken ct)
    {
        using var response = await http.GetAsync($"users/{userId}/settings", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var dict = await response.Content.ReadFromJsonAsync<Dictionary<string, string?>>(ct);
        return new UserSettings(dict ?? new Dictionary<string, string?>());
    }
}
