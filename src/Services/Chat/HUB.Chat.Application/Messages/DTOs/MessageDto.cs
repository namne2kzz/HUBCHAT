using HUB.Chat.Domain.Enums;

namespace HUB.Chat.Application.Messages.DTOs;

/// <summary>Read model for a message.</summary>
public sealed record MessageDto(
    Guid Id,
    Guid ChannelId,
    Guid? ParentId,
    Guid? ReplyToId,
    Guid? ForwardedFromId,
    Guid AuthorId,
    string Body,
    MessageFormat Format,
    IReadOnlyList<Guid> Mentions,
    IReadOnlyList<ReactionDto> Reactions,
    IReadOnlyList<AttachmentDto> Attachments,
    DateTime? EditedAt,
    DateTime CreatedAt,
    /// <summary>True when the message was deleted — rendered as a "message deleted" tombstone; Body is empty.</summary>
    bool IsDeleted = false,
    /// <summary>True when the message is pinned in its channel.</summary>
    bool IsPinned = false,
    /// <summary>Non-deleted replies in this message's thread. Set only for top-level messages in the channel list; 0 otherwise.</summary>
    int ReplyCount = 0,
    /// <summary>Timestamp of the newest reply; null when there are none. Set only in the channel list.</summary>
    DateTime? LastReplyAt = null);
