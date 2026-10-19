using StackExchange.Redis;

namespace HUB.Realtime.WebApi.Channels;

/// <summary>
/// Redis-backed channel subscription index. Per channel a hash maps connectionId → userId; per connection
/// a set lists the channels it is in, so a disconnect can clean up without scanning every channel.
/// </summary>
/// <param name="redis">Shared Redis multiplexer (same one the backplane and presence use).</param>
public sealed class RedisChannelSubscriptions(IConnectionMultiplexer redis) : IChannelSubscriptions
{
    // A membership record is only meaningful while the connection is alive; the TTL is a safety net for a
    // connection that vanished without OnDisconnected firing, mirroring presence's stale-connection handling.
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(12);
    private readonly IDatabase _db = redis.GetDatabase();

    private static string Subs(Guid channelId) => $"chan:subs:{channelId}";   // hash connectionId -> userId
    private static string Conn(string connectionId) => $"chan:conn:{connectionId}"; // set of channelIds

    /// <inheritdoc />
    public async Task AddAsync(Guid channelId, string connectionId, Guid userId, CancellationToken ct)
    {
        await _db.HashSetAsync(Subs(channelId), connectionId, userId.ToString());
        await _db.KeyExpireAsync(Subs(channelId), Ttl);
        await _db.SetAddAsync(Conn(connectionId), channelId.ToString());
        await _db.KeyExpireAsync(Conn(connectionId), Ttl);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(Guid channelId, string connectionId, CancellationToken ct)
    {
        await _db.HashDeleteAsync(Subs(channelId), connectionId);
        await _db.SetRemoveAsync(Conn(connectionId), channelId.ToString());
    }

    /// <inheritdoc />
    public async Task RemoveConnectionAsync(string connectionId, CancellationToken ct)
    {
        var channels = await _db.SetMembersAsync(Conn(connectionId));
        foreach (var c in channels)
            if (Guid.TryParse(c.ToString(), out var channelId))
                await _db.HashDeleteAsync(Subs(channelId), connectionId);
        await _db.KeyDeleteAsync(Conn(connectionId));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(string ConnectionId, Guid UserId)>> GetSubscribersAsync(Guid channelId, CancellationToken ct)
    {
        var entries = await _db.HashGetAllAsync(Subs(channelId));
        var result = new List<(string, Guid)>(entries.Length);
        foreach (var e in entries)
            if (Guid.TryParse(e.Value.ToString(), out var userId))
                result.Add((e.Name.ToString(), userId));
        return result;
    }
}
