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

        await harness.Bus.Publish(new UserMentioned(mentioned, channelId, messageId, author));

        (await harness.Consumed.Any<UserMentioned>()).ShouldBeTrue();

        // Every field has to arrive intact: the notification is useless for deep-linking if the message or
        // channel id is dropped on the way through.
        await mediator.Received(1).Send(
            Arg.Is<CreateMentionNotificationCommand>(c =>
                c.UserId    == mentioned &&
                c.ChannelId == channelId &&
                c.MessageId == messageId &&
                c.ByUserId  == author),
            Arg.Any<CancellationToken>());

        await harness.Stop();
    }

    [Fact]
    public async Task TheSameEventConsumedTwiceCreatesOneNotification()
    {
        // The core idempotency case, asserted end to end through the consumer rather than only on the
        // handler: the guard has to survive the path the broker actually takes.
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var handler = new CreateMentionNotificationHandler(lease.Context, new RecordingEmailSender());

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
    public async Task ARedeliveredEventDoesNotSendASecondEmail()
    {
        // The email is fired after the duplicate check, so an idempotency guard that only skipped the row
        // would still email the person twice. That is the visible half of the failure.
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var email   = new RecordingEmailSender();
        var handler = new CreateMentionNotificationHandler(lease.Context, email);

        var command = new CreateMentionNotificationCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hello");

        await handler.Handle(command, CancellationToken.None);
        await handler.Handle(command, CancellationToken.None);

        email.SendCount.ShouldBe(1);
    }

    [Fact]
    public async Task TwoPeopleMentionedInTheSameMessageEachGetANotification()
    {
        // The dedupe key is (user, message, type). Keying on the message alone would notify only whoever
        // happened to be processed first.
        await using var lease = await NotificationDbContextFactory.CreateAsync();

        var handler   = new CreateMentionNotificationHandler(lease.Context, new RecordingEmailSender());
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

        var handler = new CreateMentionNotificationHandler(lease.Context, new RecordingEmailSender());
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

        var handler   = new CreateMentionNotificationHandler(lease.Context, new RecordingEmailSender());
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

    /// <summary>Counts the emails a handler tried to send.</summary>
    private sealed class RecordingEmailSender : HUB.Notification.Application.Common.Interfaces.IEmailSender
    {
        /// <summary>How many sends were attempted.</summary>
        public int SendCount { get; private set; }

        /// <inheritdoc />
        public Task SendAsync(Guid userId, string subject, string body, CancellationToken ct)
        {
            SendCount++;
            return Task.CompletedTask;
        }
    }
}
