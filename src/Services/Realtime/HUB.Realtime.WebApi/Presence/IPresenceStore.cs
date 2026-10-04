namespace HUB.Realtime.WebApi.Presence;

/// <summary>Redis-backed presence tracking (connection lifetime + a user-set manual status override).</summary>
public interface IPresenceStore
{
    /// <summary>Records a new connection. Returns the effective status to broadcast when the user just came online (first connection); null when already online.</summary>
    Task<PresenceStatus?> AddConnectionAsync(Guid userId, string connectionId, CancellationToken ct);

    /// <summary>
    /// Returns the user's live connection ids across all realtime instances (may include a connection that
    /// died without a clean disconnect, until its TTL lapses).
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Connection ids; empty when the user is offline.</returns>
    Task<IReadOnlyList<string>> GetConnectionsAsync(Guid userId, CancellationToken ct);

    /// <summary>Removes a connection. Returns true if the user just went offline (last connection dropped).</summary>
    Task<bool> RemoveConnectionAsync(Guid userId, string connectionId, CancellationToken ct);

    /// <summary>Refreshes the presence TTL and re-asserts the effective status (called on client heartbeat).</summary>
    Task HeartbeatAsync(Guid userId, CancellationToken ct);

    /// <summary>Sets (Away/DoNotDisturb) or clears (Online/null → "auto") the user's manual status override. Returns the effective status to broadcast.</summary>
    Task<PresenceStatus> SetManualStatusAsync(Guid userId, PresenceStatus? status, CancellationToken ct);

    /// <summary>Gets current status for a set of users.</summary>
    Task<IReadOnlyDictionary<Guid, PresenceStatus>> GetStatusesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}
