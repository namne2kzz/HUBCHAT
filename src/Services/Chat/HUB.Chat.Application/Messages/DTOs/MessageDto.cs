using HUB.Chat.Domain.Enums;

namespace HUB.Chat.Application.Messages.DTOs;

/// <summary>A single reaction summary.</summary>
/// <param name="Emoji">Emoji shortcode.</param>
/// <param name="UserId">User who reacted.</param>
public sealed record ReactionDto(string Emoji, Guid UserId);

/// <summary>A message attachment (file/image/video).</summary>
public sealed record AttachmentDto(
    Guid Id, AttachmentKind Kind, string Url, string Name, long Size, string Mime, int? Width, int? Height);

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
