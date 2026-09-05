using HUB.Chat.Domain.Enums;

namespace HUB.Chat.WebApi.Controllers.Channels.Requests;

/// <summary>Body for creating a channel.</summary>
/// <param name="WorkspaceId">Owning workspace (= DASHBOARD repository).</param>
/// <param name="Name">Display name.</param>
/// <param name="Type">Channel kind.</param>
/// <param name="Topic">Optional topic.</param>
public sealed record CreateChannelRequest(Guid WorkspaceId, string Name, ChannelType Type, string? Topic);

/// <summary>Body for marking a channel read.</summary>
/// <param name="ReadAt">Point in time read up to; null = now.</param>
public sealed record MarkReadRequest(DateTime? ReadAt);

/// <summary>Body for updating a channel's name and/or topic.</summary>
/// <param name="Name">New display name (null = keep current).</param>
/// <param name="Topic">New topic (null = keep current).</param>
public sealed record UpdateChannelRequest(string? Name, string? Topic);

/// <summary>Body for switching a channel between Public and Private (owner only).</summary>
/// <param name="IsPrivate">True to make the channel private; false for public.</param>
public sealed record ChangeVisibilityRequest(bool IsPrivate);

/// <summary>Body for transferring channel ownership to another member (owner only).</summary>
/// <param name="NewOwnerUserId">The member to promote to Owner.</param>
public sealed record TransferOwnershipRequest(Guid NewOwnerUserId);

/// <summary>Body for adding a user to a channel by the calling admin/owner.</summary>
/// <param name="UserId">User to add.</param>
public sealed record AddChannelMemberRequest(Guid UserId);

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
