namespace HUB.Shared.Messaging;

/// <summary>Connection settings for HUB's own RabbitMQ broker.</summary>
public sealed class RabbitMqOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "RabbitMq";

    /// <summary>Broker host name (e.g. "rabbitmq" inside Docker).</summary>
    public string Host { get; init; } = "rabbitmq";

    /// <summary>Virtual host. HUB uses its own broker so "/" is fine.</summary>
    public string VirtualHost { get; init; } = "/";

    /// <summary>Broker username.</summary>
    public string User { get; init; } = "hub";

    /// <summary>Broker password.</summary>
    public string Password { get; init; } = string.Empty;
}
