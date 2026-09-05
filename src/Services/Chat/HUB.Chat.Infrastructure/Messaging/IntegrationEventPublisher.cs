using HUB.Chat.Application.Common.Interfaces;
using HUB.Shared.Contracts;
using MassTransit;

namespace HUB.Chat.Infrastructure.Messaging;

/// <summary>Publishes integration events through MassTransit. With the bus outbox, delivery is tied to SaveChanges.</summary>
/// <param name="publishEndpoint">The (outbox-aware) publish endpoint.</param>
public sealed class IntegrationEventPublisher(IPublishEndpoint publishEndpoint) : IIntegrationEventPublisher
{
    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct) where TEvent : IntegrationEvent =>
        publishEndpoint.Publish(@event, ct);
}
