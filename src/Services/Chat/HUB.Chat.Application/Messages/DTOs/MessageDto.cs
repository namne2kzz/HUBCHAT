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
    DateTime CreatedAt);
