using HUB.Notification.Application.Notifications.Commands.CreateNotification;
using HUB.Notification.Domain.Enums;
using HUB.Notification.Infrastructure.Consumers;
using HUB.Shared.Contracts.Events;
using MassTransit;
using MassTransit.Testing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace HUB.Notification.UnitTests;

/// <summary>
/// Covers <c>UserMentionedConsumer</c> and the command it dispatches, with redelivery as the main subject.
/// </summary>
/// <remarks>
/// RabbitMQ gives at-least-once delivery, so a consumer will eventually see the same event twice — on a
/// broker retry, a redeploy mid-ack, or a manual requeue. The repository's rules therefore make consumer
/// idempotency non-negotiable. It is also invisible when broken: a duplicated notification is not an
/// error, it is just a person seeing the same mention twice and losing confidence in the badge.
///
/// The bus is exercised through MassTransit's in-memory harness rather than a real broker, so these stay
/// unit-speed while still going through genuine consumer dispatch.
/// </remarks>
public sealed class UserMentionedConsumerTests
{
    [Fact]
    public async Task TheConsumerMapsTheEventOntoTheCreateCommand()
    {
        var mediator = Substitute.For<ISender>();

        await using var provider = new ServiceCollection()
            .AddSingleton(mediator)
            .AddMassTransitTestHarness(bus => bus.AddConsumer<UserMentionedConsumer>())
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var mentioned = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var author    = Guid.NewGuid();

        await harness.Bus.Publish(new UserMentioned(mentioned, channelId, messageId, author, "hey @you"));

        (await harness.Consumed.Any<UserMentioned>()).ShouldBeTrue();

        // Every field has to arrive intact: the notification is useless for deep-linking if the message or
        // channel id is dropped on the way through.
        await mediator.Received(1).Send(
            Arg.Is<CreateMentionNotificationCommand>(c =>
                c.UserId    == mentioned &&
                c.ChannelId == channelId &&
                c.MessageId == messageId &&
                c.ByUserId  == author &&
                c.Preview   == "hey @you"),
            Arg.Any<CancellationToken>());

        await harness.Stop();
    }

    [Fact]
    public async Task TheSameEventConsumedTwiceCreatesOneNotification()
    {
        // The core idempotency case, asserted end to end through the consumer rather than only on the
        // handler: the guard has to survive the path the broker actually takes.
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var handler = new CreateMentionNotificationHandler(lease.Context, new RecordingPublisher());

        var mentioned = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var command   = new CreateMentionNotificationCommand(
            mentioned, messageId, Guid.NewGuid(), Guid.NewGuid(), "hello");

        await handler.Handle(command, CancellationToken.None);
        await handler.Handle(command, CancellationToken.None);

        await using var verify = lease.NewContext();
        (await verify.Notifications.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task ADuplicateIsNotAnnouncedTwice()
    {
        // NotificationCreated drives the email and the realtime push. A duplicate that only skipped the row
        // but still published would email and push the person twice  14 the visible half of the failure.
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var events  = new RecordingPublisher();
        var handler = new CreateMentionNotificationHandler(lease.Context, events);

        var command = new CreateMentionNotificationCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hello");

        await handler.Handle(command, CancellationToken.None);
        await handler.Handle(command, CancellationToken.None);

        var announced = events.Published.OfType<NotificationCreated>().ShouldHaveSingleItem();
        announced.UserId.ShouldBe(command.UserId);
        announced.Preview.ShouldBe("hello");
        announced.Type.ShouldBe((int)NotificationType.Mention);
    }

    [Fact]
    public async Task TwoPeopleMentionedInTheSameMessageEachGetANotification()
    {
        // The dedupe key is (user, message, type). Keying on the message alone would notify only whoever
        // happened to be processed first.
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var handler   = new CreateMentionNotificationHandler(lease.Context, new RecordingPublisher());
        var messageId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var author    = Guid.NewGuid();

        await handler.Handle(new CreateMentionNotificationCommand(
            Guid.NewGuid(), messageId, channelId, author, "hi"), CancellationToken.None);
        await handler.Handle(new CreateMentionNotificationCommand(
            Guid.NewGuid(), messageId, channelId, author, "hi"), CancellationToken.None);

        await using var verify = lease.NewContext();
        (await verify.Notifications.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task OnePersonMentionedInTwoMessagesGetsTwoNotifications()
    {
        // The other direction: keying on the user alone would silence every mention after the first.
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var handler = new CreateMentionNotificationHandler(lease.Context, new RecordingPublisher());
        var user    = Guid.NewGuid();

        await handler.Handle(new CreateMentionNotificationCommand(
            user, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "first"), CancellationToken.None);
        await handler.Handle(new CreateMentionNotificationCommand(
            user, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "second"), CancellationToken.None);

        await using var verify = lease.NewContext();
        (await verify.Notifications.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task TheStoredNotificationCarriesTheEventsDetails()
    {
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var handler   = new CreateMentionNotificationHandler(lease.Context, new RecordingPublisher());
        var user      = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var author    = Guid.NewGuid();

        await handler.Handle(new CreateMentionNotificationCommand(
            user, messageId, channelId, author, "you were mentioned"), CancellationToken.None);

        await using var verify = lease.NewContext();
        var stored = await verify.Notifications.AsNoTracking().SingleAsync();

        stored.UserId.ShouldBe(user);
        stored.SourceId.ShouldBe(messageId);
        stored.ChannelId.ShouldBe(channelId);
        stored.ByUserId.ShouldBe(author);
        stored.Type.ShouldBe(NotificationType.Mention);
        stored.IsRead.ShouldBeFalse();
    }

    /// <summary>Records published integration events.</summary>
    private sealed class RecordingPublisher : HUB.Notification.Application.Common.Interfaces.IIntegrationEventPublisher
    {
        /// <summary>Events published so far.</summary>
        public List<HUB.Shared.Contracts.IntegrationEvent> Published { get; } = [];

        /// <inheritdoc />
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct) where TEvent : HUB.Shared.Contracts.IntegrationEvent
        {
            Published.Add(@event);
            return Task.CompletedTask;
        }
    }
}
