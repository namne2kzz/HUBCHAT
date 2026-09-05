using HUB.Chat.Application.Channels.DTOs;
using MediatR;

namespace HUB.Chat.Application.Channels.Queries.GetChannel;

/// <summary>Gets a single channel by id, if the acting user may see it.</summary>
/// <param name="ChannelId">Channel to fetch.</param>
/// <param name="ActingUserId">The requesting user.</param>
public sealed record GetChannelQuery(Guid ChannelId, Guid ActingUserId) : IRequest<ChannelDto>;
