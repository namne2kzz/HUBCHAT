using StackExchange.Redis;

namespace HUB.Realtime.WebApi.Presence;

/// <summary>Presence in Redis: a per-user connection set + a TTL'd status key (safety against stale connections).</summary>
/// <param name="redis">Shared Redis multiplexer.</param>
public sealed class RedisPresenceStore(IConnectionMultiplexer redis) : IPresenceStore
{
    private static readonly TimeSpan Ttl       = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan ManualTtl = TimeSpan.FromDays(7); // manual override outlives brief disconnects
    private readonly IDatabase _db = redis.GetDatabase();

    private static string Conns(Guid u)  => $"conn:{u}";
    private static string Status(Guid u) => $"presence:{u}";
    private static string Manual(Guid u) => $"manual:{u}"; // user-chosen override (Away/DoNotDisturb)

    /// <inheritdoc />
    public async Task<PresenceStatus?> AddConnectionAsync(Guid userId, string connectionId, CancellationToken ct)
    {
        var added = await _db.SetAddAsync(Conns(userId), connectionId);
        var count = await _db.SetLengthAsync(Conns(userId));
        var effective = await EffectiveOnlineStatusAsync(userId); // honours a persisted manual override
        await _db.StringSetAsync(Status(userId), (int)effective, Ttl);
        await _db.KeyExpireAsync(Conns(userId), Ttl);
        return added && count == 1 ? effective : null; // broadcast only when just became online
    }

    /// <inheritdoc />
    public async Task<bool> RemoveConnectionAsync(Guid userId, string connectionId, CancellationToken ct)
    {
        await _db.SetRemoveAsync(Conns(userId), connectionId);
        var count = await _db.SetLengthAsync(Conns(userId));
        if (count > 0) return false;
        await _db.KeyDeleteAsync(Status(userId));
        await _db.KeyDeleteAsync(Conns(userId));
        return true; // last connection dropped
    }

    /// <inheritdoc />
    public async Task HeartbeatAsync(Guid userId, CancellationToken ct)
    {
        // Re-assert the effective status (in case the manual override changed) and refresh TTLs.
        var effective = await EffectiveOnlineStatusAsync(userId);
        await _db.StringSetAsync(Status(userId), (int)effective, Ttl);
        await _db.KeyExpireAsync(Conns(userId), Ttl);
    }

    /// <inheritdoc />
    public async Task<PresenceStatus> SetManualStatusAsync(Guid userId, PresenceStatus? status, CancellationToken ct)
    {
        // Online/null = "Active"/auto → drop the override so presence follows the connection.
        if (status is null or PresenceStatus.Online)
            await _db.KeyDeleteAsync(Manual(userId));
        else
            await _db.StringSetAsync(Manual(userId), (int)status.Value, ManualTtl);

        var online    = await _db.SetLengthAsync(Conns(userId)) > 0;
        var effective = online ? (status ?? PresenceStatus.Online) : PresenceStatus.Offline;
        if (online) await _db.StringSetAsync(Status(userId), (int)effective, Ttl);
        return effective;
    }

    /// <summary>Effective status for a connected user: the persisted manual override, or Online when none.</summary>
    private async Task<PresenceStatus> EffectiveOnlineStatusAsync(Guid userId)
    {
        var manual = await _db.StringGetAsync(Manual(userId));
        return manual.HasValue && int.TryParse(manual.ToString(), out var s)
            ? (PresenceStatus)s
            : PresenceStatus.Online;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, PresenceStatus>> GetStatusesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var keys = userIds.Select(u => (RedisKey)Status(u)).ToArray();
        var values = keys.Length == 0 ? [] : await _db.StringGetAsync(keys);
        var result = new Dictionary<Guid, PresenceStatus>(userIds.Count);
        var i = 0;
        foreach (var u in userIds)
        {
            var v = values[i++];
            result[u] = v.HasValue && int.TryParse(v.ToString(), out var s) ? (PresenceStatus)s : PresenceStatus.Offline;
        }
        return result;
    }
}
