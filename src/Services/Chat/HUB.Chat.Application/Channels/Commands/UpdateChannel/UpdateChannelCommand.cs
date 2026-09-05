using HUB.Chat.Application.Channels.DTOs;
using MediatR;

namespace HUB.Chat.Application.Channels.Commands.UpdateChannel;

/// <summary>Updates a channel's name and/or topic. Caller must be Owner or Admin.</summary>
/// <param name="ChannelId">Channel to update.</param>
/// <param name="Name">New name (null = keep current).</param>
/// <param name="Topic">New topic (null = keep current).</param>
/// <param name="ActingUserId">Caller performing the update.</param>
public sealed record UpdateChannelCommand(Guid ChannelId, string? Name, string? Topic, Guid ActingUserId)
    : IRequest<ChannelDto>;
