using HUB.Notification.Domain.Enums;

namespace HUB.Notification.Application.Notifications.DTOs;

/// <summary>Read model for a notification.</summary>
public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    Guid? SourceId,
    Guid? ChannelId,
    Guid? ByUserId,
    string Preview,
    bool IsRead,
    DateTime CreatedAt);
