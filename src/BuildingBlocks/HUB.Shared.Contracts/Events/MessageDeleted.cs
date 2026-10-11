namespace HUB.Shared.Contracts.Events;

/// <summary>Published by chat-service when a message is deleted; consumed by realtime-service.</summary>
/// <param name="MessageId">The deleted message.</param>
/// <param name="ChannelId">Channel of the message (the SignalR group to push to).</param>
public sealed record MessageDeleted(Guid MessageId, Guid ChannelId) : IntegrationEvent;
