using MediatR;

namespace HUB.Chat.Application.Channels.Commands.ArchiveChannel;

/// <summary>Archives or reopens a channel.</summary>
/// <param name="ChannelId">Target channel.</param>
/// <param name="Archive">True to archive, false to reopen.</param>
/// <param name="ActingUserId">
/// User performing it — must be a channel Owner/Admin (or hold ManageChannels). Null only for the internal
/// API (sprint close), which is a trusted service-to-service call.
/// </param>
public sealed record ArchiveChannelCommand(Guid ChannelId, bool Archive = true, Guid? ActingUserId = null) : IRequest;
