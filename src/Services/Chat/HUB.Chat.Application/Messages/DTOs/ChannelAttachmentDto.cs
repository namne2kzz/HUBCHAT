using HUB.Chat.Domain.Enums;

namespace HUB.Chat.Application.Messages.DTOs;

/// <summary>
/// An attachment surfaced in a channel's Files/Media panel (CF-5): the attachment itself plus the
/// message it belongs to, so the panel can jump to it and show who shared it when.
/// </summary>
/// <param name="Id">Attachment id.</param>
/// <param name="MessageId">Owning message id (used to jump to it in the timeline).</param>
/// <param name="AuthorId">User who shared it.</param>
/// <param name="Kind">File / Image / Video.</param>
/// <param name="FileId">Media-service file id (to resolve a presigned download URL).</param>
/// <param name="Name">Original file name.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="Mime">MIME type.</param>
/// <param name="Width">Pixel width for media; null otherwise.</param>
/// <param name="Height">Pixel height for media; null otherwise.</param>
/// <param name="CreatedAt">When the owning message was posted (the panel's sort key).</param>
public sealed record ChannelAttachmentDto(
    Guid Id,
    Guid MessageId,
    Guid AuthorId,
    AttachmentKind Kind,
    string FileId,
    string Name,
    long Size,
    string Mime,
    int? Width,
    int? Height,
    DateTime CreatedAt);
