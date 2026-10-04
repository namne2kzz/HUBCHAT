using HUB.Chat.Domain.Enums;

namespace HUB.Chat.WebApi.Controllers.Channels.Requests;

/// <summary>Body for opening a discussion thread linked to a DASHBOARD resource.</summary>
/// <param name="WorkspaceId">Owning workspace (= DASHBOARD repository).</param>
/// <param name="LinkType">Kind of linked resource.</param>
/// <param name="ExternalId">Resource id in DASHBOARD.</param>
/// <param name="ExternalKey">Human-readable key (e.g. "DASH-142").</param>
/// <param name="Title">Title for the thread.</param>
/// <param name="Url">Deep-link URL back to DASHBOARD.</param>
public sealed record OpenLinkedThreadRequest(
    Guid WorkspaceId,
    HUB.Chat.Domain.Enums.LinkedResourceType LinkType,
    Guid ExternalId,
    string ExternalKey,
    string? Title,
    string? Url);
