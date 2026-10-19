namespace HUB.Shared.Contracts.Events;

/// <summary>
/// Published by chat-service when a channel is switched from public to private. Consumed by
/// realtime-service to drop every live subscriber who is not a member — a public channel let anyone
/// watch, and making it private must take effect immediately, not on the next reconnect.
/// </summary>
/// <param name="ChannelId">The channel that became private.</param>
/// <param name="MemberIds">The channel's current members — everyone else watching is revoked.</param>
public sealed record ChannelMadePrivate(Guid ChannelId, IReadOnlyCollection<Guid> MemberIds) : IntegrationEvent;
