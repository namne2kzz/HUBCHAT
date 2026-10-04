using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace HUB.Shared.Messaging.UnitTests;

/// <summary>
/// Covers <c>AddHubRetryPolicy</c>: that a transient failure is retried until it succeeds, and that an
/// exception a service marks as non-transient is faulted straight away instead of being retried.
/// </summary>
/// <remarks>
/// Runs through real MassTransit dispatch (in-memory test harness) with the production policy applied via
/// the same callback <c>AddHubMessaging</c> uses, so a regression in how the policy is attached to
/// endpoints — not just its parameters — shows up here.
/// </remarks>
public sealed class HubRetryPolicyTests
{
    [Fact]
    public async Task ATransientFailureIsRetriedUntilTheConsumerSucceeds()
    {
        var attempts = new AttemptCounter { FailuresBeforeSuccess = 2 };
        await using var provider = BuildHarness(attempts);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new Ping(Guid.NewGuid()));

        (await harness.Consumed.Any<Ping>(m => m.Exception is null)).ShouldBeTrue();
        attempts.Count.ShouldBe(3);
        (await harness.Published.Any<Fault<Ping>>()).ShouldBeFalse();

        await harness.Stop();
    }

    [Fact]
    public async Task AnIgnoredExceptionIsFaultedWithoutRetrying()
    {
        var attempts = new AttemptCounter { AlwaysThrow = new InvalidDataException("bad payload") };
        await using var provider = BuildHarness(attempts, r => r.Ignore<InvalidDataException>());

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new Ping(Guid.NewGuid()));

        (await harness.Published.Any<Fault<Ping>>()).ShouldBeTrue();
        attempts.Count.ShouldBe(1);

        await harness.Stop();
    }

    private static ServiceProvider BuildHarness(AttemptCounter attempts, Action<IRetryConfigurator>? configureRetry = null) =>
        new ServiceCollection()
            .AddSingleton(attempts)
            .AddMassTransitTestHarness(bus =>
            {
                bus.AddHubRetryPolicy(configureRetry);
                bus.AddConsumer<FlakyConsumer>();
            })
            .BuildServiceProvider(validateScopes: true);

    /// <summary>Test message.</summary>
    /// <param name="Id">Arbitrary id.</param>
    public sealed record Ping(Guid Id);

    /// <summary>Shared, thread-safe attempt counter that also scripts how the consumer fails.</summary>
    public sealed class AttemptCounter
    {
        private int _count;

        /// <summary>Number of times the consumer has been invoked.</summary>
        public int Count => Volatile.Read(ref _count);

        /// <summary>Throw a transient exception on this many attempts, then succeed.</summary>
        public int FailuresBeforeSuccess { get; init; }

        /// <summary>When set, every attempt throws this exception.</summary>
        public Exception? AlwaysThrow { get; init; }

        /// <summary>Records an attempt and returns its 1-based number.</summary>
        public int Next() => Interlocked.Increment(ref _count);
    }

    /// <summary>Consumer that fails according to the <see cref="AttemptCounter"/> script.</summary>
    /// <param name="attempts">The shared counter.</param>
    public sealed class FlakyConsumer(AttemptCounter attempts) : IConsumer<Ping>
    {
        /// <inheritdoc />
        public Task Consume(ConsumeContext<Ping> context)
        {
            var attempt = attempts.Next();

            if (attempts.AlwaysThrow is { } ex) throw ex;
            if (attempt <= attempts.FailuresBeforeSuccess) throw new TimeoutException($"transient #{attempt}");

            return Task.CompletedTask;
        }
    }
}
