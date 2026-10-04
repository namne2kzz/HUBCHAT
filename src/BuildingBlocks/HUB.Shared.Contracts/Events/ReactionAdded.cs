namespace HUB.Shared.Contracts.Events;

/// <summary>Published by chat-service when a user adds a reaction; consumed by realtime-service to update open clients.</summary>
/// <param name="MessageId">The reacted message.</param>
/// <param name="ChannelId">Channel of the message (the SignalR group to push to).</param>
/// <param name="UserId">User who reacted.</param>
/// <param name="Emoji">Emoji shortcode.</param>
public sealed record ReactionAdded(Guid MessageId, Guid ChannelId, Guid UserId, string Emoji) : IntegrationEvent;
