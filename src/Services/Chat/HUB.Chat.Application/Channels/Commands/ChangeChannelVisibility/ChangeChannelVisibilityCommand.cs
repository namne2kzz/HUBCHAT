using HUB.Chat.Application.Channels.DTOs;
using MediatR;

namespace HUB.Chat.Application.Channels.Commands.ChangeChannelVisibility;

/// <summary>Switches a channel between Public and Private. Caller must be the channel Owner.</summary>
/// <param name="ChannelId">Channel to update.</param>
/// <param name="IsPrivate">True to make the channel private; false for public.</param>
/// <param name="ActingUserId">Caller performing the change.</param>
public sealed record ChangeChannelVisibilityCommand(Guid ChannelId, bool IsPrivate, Guid ActingUserId)
    : IRequest<ChannelDto>;
