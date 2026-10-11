namespace HUB.Shared.Contracts.Events;

/// <summary>Published by chat-service when a user removes their reaction; consumed by realtime-service to update open clients.</summary>
/// <param name="MessageId">The message the reaction was on.</param>
/// <param name="ChannelId">Channel of the message (the SignalR group to push to).</param>
/// <param name="UserId">User whose reaction was removed.</param>
/// <param name="Emoji">Emoji removed.</param>
public sealed record ReactionRemoved(Guid MessageId, Guid ChannelId, Guid UserId, string Emoji) : IntegrationEvent;
