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
    ChannelMemberRole? MyRole = null);
