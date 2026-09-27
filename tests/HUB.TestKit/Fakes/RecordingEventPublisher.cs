using HUB.Chat.Application.Common.Interfaces;
using HUB.Shared.Contracts;

namespace HUB.TestKit.Fakes;

/// <summary>
/// Records the integration events a handler publishes, so a test can assert on them.
/// </summary>
/// <remarks>
/// Every Chat command handler publishes through <see cref="IIntegrationEventPublisher"/>, and the
/// repository's rules make that the atomic half of "save and publish": the bus outbox stores the
/// message in the same transaction as <c>SaveChanges</c>. Two things are worth asserting, and this
/// fake exists to make both possible.
///
/// The first is which events were published, and how many — a handler that publishes
/// <c>UserMentioned</c> once for a message mentioning three people has a bug that no domain test can
/// see.
///
/// The second is <see cref="PublishedBeforeSave"/>. A handler that publishes *after*
/// <c>SaveChangesAsync</c> is outside the outbox transaction: the row is committed and the event can
/// still be lost. That ordering is invisible in production until a crash lands in the gap, so it is
/// worth a test. <see cref="MarkSaved"/> is how the test tells this fake that the save happened.
/// </remarks>
public sealed class RecordingEventPublisher : IIntegrationEventPublisher
{
    private readonly List<IntegrationEvent> _published = [];
    private bool _saved;

    /// <summary>Every event published, in order.</summary>
    public IReadOnlyList<IntegrationEvent> Published => _published;

    /// <summary>True while every event so far was published before the save was marked.</summary>
    public bool PublishedBeforeSave { get; private set; } = true;

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct) where TEvent : IntegrationEvent
    {
        if (_saved) PublishedBeforeSave = false;

        _published.Add(@event);
        return Task.CompletedTask;
    }

    /// <summary>Records that the handler's <c>SaveChangesAsync</c> has completed.</summary>
    /// <remarks>
    /// Call this from a test's save interception point (or immediately after invoking the handler when
    /// the handler owns the save) to give <see cref="PublishedBeforeSave"/> meaning.
    /// </remarks>
    public void MarkSaved() => _saved = true;

    /// <summary>Returns every published event of one type.</summary>
    /// <typeparam name="TEvent">Event type to filter by.</typeparam>
    /// <returns>The matching events, in publish order.</returns>
    public IReadOnlyList<TEvent> OfType<TEvent>() where TEvent : IntegrationEvent =>
        [.. _published.OfType<TEvent>()];

    /// <summary>Returns the single published event of one type, failing when there is not exactly one.</summary>
    /// <typeparam name="TEvent">Event type expected exactly once.</typeparam>
    /// <returns>The only event of that type.</returns>
    public TEvent Single<TEvent>() where TEvent : IntegrationEvent =>
        _published.OfType<TEvent>().Single();
}
