# Notifications — Business Doc

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-08-02 | 12:58 | Impl P2 | notification-service: consume UserMentioned tạo in-app notification, API list/mark-read, email stub (no-op) |
| 2026-08-01 | 00:00 | Khởi tạo document | Tạo stub ban đầu |

---

## Purpose
Thông báo in-app khi user được @mention (P2); email digest để sau (hiện stub no-op).

## Key Entities & Relationships
- `Notification` (aggregate): `UserId`, `Type` (`NotificationType` Mention/DirectMessage/System), `SourceId` (messageId), `ChannelId`, `ByUserId`, `Preview`, `IsRead`, `ReadAt`.
- `IEmailSender` → `NoOpEmailSender` (log). Consume `UserMentioned` (RabbitMQ).
- DB `hub_notif`.

## Business Rules & Invariants
- Consumer **idempotent**: bỏ qua nếu đã có notification cùng `(UserId, SourceId, Type=Mention)` (at-least-once có thể replay).
- `MarkRead` idempotent; chỉ chủ sở hữu mark được (scope theo `UserId`).
- List scope theo user, newest-first, filter unreadOnly.

## Main Workflows
1. chat-service publish `UserMentioned` → `UserMentionedConsumer` → MediatR `CreateMentionNotificationCommand` → lưu + email (no-op).
2. `GET /api/v1/notifications?unreadOnly=&limit=` · `POST /{id}/read` · `POST /read-all`.

## Definition of Done
- [ ] Dedup consumer (idempotent) + index `(UserId, SourceId, Type)`.
- [ ] Index `(UserId, CreatedAt)` + `(UserId, IsRead)` cho list/unread.
- [ ] Email qua `IEmailSender` (swap SMTP/MailKit sau).

## Edge Cases & Notes
- Preview mention hiện rỗng (event `UserMentioned` không mang preview) — GA có thể enrich bằng cách pull message hoặc thêm preview vào event.
- `NotificationPreference` (mute/quiet-hours/digest) chưa làm — backlog.
