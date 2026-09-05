using System.Text.Json;
using HUB.DashboardGateway.Dashboard;
using Microsoft.Extensions.Caching.Distributed;

namespace HUB.DashboardGateway.Directory;

/// <summary>Redis-backed read-through cache. Profiles cache longer (15m); memberships short (3m) to reflect access changes.</summary>
/// <param name="dashboard">Upstream DASHBOARD client.</param>
/// <param name="cache">Distributed (Redis) cache.</param>
public sealed class DirectoryService(IDashboardClient dashboard, IDistributedCache cache) : IDirectoryService
{
    private static readonly TimeSpan ProfileTtl    = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MembershipTtl = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan WorkItemTtl   = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MemberListTtl = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public Task<UserProfile?> GetUserAsync(Guid userId, CancellationToken ct) =>
        GetOrPullAsync($"dir:user:{userId}", ProfileTtl, () => dashboard.GetUserAsync(userId, ct), ct);

    /// <inheritdoc />
    public Task<UserMemberships?> GetMembershipsAsync(Guid userId, CancellationToken ct) =>
        GetOrPullAsync($"dir:member:{userId}", MembershipTtl, () => dashboard.GetMembershipsAsync(userId, ct), ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserProfile>> GetWorkspaceMembersAsync(Guid repositoryId, CancellationToken ct)
    {
        var key = $"dir:repo:{repositoryId}:members";
        var cached = await cache.GetStringAsync(key, ct);
        if (cached is not null)
            return JsonSerializer.Deserialize<List<UserProfile>>(cached) ?? [];

        var value = await dashboard.GetRepositoryMembersAsync(repositoryId, ct);

        await cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(value),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = MemberListTtl },
            ct);

        return value;
    }

    /// <inheritdoc />
    public Task<WorkItemContext?> GetWorkItemAsync(Guid workItemId, CancellationToken ct) =>
        GetOrPullAsync($"dir:workitem:{workItemId}", WorkItemTtl, () => dashboard.GetWorkItemAsync(workItemId, ct), ct);

    private async Task<T?> GetOrPullAsync<T>(string key, TimeSpan ttl, Func<Task<T?>> pull, CancellationToken ct)
        where T : class
    {
        var cached = await cache.GetStringAsync(key, ct);
        if (cached is not null)
            return JsonSerializer.Deserialize<T>(cached);

        var value = await pull();
        if (value is null) return null; // don't cache negative lookups for now

        await cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(value),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            ct);

        return value;
    }
}
