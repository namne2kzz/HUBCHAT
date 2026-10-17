namespace HUB.Shared.Contracts.Events;

/// <summary>Published when a message is pinned or unpinned; consumed by realtime-service to update open clients.</summary>
/// <param name="MessageId">The message.</param>
/// <param name="ChannelId">Channel of the message (the SignalR group to push to).</param>
/// <param name="IsPinned">New pin state.</param>
public sealed record MessagePinChanged(Guid MessageId, Guid ChannelId, bool IsPinned) : IntegrationEvent;
