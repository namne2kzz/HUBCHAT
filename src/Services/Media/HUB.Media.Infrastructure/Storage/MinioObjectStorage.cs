using HUB.Media.Application.Common.Interfaces;
using Minio;
using Minio.DataModel.Args;

namespace HUB.Media.Infrastructure.Storage;

/// <summary>MinIO implementation of <see cref="IObjectStorage"/> (S3-compatible presigned URLs).</summary>
/// <param name="client">Configured MinIO client.</param>
public sealed class MinioObjectStorage(IMinioClient client) : IObjectStorage
{
    /// <inheritdoc />
    public async Task EnsureBucketAsync(string bucket, CancellationToken ct)
    {
        var exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
        if (!exists)
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
    }

    /// <inheritdoc />
    public Task<string> CreatePresignedPutUrlAsync(string bucket, string key, TimeSpan ttl, CancellationToken ct) =>
        client.PresignedPutObjectAsync(new PresignedPutObjectArgs()
            .WithBucket(bucket).WithObject(key).WithExpiry((int)ttl.TotalSeconds));

    /// <inheritdoc />
    public Task<string> CreatePresignedGetUrlAsync(string bucket, string key, TimeSpan ttl, CancellationToken ct) =>
        client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(bucket).WithObject(key).WithExpiry((int)ttl.TotalSeconds));
}
