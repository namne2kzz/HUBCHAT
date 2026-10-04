namespace HUB.Media.Application.Files.DTOs;

/// <summary>A presigned download URL.</summary>
/// <param name="Url">Presigned GET URL.</param>
/// <param name="ExpiresInSeconds">TTL of the URL.</param>
public sealed record DownloadUrlDto(string Url, int ExpiresInSeconds);
