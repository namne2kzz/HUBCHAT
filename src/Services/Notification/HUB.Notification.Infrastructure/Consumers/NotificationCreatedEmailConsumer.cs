using HUB.Notification.Application.Notifications.Commands.SendNotificationEmail;
using HUB.Notification.Domain.Enums;
using HUB.Shared.Contracts.Events;
using MassTransit;
using MediatR;

namespace HUB.Notification.Infrastructure.Consumers;

/// <summary>
/// Consumes <see cref="NotificationCreated"/> and sends the notification email — a step of its own, so an
/// email failure is retried (and dead-lettered) independently of storing the notification.
/// </summary>
/// <param name="mediator">MediatR sender.</param>
public sealed class NotificationCreatedEmailConsumer(ISender mediator) : IConsumer<NotificationCreated>
{
    /// <summary>Maps the event to a send-email command.</summary>
    /// <param name="context">The consume context carrying the event.</param>
    /// <returns>A task that completes when the email has been handed to the sender.</returns>
    public Task Consume(ConsumeContext<NotificationCreated> context)
    {
        var e = context.Message;
        return mediator.Send(
            new SendNotificationEmailCommand(e.UserId, (NotificationType)e.Type, e.Preview),
            context.CancellationToken);
    }
}
