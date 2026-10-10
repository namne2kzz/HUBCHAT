using HUB.Shared.Contracts;

namespace HUB.Notification.Application.Common.Interfaces;

/// <summary>
/// Publishes integration events. Inside a consumer the publish is held by the transactional outbox and
/// only leaves when the consumer's database transaction commits.
/// </summary>
public interface IIntegrationEventPublisher
{
    /// <summary>Publishes an integration event (delivered after commit).</summary>
    /// <typeparam name="TEvent">Concrete event type (kept so routing uses it, not the base type).</typeparam>
    /// <param name="event">The event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the event is queued.</returns>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct) where TEvent : IntegrationEvent;
}
