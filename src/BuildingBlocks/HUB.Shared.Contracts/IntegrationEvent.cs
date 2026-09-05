namespace HUB.Shared.Contracts;

/// <summary>
/// Base type for all integration events published on HUB's internal RabbitMQ bus.
/// Carries an id and timestamp so consumers can deduplicate (idempotency) and order events.
/// </summary>
public abstract record IntegrationEvent
{
    /// <summary>Unique id of this event occurrence — use as the idempotency key in consumers.</summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>UTC time the event was created.</summary>
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}
