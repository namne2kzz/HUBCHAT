using HUB.Notification.Application;
using HUB.Notification.Application.Common.Interfaces;
using HUB.Notification.Infrastructure;
using HUB.Notification.Infrastructure.Messaging;
using HUB.Notification.Infrastructure.Persistence;
using HUB.Shared.Contracts.Events;
using HUB.Shared.Messaging;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace HUB.Notification.IntegrationTests;

/// <summary>
/// The notification pipeline end to end on real PostgreSQL with the production bus wiring
/// (<c>AddNotificationConsumers</c> + HUB retry): inbox dedupe, the outboxed NotificationCreated, and the
/// separately retried email.
/// </summary>
/// <remarks>
/// The in-memory transport stands in for RabbitMQ; everything database-side — InboxState, OutboxMessage,
/// the unique index, the transaction that ties them together — is the real thing.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "RequiresDocker")]
public sealed class NotificationPipelineTests(PostgresFixture database)
{
    /// <summary>Email double that can fail a set number of times before succeeding.</summary>
    private sealed class FlakyEmailSender(int failuresBeforeSuccess = 0) : IEmailSender
    {
        private int _attempts;

        public int Attempts => Volatile.Read(ref _attempts);
        public int Sent { get; private set; }

        public Task SendAsync(Guid toUserId, string subject, string body, CancellationToken ct)
        {
            if (Interlocked.Increment(ref _attempts) <= failuresBeforeSuccess)
                throw new IOException("smtp unavailable");
            Sent++;
            return Task.CompletedTask;
        }
    }

    private static ServiceProvider Build(string connectionString, IEmailSender email) =>
        new ServiceCollection()
            .AddLogging()
            .AddDbContext<NotificationDbContext>(o => o.UseNpgsql(connectionString))
            .AddScoped<INotificationDbContext>(sp => sp.GetRequiredService<NotificationDbContext>())
            .AddScoped<IIntegrationEventPublisher, IntegrationEventPublisher>()
            .AddSingleton(email)
            .AddNotificationApplication()
            .AddMassTransitTestHarness(bus =>
            {
                bus.AddHubRetryPolicy(HUB.Notification.Infrastructure.DependencyInjection.ConfigureRetry);
                bus.AddNotificationConsumers();
            })
            .BuildServiceProvider();

    private static UserMentioned AMention(string preview = "hey @you") =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), preview);

    [Fact]
    public async Task TheInboxDropsARedeliveryOfTheSameMessage()
    {
        var connectionString = await database.CreateDatabaseAsync();
        var email = new FlakyEmailSender();
        await using var provider = Build(connectionString, email);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var mention   = AMention();
        var messageId = NewId.NextGuid();

        // Same broker MessageId twice = a redelivery (consumer crashed before ack, broker requeue, ...).
        await harness.Bus.Publish(mention, c => c.MessageId = messageId);
        await harness.Bus.Publish(mention, c => c.MessageId = messageId);
        await harness.InactivityTask;

        await using var db = PostgresFixture.CreateContext(connectionString);
        (await db.Notifications.CountAsync()).ShouldBe(1);
        // Announced once → one email, one realtime push.
        // Observed on the consuming side: outbox-delivered messages do not pass the harness publish observer.
        harness.Consumed.Select<NotificationCreated>().Count().ShouldBe(1);
        email.Sent.ShouldBe(1);

        await harness.Stop();
    }

    [Fact]
    public async Task TheInboxStopsARedeliveredEmailFromGoingOutTwice()
    {
        // The case only the inbox can catch: the email step has no row of its own to dedupe against, so
        // without InboxState a redelivered NotificationCreated emails the person again.
        var connectionString = await database.CreateDatabaseAsync();
        var email = new FlakyEmailSender();
        await using var provider = Build(connectionString, email);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var created   = new NotificationCreated(Guid.NewGuid(), Guid.NewGuid(), 0, Guid.NewGuid(), null, null, "hi", DateTime.UtcNow);
        var messageId = NewId.NextGuid();
        await harness.Bus.Publish(created, c => c.MessageId = messageId);
        await harness.Bus.Publish(created, c => c.MessageId = messageId);
        await harness.InactivityTask;

        email.Sent.ShouldBe(1);

        await harness.Stop();
    }

    [Fact]
    public async Task NotificationCreatedCarriesThePreviewAndTheStoredId()
    {
        var connectionString = await database.CreateDatabaseAsync();
        await using var provider = Build(connectionString, new FlakyEmailSender());
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var mention = AMention("deploy is done @you");
        await harness.Bus.Publish(mention);
        await harness.InactivityTask;

        await using var db = PostgresFixture.CreateContext(connectionString);
        var stored    = await db.Notifications.SingleAsync();
        var announced = harness.Consumed.Select<NotificationCreated>().Single().Context.Message;

        // Realtime pushes this payload as-is, so it must match the row the list endpoint will later return.
        announced.NotificationId.ShouldBe(stored.Id);
        announced.UserId.ShouldBe(mention.MentionedUserId);
        announced.Preview.ShouldBe("deploy is done @you");
        stored.Preview.ShouldBe("deploy is done @you");

        await harness.Stop();
    }

    [Fact]
    public async Task AFailedEmailIsRetriedWithoutLosingItOrDuplicatingTheNotification()
    {
        var connectionString = await database.CreateDatabaseAsync();
        var email = new FlakyEmailSender(failuresBeforeSuccess: 2);
        await using var provider = Build(connectionString, email);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(AMention());
        await harness.InactivityTask;

        // The old handler sent email after SaveChanges: a failure retried the whole consume, the retry found
        // the row, returned early — and the email was never sent. Now only the email step retries.
        email.Attempts.ShouldBe(3);
        email.Sent.ShouldBe(1);
        await using var db = PostgresFixture.CreateContext(connectionString);
        (await db.Notifications.CountAsync()).ShouldBe(1);

        await harness.Stop();
    }

    [Fact]
    public async Task TheMigrationKeepsTheOldestOfExistingDuplicates()
    {
        // A database still on InitialCreate, holding duplicates the old check-then-insert could produce.
        var connectionString = await database.CreateDatabaseAsync("20260802072702_InitialCreate");
        var (user, source) = (Guid.NewGuid(), Guid.NewGuid());
        await using (var db = PostgresFixture.CreateContext(connectionString))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO notifications ("Id","UserId","Type","SourceId","Preview","IsRead","CreatedAt")
                VALUES ({Guid.NewGuid()}, {user}, 0, {source}, 'first',  false, {new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)}),
                       ({Guid.NewGuid()}, {user}, 0, {source}, 'second', false, {new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc)});
                """);
        }

        // Applying the rest must not fail on the new unique index (services migrate on startup).
        await using (var db = PostgresFixture.CreateContext(connectionString))
            await db.Database.MigrateAsync();

        await using var verify = PostgresFixture.CreateContext(connectionString);
        var left = await verify.Notifications.ToListAsync();
        left.ShouldHaveSingleItem().Preview.ShouldBe("first");
    }
}
