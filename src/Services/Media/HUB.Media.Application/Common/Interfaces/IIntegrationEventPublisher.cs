using HUB.Shared.Contracts;

namespace HUB.Media.Application.Common.Interfaces;

/// <summary>Publishes integration events (via the transactional outbox).</summary>
public interface IIntegrationEventPublisher
{
    /// <summary>Publishes an integration event.</summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct) where TEvent : IntegrationEvent;
}
