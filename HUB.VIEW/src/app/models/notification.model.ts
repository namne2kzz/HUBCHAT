export enum NotificationType {
  Mention       = 0,
  DirectMessage = 1,
  System        = 2,
}

/** Mirrors HUB.Notification.Application.Notifications.DTOs.NotificationDto (camelCased). */
export interface NotificationDto {
  id: string;
  type: NotificationType;
  sourceId: string | null;
  channelId: string | null;
  byUserId: string | null;
  preview: string;
  isRead: boolean;
  createdAt: string;
}

/** Realtime (SignalR) payload. */
export interface NotificationReceivedEvent {
  notification: NotificationDto;
}
