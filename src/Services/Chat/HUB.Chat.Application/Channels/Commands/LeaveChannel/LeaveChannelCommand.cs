using MediatR;

namespace HUB.Chat.Application.Channels.Commands.LeaveChannel;

/// <summary>Removes the acting user from a channel.</summary>
/// <param name="ChannelId">The channel to leave.</param>
/// <param name="ActingUserId">User leaving.</param>
public sealed record LeaveChannelCommand(Guid ChannelId, Guid ActingUserId) : IRequest;
