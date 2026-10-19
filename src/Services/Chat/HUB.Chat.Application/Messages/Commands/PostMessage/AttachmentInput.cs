using HUB.Chat.Domain.Enums;

namespace HUB.Chat.Application.Messages.Commands.PostMessage;

/// <summary>
/// An attachment the client already uploaded (via the Media service) and now attaches to a message.
/// Chat stores a snapshot of this metadata plus the <paramref name="FileId"/> reference; the actual bytes
/// live in MinIO and a presigned download URL is resolved at render time, never stored here.
/// </summary>
/// <param name="FileId">The Media-service file id (its upload must already be completed).</param>
/// <param name="Kind">File / Image / Video — drives how the client renders it.</param>
/// <param name="Name">Original file name.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="Mime">MIME type.</param>
/// <param name="Width">Pixel width for images/videos; null otherwise.</param>
/// <param name="Height">Pixel height for images/videos; null otherwise.</param>
public sealed record AttachmentInput(
    Guid FileId,
    AttachmentKind Kind,
    string Name,
    long Size,
    string Mime,
    int? Width = null,
    int? Height = null);
