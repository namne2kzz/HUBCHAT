using HUB.Notification.Domain.Enums;
using MediatR;

namespace HUB.Notification.Application.Notifications.Commands.SendNotificationEmail;

/// <summary>Sends the email for a stored notification (invoked by the NotificationCreated consumer).</summary>
/// <param name="UserId">Recipient.</param>
/// <param name="Type">Notification kind — picks the subject.</param>
/// <param name="Preview">Preview text used as the body.</param>
public sealed record SendNotificationEmailCommand(Guid UserId, NotificationType Type, string Preview) : IRequest;
