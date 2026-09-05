using MediatR;

namespace HUB.Notification.Application.Notifications.Commands.MarkRead;

/// <summary>Marks a single notification read for the acting user.</summary>
public sealed record MarkNotificationReadCommand(Guid NotificationId, Guid ActingUserId) : IRequest;
