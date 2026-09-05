using HUB.Chat.Application.Channels.DTOs;
using MediatR;

namespace HUB.Chat.Application.Channels.Commands.FindOrCreateSprintChannel;

/// <summary>
/// Finds an existing Private channel linked to the given sprint, or creates one if none exists.
/// Called by DASHBOARD backend (M2M) via the internal API — the creator becomes channel owner.
/// </summary>
/// <param name="WorkspaceId">DASHBOARD repository ID (= HUB workspace).</param>
/// <param name="SprintId">DASHBOARD sprint ID used as the link key.</param>
/// <param name="SprintName">Human-readable sprint name used to derive the channel name.</param>
/// <param name="CreatorUserId">User ID of the sprint creator — added as channel owner.</param>
public sealed record FindOrCreateSprintChannelCommand(
    Guid   WorkspaceId,
    Guid   SprintId,
    string SprintName,
    Guid   CreatorUserId) : IRequest<ChannelDto>;
