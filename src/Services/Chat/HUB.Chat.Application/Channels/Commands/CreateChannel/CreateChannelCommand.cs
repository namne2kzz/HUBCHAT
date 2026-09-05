using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Domain.Enums;
using MediatR;

namespace HUB.Chat.Application.Channels.Commands.CreateChannel;

/// <summary>Creates a channel in a workspace; the acting user becomes its owner.</summary>
/// <param name="WorkspaceId">Owning workspace (= DASHBOARD repository).</param>
/// <param name="Name">Display name.</param>
/// <param name="Type">Channel kind.</param>
/// <param name="Topic">Optional topic.</param>
/// <param name="ActingUserId">User creating the channel (from the JWT).</param>
public sealed record CreateChannelCommand(
    Guid WorkspaceId,
    string Name,
    ChannelType Type,
    string Topic,
    Guid ActingUserId) : IRequest<ChannelDto>;
