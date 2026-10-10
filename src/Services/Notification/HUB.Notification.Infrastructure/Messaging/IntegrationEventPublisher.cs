using HUB.Notification.Application.Common.Interfaces;
using HUB.Shared.Contracts;
using MassTransit;

namespace HUB.Notification.Infrastructure.Messaging;

/// <summary>
/// Publishes through MassTransit. Inside a consumer, <see cref="IPublishEndpoint"/> is the consume context,
/// which the endpoint's transactional outbox buffers until the consumer's transaction commits.
/// </summary>
/// <param name="publishEndpoint">The (outbox-aware) publish endpoint.</param>
public sealed class IntegrationEventPublisher(IPublishEndpoint publishEndpoint) : IIntegrationEventPublisher
{
    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct) where TEvent : IntegrationEvent =>
        publishEndpoint.Publish(@event, ct);
}
