using HUB.Notification.Application.Notifications.DTOs;
using MediatR;

namespace HUB.Notification.Application.Notifications.Queries.ListNotifications;

/// <summary>Lists the acting user's notifications, newest first.</summary>
public sealed record ListNotificationsQuery(Guid ActingUserId, bool UnreadOnly, int Limit)
    : IRequest<IReadOnlyList<NotificationDto>>;
