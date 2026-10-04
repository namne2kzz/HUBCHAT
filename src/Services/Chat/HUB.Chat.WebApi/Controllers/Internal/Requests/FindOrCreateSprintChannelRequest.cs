namespace HUB.Chat.WebApi.Controllers.Internal.Requests;

/// <summary>Payload for the find-or-create sprint channel endpoint.</summary>
/// <param name="WorkspaceId">Workspace (= DASHBOARD repository) of the sprint.</param>
/// <param name="SprintId">DASHBOARD sprint id the channel is linked to.</param>
/// <param name="SprintName">Sprint name, used for the channel name and topic.</param>
/// <param name="CreatorUserId">User who becomes the channel owner if it is created.</param>
public sealed record FindOrCreateSprintChannelRequest(
    Guid   WorkspaceId,
    Guid   SprintId,
    string SprintName,
    Guid   CreatorUserId);
