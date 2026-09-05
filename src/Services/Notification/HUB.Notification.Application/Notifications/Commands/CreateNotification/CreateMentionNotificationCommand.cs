using MediatR;

namespace HUB.Notification.Application.Notifications.Commands.CreateNotification;

/// <summary>Creates a mention notification for a user (invoked by the UserMentioned consumer).</summary>
public sealed record CreateMentionNotificationCommand(
    Guid UserId, Guid MessageId, Guid ChannelId, Guid ByUserId, string Preview) : IRequest;
