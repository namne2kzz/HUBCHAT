namespace HUB.Media.Application.Common.Interfaces;

/// <summary>Abstraction over S3-compatible object storage (MinIO). Swap for AWS S3/Azure later without touching domain.</summary>
public interface IObjectStorage
{
    /// <summary>Ensures a bucket exists (creates it if missing).</summary>
    Task EnsureBucketAsync(string bucket, CancellationToken ct);

    /// <summary>Creates a presigned PUT URL for direct client upload.</summary>
    Task<string> CreatePresignedPutUrlAsync(string bucket, string key, TimeSpan ttl, CancellationToken ct);

    /// <summary>Creates a presigned GET URL for direct client download.</summary>
    Task<string> CreatePresignedGetUrlAsync(string bucket, string key, TimeSpan ttl, CancellationToken ct);
}
