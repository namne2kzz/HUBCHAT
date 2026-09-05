using HUB.Chat.Application.Channels.DTOs;
using HUB.Chat.Domain.Enums;
using MediatR;

namespace HUB.Chat.Application.Channels.Commands.OpenLinkedThread;

/// <summary>Opens (find-or-create) a discussion thread channel linked to a DASHBOARD resource (e.g. work item).</summary>
/// <param name="WorkspaceId">Owning workspace (= DASHBOARD repository).</param>
/// <param name="LinkType">Kind of linked resource.</param>
/// <param name="ExternalId">Resource id in DASHBOARD.</param>
/// <param name="ExternalKey">Human-readable key (e.g. "DASH-142").</param>
/// <param name="Title">Title for the thread (usually the work-item title).</param>
/// <param name="Url">Deep-link URL back to DASHBOARD.</param>
/// <param name="ActingUserId">User opening the thread.</param>
public sealed record OpenLinkedThreadCommand(
    Guid WorkspaceId,
    LinkedResourceType LinkType,
    Guid ExternalId,
    string ExternalKey,
    string Title,
    string Url,
    Guid ActingUserId) : IRequest<ChannelDto>;
