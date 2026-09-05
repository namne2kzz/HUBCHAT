using HUB.Media.Application.Common.Interfaces;
using HUB.Shared.Contracts;
using MassTransit;

namespace HUB.Media.Infrastructure.Messaging;

/// <summary>Publishes integration events through MassTransit (outbox-aware).</summary>
/// <param name="publishEndpoint">The publish endpoint.</param>
public sealed class IntegrationEventPublisher(IPublishEndpoint publishEndpoint) : IIntegrationEventPublisher
{
    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct) where TEvent : IntegrationEvent =>
        publishEndpoint.Publish(@event, ct);
}
