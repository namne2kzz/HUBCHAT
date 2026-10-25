namespace HUB.Chat.WebApi.Controllers.Channels.Requests;

/// <summary>Body for opening a group direct message.</summary>
/// <param name="WorkspaceId">Workspace used if a new group channel must be created.</param>
/// <param name="MemberIds">The other participants (2–7 users; the caller is added automatically).</param>
public sealed record OpenGroupDmRequest(Guid WorkspaceId, IReadOnlyList<Guid> MemberIds);
