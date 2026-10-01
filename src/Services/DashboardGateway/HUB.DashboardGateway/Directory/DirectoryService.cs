using System.Text.Json;
using HUB.DashboardGateway.Dashboard;
using Microsoft.Extensions.Caching.Distributed;

namespace HUB.DashboardGateway.Directory;

/// <summary>Redis-backed read-through cache. Profiles cache longer (15m); memberships short (3m) to reflect access changes.</summary>
/// <remarks>
/// TTLs are the backstop, not the primary freshness mechanism for membership: DASHBOARD publishes
/// <c>MemberDirectoryChangedEvent</c> on every membership change and
/// <see cref="Consumers.MemberDirectoryChangedConsumer"/> evicts the affected entries straight away.
/// The TTL only covers the case where that event never arrives.
/// </remarks>
/// <param name="dashboard">Upstream DASHBOARD client.</param>
/// <param name="cache">Distributed (Redis) cache.</param>
public sealed class DirectoryService(IDashboardClient dashboard, IDistributedCache cache) : IDirectoryService
{
    private static readonly TimeSpan ProfileTtl    = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MembershipTtl = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan WorkItemTtl   = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MemberListTtl = TimeSpan.FromMinutes(5);
    // Settings are user-controlled and should reflect changes quickly — keep TTL very short.
    private static readonly TimeSpan SettingsTtl   = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public Task<UserProfile?> GetUserAsync(Guid userId, CancellationToken ct) =>
        GetOrPullAsync(DirectoryCacheKeys.User(userId), ProfileTtl, () => dashboard.GetUserAsync(userId, ct), ct);

    /// <inheritdoc />
    public Task<UserMemberships?> GetMembershipsAsync(Guid userId, CancellationToken ct) =>
        GetOrPullAsync(DirectoryCacheKeys.Memberships(userId), MembershipTtl, () => dashboard.GetMembershipsAsync(userId, ct), ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserProfile>> GetWorkspaceMembersAsync(Guid repositoryId, CancellationToken ct)
    {
        var key = DirectoryCacheKeys.WorkspaceMembers(repositoryId);
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
        GetOrPullAsync(DirectoryCacheKeys.WorkItem(workItemId), WorkItemTtl, () => dashboard.GetWorkItemAsync(workItemId, ct), ct);

    /// <inheritdoc />
    public Task<UserSettings?> GetUserSettingsAsync(Guid userId, CancellationToken ct) =>
        GetOrPullAsync(DirectoryCacheKeys.UserSettings(userId), SettingsTtl, () => dashboard.GetUserSettingsAsync(userId, ct), ct);

    /// <inheritdoc />
    public async Task InvalidateMembershipAsync(Guid repositoryId, Guid userId, CancellationToken ct)
    {
        // Only these two: the membership change says nothing about the user's profile or settings, and
        // dropping those would force needless re-pulls from DASHBOARD.
        await cache.RemoveAsync(DirectoryCacheKeys.WorkspaceMembers(repositoryId), ct);
        await cache.RemoveAsync(DirectoryCacheKeys.Memberships(userId), ct);
    }

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
