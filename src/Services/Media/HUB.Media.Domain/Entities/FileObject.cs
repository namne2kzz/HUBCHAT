using HUB.Media.Domain.Common;
using HUB.Media.Domain.Enums;

namespace HUB.Media.Domain.Entities;

/// <summary>Metadata for a file stored in object storage (MinIO). The bytes live in MinIO, not the DB.</summary>
public sealed class FileObject : Entity
{
    private FileObject() { } // EF

    private FileObject(Guid workspaceId, Guid? channelId, string fileName, string contentType, long sizeBytes, string storageKey, Guid uploadedBy)
    {
        WorkspaceId = workspaceId;
        ChannelId   = channelId;
        FileName    = fileName;
        ContentType = contentType;
        SizeBytes   = sizeBytes;
        StorageKey  = storageKey;
        UploadedBy  = uploadedBy;
        ScanStatus  = ScanStatus.Pending;
    }

    /// <summary>Owning workspace (= DASHBOARD repository).</summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>Channel the file is attached to (optional).</summary>
    public Guid? ChannelId { get; private set; }

    /// <summary>Original file name.</summary>
    public string FileName { get; private set; } = default!;

    /// <summary>MIME type.</summary>
    public string ContentType { get; private set; } = default!;

    /// <summary>Size in bytes.</summary>
    public long SizeBytes { get; private set; }

    /// <summary>Object key in MinIO.</summary>
    public string StorageKey { get; private set; } = default!;

    /// <summary>Uploader user id.</summary>
    public Guid UploadedBy { get; private set; }

    /// <summary>Virus-scan status. Download allowed only when Clean.</summary>
    public ScanStatus ScanStatus { get; private set; }

    /// <summary>Bucket for a workspace.</summary>
    public string Bucket => $"ws-{WorkspaceId}";

    /// <summary>Creates pending file metadata and computes its storage key.</summary>
    /// <param name="workspaceId">Owning workspace.</param>
    /// <param name="channelId">Attached channel (optional).</param>
    /// <param name="fileName">Original file name (required).</param>
    /// <param name="contentType">MIME type (required).</param>
    /// <param name="sizeBytes">Declared size (must be &gt; 0).</param>
    /// <param name="uploadedBy">Uploader user id.</param>
    /// <returns>The new file metadata.</returns>
    public static FileObject Create(Guid workspaceId, Guid? channelId, string fileName, string contentType, long sizeBytes, Guid uploadedBy)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new DomainException("File name is required.");
        if (string.IsNullOrWhiteSpace(contentType)) throw new DomainException("Content type is required.");
        if (sizeBytes <= 0) throw new DomainException("File size must be greater than zero.");

        var id = Guid.NewGuid();
        var key = channelId is { } c ? $"channels/{c}/{id}" : $"misc/{id}";
        return new FileObject(workspaceId, channelId, fileName.Trim(), contentType.Trim(), sizeBytes, key, uploadedBy) { Id = id };
    }

    /// <summary>Records a scan result. Verdicts only ever tighten: Pending → Clean/Infected, Clean → Infected.</summary>
    /// <param name="clean">True if clean; false if infected.</param>
    /// <returns>
    /// True only when this call gave the file its first verdict (it was Pending). Callers use it to publish
    /// "uploaded" exactly once — a repeated complete-upload (client retry, double click) returns false and
    /// must not announce the file again.
    /// </returns>
    /// <remarks>
    /// A later infected verdict still closes an already-clean file (e.g. after a signature update). The
    /// reverse is refused: an Infected file never becomes Clean through this method, so a replayed
    /// "complete, assume clean" cannot reopen a file a scan has blocked.
    /// </remarks>
    public bool MarkScanned(bool clean)
    {
        if (ScanStatus == ScanStatus.Pending)
        {
            ScanStatus = clean ? ScanStatus.Clean : ScanStatus.Infected;
            return true;
        }

        if (!clean) ScanStatus = ScanStatus.Infected;
        return false;
    }

    /// <summary>True when the object may be downloaded.</summary>
    public bool IsDownloadable => ScanStatus == ScanStatus.Clean;
}
