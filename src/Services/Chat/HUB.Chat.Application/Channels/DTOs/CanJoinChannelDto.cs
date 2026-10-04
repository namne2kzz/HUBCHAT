namespace HUB.Chat.Application.Channels.DTOs;

/// <summary>Whether a user may subscribe to a channel's realtime stream (internal API, read by realtime-service).</summary>
/// <param name="Allowed">True when the channel is public or the user is a member.</param>
public sealed record CanJoinChannelDto(bool Allowed);
