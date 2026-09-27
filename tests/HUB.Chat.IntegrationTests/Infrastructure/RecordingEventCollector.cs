using System.Collections.Concurrent;
using HUB.Chat.Application.Common.Interfaces;
using HUB.Shared.Contracts;

namespace HUB.Chat.IntegrationTests.Infrastructure;

/// <summary>Collects the integration events published during a request, so a test can assert on them.</summary>
/// <remarks>
/// Registered as a singleton in <see cref="ApiFactory"/>, which means it outlives any one request and
/// accumulates across a whole test class. A test that asserts on a count should therefore either
/// <see cref="Clear"/> first or filter by an id it owns — the shared instance is the price of being able
/// to read the events after the response has come back.
/// </remarks>
public sealed class RecordingEventCollector : IIntegrationEventPublisher
{
    private readonly ConcurrentQueue<IntegrationEvent> _published = new();

    /// <summary>Every event published so far, in order.</summary>
    public IReadOnlyList<IntegrationEvent> Published => [.. _published];

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct) where TEvent : IntegrationEvent
    {
        _published.Enqueue(@event);
        return Task.CompletedTask;
    }

    /// <summary>Returns every published event of one type.</summary>
    /// <typeparam name="TEvent">Event type to filter by.</typeparam>
    /// <returns>The matching events, in publish order.</returns>
    public IReadOnlyList<TEvent> OfType<TEvent>() where TEvent : IntegrationEvent =>
        [.. _published.OfType<TEvent>()];

    /// <summary>Discards everything recorded so far.</summary>
    public void Clear() => _published.Clear();
}
