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

    /// <summary>Marks the object scanned with the given result.</summary>
    /// <param name="clean">True if clean; false if infected.</param>
    public void MarkScanned(bool clean) => ScanStatus = clean ? ScanStatus.Clean : ScanStatus.Infected;

    /// <summary>True when the object may be downloaded.</summary>
    public bool IsDownloadable => ScanStatus == ScanStatus.Clean;
}
