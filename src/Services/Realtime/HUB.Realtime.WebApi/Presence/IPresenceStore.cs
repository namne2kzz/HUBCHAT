namespace HUB.Realtime.WebApi.Presence;

/// <summary>User presence status. Online/Away/DoNotDisturb are "online" variants; Offline means no live connection.</summary>
public enum PresenceStatus { Offline = 0, Online = 1, Away = 2, DoNotDisturb = 3 }

/// <summary>Redis-backed presence tracking (connection lifetime + a user-set manual status override).</summary>
public interface IPresenceStore
{
    /// <summary>Records a new connection. Returns the effective status to broadcast when the user just came online (first connection); null when already online.</summary>
    Task<PresenceStatus?> AddConnectionAsync(Guid userId, string connectionId, CancellationToken ct);

    /// <summary>Removes a connection. Returns true if the user just went offline (last connection dropped).</summary>
    Task<bool> RemoveConnectionAsync(Guid userId, string connectionId, CancellationToken ct);

    /// <summary>Refreshes the presence TTL and re-asserts the effective status (called on client heartbeat).</summary>
    Task HeartbeatAsync(Guid userId, CancellationToken ct);

    /// <summary>Sets (Away/DoNotDisturb) or clears (Online/null → "auto") the user's manual status override. Returns the effective status to broadcast.</summary>
    Task<PresenceStatus> SetManualStatusAsync(Guid userId, PresenceStatus? status, CancellationToken ct);

    /// <summary>Gets current status for a set of users.</summary>
    Task<IReadOnlyDictionary<Guid, PresenceStatus>> GetStatusesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}
