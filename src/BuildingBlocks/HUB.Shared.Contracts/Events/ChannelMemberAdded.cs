namespace HUB.Shared.Contracts.Events;

/// <summary>
/// Published by chat-service when a user becomes a member of a channel — added by a manager or by
/// DASHBOARD sprint sync. Consumed by realtime-service to push the channel to the user straight away,
/// the mirror of <see cref="ChannelMemberRemoved"/>.
/// </summary>
/// <remarks>
/// Without it, someone added to a private channel saw nothing until they reloaded: the sidebar is
/// built from the channel list fetched on load, and no live event told them a new one existed.
/// </remarks>
/// <param name="ChannelId">The channel the user was added to.</param>
/// <param name="UserId">The user who is now a member.</param>
public sealed record ChannelMemberAdded(Guid ChannelId, Guid UserId) : IntegrationEvent;
