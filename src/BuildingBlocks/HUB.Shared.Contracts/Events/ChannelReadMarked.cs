namespace HUB.Shared.Contracts.Events;

/// <summary>
/// Published by chat-service when a user's read point in a channel moves forward. Consumed by
/// realtime-service to clear that channel's unread badge on the user's other tabs and devices.
/// </summary>
/// <param name="ChannelId">The channel read.</param>
/// <param name="UserId">The reader.</param>
/// <param name="ReadAt">UTC point in time read up to.</param>
/// <param name="IsDirect">True for a DM/GroupDm — drives the "Seen" read receipt shown to the other participant (CF-15); false for channels, where receipts would be noise.</param>
public sealed record ChannelReadMarked(Guid ChannelId, Guid UserId, DateTime ReadAt, bool IsDirect = false) : IntegrationEvent;
