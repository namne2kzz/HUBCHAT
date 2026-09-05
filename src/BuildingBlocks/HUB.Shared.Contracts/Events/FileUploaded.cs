namespace HUB.Shared.Contracts.Events;

/// <summary>Published by media-service after an object is stored and scanned; consumed by chat-service to attach.</summary>
/// <param name="FileId">The stored file id.</param>
/// <param name="WorkspaceId">Owning workspace.</param>
/// <param name="StorageKey">Object key in MinIO/S3.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="SizeBytes">File size in bytes.</param>
public sealed record FileUploaded(
    Guid FileId,
    Guid WorkspaceId,
    string StorageKey,
    string ContentType,
    long SizeBytes) : IntegrationEvent;
