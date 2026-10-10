using HUB.Notification.Application.Notifications.Commands.CreateNotification;
using HUB.Shared.Contracts.Events;
using MassTransit;
using MediatR;

namespace HUB.Notification.Infrastructure.Consumers;

/// <summary>Consumes <see cref="UserMentioned"/> and creates an in-app notification via MediatR.</summary>
/// <param name="mediator">MediatR sender.</param>
public sealed class UserMentionedConsumer(ISender mediator) : IConsumer<UserMentioned>
{
    /// <summary>Maps the event to a create-notification command.</summary>
    public Task Consume(ConsumeContext<UserMentioned> context)
    {
        var e = context.Message;
        return mediator.Send(
            new CreateMentionNotificationCommand(e.MentionedUserId, e.MessageId, e.ChannelId, e.ByUserId, Preview: e.Preview ?? string.Empty),
            context.CancellationToken);
    }
}
