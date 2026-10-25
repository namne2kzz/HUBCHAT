using HUB.Chat.Application.Channels.DTOs;
using MediatR;

namespace HUB.Chat.Application.Channels.Commands.OpenGroupDm;

/// <summary>
/// Finds or creates a group direct message among the acting user and 2–7 other people (3–8 total).
/// Identified by its exact member set regardless of workspace, so reopening the same group returns the
/// same channel instead of making a duplicate. Changing who is in the group makes a new group (Slack-style).
/// </summary>
/// <param name="WorkspaceId">Workspace used when a new group channel must be created.</param>
/// <param name="MemberIds">The other participants (excluding the caller); 2–7 distinct users.</param>
/// <param name="ActingUserId">The caller (from the JWT); always a member.</param>
public sealed record OpenGroupDmCommand(
    Guid WorkspaceId,
    IReadOnlyList<Guid> MemberIds,
    Guid ActingUserId) : IRequest<ChannelDto>;
