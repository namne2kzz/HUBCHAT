namespace HUB.Chat.Domain.Enums;

/// <summary>Kind of a message attachment (drives preview/rendering).</summary>
public enum AttachmentKind
{
    /// <summary>An arbitrary file (download).</summary>
    File  = 0,
    /// <summary>An image (inline preview).</summary>
    Image = 1,
    /// <summary>A video (inline player).</summary>
    Video = 2,
}
