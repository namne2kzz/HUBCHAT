using MediatR;

namespace HUB.Chat.Application.Channels.Commands.JoinChannel;

/// <summary>Adds the acting user to a channel as a member.</summary>
/// <param name="ChannelId">Channel to join.</param>
/// <param name="ActingUserId">User joining.</param>
public sealed record JoinChannelCommand(Guid ChannelId, Guid ActingUserId) : IRequest;
