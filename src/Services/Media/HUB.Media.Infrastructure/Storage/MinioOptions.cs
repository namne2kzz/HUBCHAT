namespace HUB.Media.Infrastructure.Storage;

/// <summary>MinIO connection settings.</summary>
public sealed class MinioOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Minio";

    /// <summary>Endpoint host:port. NOTE: presigned URLs use this host, so it must be reachable by the CLIENT (browser).</summary>
    public string Endpoint { get; init; } = "localhost:9000";

    /// <summary>Access key.</summary>
    public string AccessKey { get; init; } = string.Empty;

    /// <summary>Secret key.</summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>Whether to use TLS.</summary>
    public bool UseSsl { get; init; }
}
