using HUB.Chat.Application.Channels.DTOs;
using MediatR;

namespace HUB.Chat.Application.Channels.Commands.OpenDirectMessage;

/// <summary>
/// Finds or creates the canonical direct-message channel between the acting user and a target user.
/// A DM is identified by having exactly those two members (regardless of workspace), so both
/// participants always resolve to the same channel.
/// </summary>
/// <param name="WorkspaceId">Workspace used when a new DM channel must be created.</param>
/// <param name="TargetUserId">The other participant.</param>
/// <param name="ActingUserId">The caller (from the JWT).</param>
public sealed record OpenDirectMessageCommand(Guid WorkspaceId, Guid TargetUserId, Guid ActingUserId)
    : IRequest<ChannelDto>;
