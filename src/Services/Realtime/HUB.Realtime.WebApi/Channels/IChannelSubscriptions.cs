namespace HUB.Realtime.WebApi.Channels;

/// <summary>
/// Tracks which connections are subscribed to which channel's realtime group, and the user behind each
/// connection. SignalR exposes no way to enumerate a group, so this Redis-backed index is what lets the
/// service revoke non-members when a public channel is made private (there is otherwise no record of who
/// is watching).
/// </summary>
public interface IChannelSubscriptions
{
    /// <summary>Records that a connection joined a channel's group.</summary>
    /// <param name="channelId">The channel.</param>
    /// <param name="connectionId">The SignalR connection.</param>
    /// <param name="userId">The user behind the connection.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(Guid channelId, string connectionId, Guid userId, CancellationToken ct);

    /// <summary>Removes a single connection from a channel's group index.</summary>
    /// <param name="channelId">The channel.</param>
    /// <param name="connectionId">The connection.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RemoveAsync(Guid channelId, string connectionId, CancellationToken ct);

    /// <summary>Removes a connection from every channel it was subscribed to (on disconnect).</summary>
    /// <param name="connectionId">The connection.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RemoveConnectionAsync(string connectionId, CancellationToken ct);

    /// <summary>Returns the live (connectionId, userId) pairs subscribed to a channel.</summary>
    /// <param name="channelId">The channel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Subscribers; empty when none.</returns>
    Task<IReadOnlyList<(string ConnectionId, Guid UserId)>> GetSubscribersAsync(Guid channelId, CancellationToken ct);
}
