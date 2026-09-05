namespace HUB.Media.Application.Files.DTOs;

/// <summary>Result of requesting an upload ticket.</summary>
/// <param name="FileId">The created file metadata id.</param>
/// <param name="UploadUrl">Presigned PUT URL to upload the bytes directly to MinIO.</param>
/// <param name="StorageKey">Object key.</param>
/// <param name="ExpiresInSeconds">TTL of the URL.</param>
public sealed record UploadTicketDto(Guid FileId, string UploadUrl, string StorageKey, int ExpiresInSeconds);

/// <summary>A presigned download URL.</summary>
/// <param name="Url">Presigned GET URL.</param>
/// <param name="ExpiresInSeconds">TTL of the URL.</param>
public sealed record DownloadUrlDto(string Url, int ExpiresInSeconds);
