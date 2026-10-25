using HUB.Chat.Domain.Enums;

namespace HUB.Chat.Application.Channels.DTOs;

/// <summary>Read model for a channel (including optional DASHBOARD link for discussion threads).</summary>
public sealed record ChannelDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string Slug,
    ChannelType Type,
    string Topic,
    bool IsPrivate,
    bool IsArchived,
    int MemberCount,
    DateTime CreatedAt,
    LinkedResourceType? LinkType,
    string? LinkExternalKey,
    string LinkUrl,
    /// <summary>True when the acting user is a member of this channel.</summary>
    bool IsMember = false,
    /// <summary>For DMs: the other participant's user id (relative to the acting user). Null for non-DM channels.</summary>
    Guid? OtherUserId = null,
    /// <summary>The acting user's role in this channel, or null when they are not a member.</summary>
    ChannelMemberRole? MyRole = null,
    /// <summary>
    /// Unread top-level messages for the acting user (others' messages after their read point), capped at
    /// <c>ListChannelsHandler.UnreadCap</c>. Only computed by the channel list; 0 elsewhere and for non-members.
    /// </summary>
    int UnreadCount = 0,
    /// <summary>
    /// For a group DM: every member's user id, so the client can name the group from its people.
    /// Empty for other channel kinds (they are named by <c>Name</c> or <c>OtherUserId</c>).
    /// </summary>
    IReadOnlyList<Guid>? GroupMemberIds = null);
