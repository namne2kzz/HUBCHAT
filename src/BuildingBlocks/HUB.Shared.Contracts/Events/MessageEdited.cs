namespace HUB.Shared.Contracts.Events;

/// <summary>Published by chat-service when a message's body is edited; consumed by realtime-service.</summary>
/// <param name="MessageId">The edited message.</param>
/// <param name="ChannelId">Channel of the message (the SignalR group to push to).</param>
/// <param name="Body">The new body.</param>
/// <param name="EditedAt">UTC time of the edit.</param>
public sealed record MessageEdited(Guid MessageId, Guid ChannelId, string Body, DateTime EditedAt) : IntegrationEvent;
