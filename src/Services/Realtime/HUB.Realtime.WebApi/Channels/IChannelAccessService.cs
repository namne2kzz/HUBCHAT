namespace HUB.Realtime.WebApi.Channels;

/// <summary>
/// Decides whether a connection may subscribe to a channel's realtime stream, asking chat-service and
/// caching the answer.
/// </summary>
public interface IChannelAccessService
{
    /// <summary>Checks whether a user may join a channel's SignalR group.</summary>
    /// <param name="channelId">Channel the user wants to join.</param>
    /// <param name="userId">The user asking to join.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the channel is public or the user is a member.</returns>
    Task<bool> CanJoinAsync(Guid channelId, Guid userId, CancellationToken ct);

    /// <summary>Drops the cached answer for a user and channel, so the next join asks chat-service again.</summary>
    /// <param name="channelId">The channel.</param>
    /// <param name="userId">The user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the entry is gone.</returns>
    Task InvalidateAsync(Guid channelId, Guid userId, CancellationToken ct);
}
