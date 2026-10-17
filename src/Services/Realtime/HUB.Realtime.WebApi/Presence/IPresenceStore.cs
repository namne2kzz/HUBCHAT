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

    /// <summary>
    /// Sets the caller's custom status (CF-19). An empty <see cref="CustomStatus.Text"/> and emoji clears it.
    /// When <see cref="CustomStatus.ExpiresAt"/> is in the future the status auto-clears at that time.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="status">The desired custom status; empty text+emoji clears.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored status, or null when it was cleared (or already expired).</returns>
    Task<CustomStatus?> SetCustomStatusAsync(Guid userId, CustomStatus status, CancellationToken ct);

    /// <summary>Gets the current (non-expired) custom status for a set of users (CF-19).</summary>
    /// <param name="userIds">Users to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A map of user id → custom status, omitting users with none.</returns>
    Task<IReadOnlyDictionary<Guid, CustomStatus>> GetCustomStatusesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}
