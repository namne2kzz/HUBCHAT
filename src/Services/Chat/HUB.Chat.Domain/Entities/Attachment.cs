using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Enums;

namespace HUB.Chat.Domain.Entities;

/// <summary>A file/image/video attached to a <see cref="Message"/> (part of the Message aggregate). Content lives in MinIO; this row holds the reference + metadata.</summary>
public sealed class Attachment : Entity
{
    private Attachment() { } // EF

    internal Attachment(Guid messageId, AttachmentKind kind, string url, string name, long size, string mime, int? width, int? height)
    {
        MessageId = messageId;
        Kind      = kind;
        Url       = url;
        Name      = name;
        Size      = size;
        Mime      = mime;
        Width     = width;
        Height    = height;
    }

    /// <summary>Owning message id.</summary>
    public Guid MessageId { get; private set; }

    /// <summary>Attachment kind (File/Image/Video).</summary>
    public AttachmentKind Kind { get; private set; }

    /// <summary>Storage key / URL in MinIO.</summary>
    public string Url { get; private set; } = default!;

    /// <summary>Original file name.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Size in bytes.</summary>
    public long Size { get; private set; }

    /// <summary>MIME type.</summary>
    public string Mime { get; private set; } = default!;

    /// <summary>Pixel width (images/videos); null otherwise.</summary>
    public int? Width { get; private set; }

    /// <summary>Pixel height (images/videos); null otherwise.</summary>
    public int? Height { get; private set; }
}
