using HUB.Shared.Contracts;

namespace HUB.Chat.Application.Common.Interfaces;

/// <summary>Publishes integration events to the bus. With the transactional outbox, delivery is tied to SaveChanges.</summary>
public interface IIntegrationEventPublisher
{
    /// <summary>Publishes an integration event.</summary>
    /// <typeparam name="TEvent">Event type.</typeparam>
    /// <param name="event">The event instance.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct) where TEvent : IntegrationEvent;
}
