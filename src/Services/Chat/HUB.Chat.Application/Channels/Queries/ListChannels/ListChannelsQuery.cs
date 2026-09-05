using HUB.Chat.Application.Channels.DTOs;
using MediatR;

namespace HUB.Chat.Application.Channels.Queries.ListChannels;

/// <summary>Lists channels in a workspace visible to the acting user (public, or private ones they belong to).</summary>
/// <param name="WorkspaceId">Workspace to list.</param>
/// <param name="ActingUserId">The requesting user.</param>
public sealed record ListChannelsQuery(Guid WorkspaceId, Guid ActingUserId) : IRequest<IReadOnlyList<ChannelDto>>;
