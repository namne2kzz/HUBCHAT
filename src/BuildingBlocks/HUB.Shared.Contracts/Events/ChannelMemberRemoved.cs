namespace HUB.Shared.Contracts.Events;

/// <summary>
/// Published by chat-service when a user stops being a member of a channel — removed by a manager,
/// removed by DASHBOARD sprint sync, or left on their own. Consumed by realtime-service to revoke the
/// user's live subscription immediately rather than waiting for a reconnect or cache expiry.
/// </summary>
/// <param name="ChannelId">The channel the user was removed from.</param>
/// <param name="UserId">The user who is no longer a member.</param>
public sealed record ChannelMemberRemoved(Guid ChannelId, Guid UserId) : IntegrationEvent;
