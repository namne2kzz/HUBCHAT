namespace HUB.Shared.Contracts.Events;

/// <summary>Published when a channel is archived or reopened; consumed by realtime-service to update clients.</summary>
/// <param name="ChannelId">The channel.</param>
/// <param name="IsArchived">New archived state.</param>
public sealed record ChannelArchivedChanged(Guid ChannelId, bool IsArchived) : IntegrationEvent;
