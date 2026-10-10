# Notifications — Business Doc

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-10-08 | — | Notification pipeline: inbox, tách email, push realtime (MP-6) | Endpoint consumer bật **inbox + consumer outbox** (MassTransit EF): dedupe theo MessageId cùng transaction với ghi DB. Unique `(UserId, SourceId, Type)` (lọc NULL) làm chốt cuối; migration xoá bản trùng cũ (giữ cái cũ nhất). Handler **không gửi email nữa** → publish `NotificationCreated` (outbox) → consumer email riêng (retry độc lập) + realtime push `notificationReceived` tới mọi connection người nhận. `UserMentioned` mang `Preview` → notification hết rỗng preview. FE bỏ qua notification đã có (không đếm badge 2 lần). |
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
- Consumer **idempotent**, 3 lớp: (1) **inbox** — cùng broker MessageId giao lại thì bỏ qua, ghi cùng transaction với notification; (2) handler bỏ qua nếu đã có notification cùng `(UserId, SourceId, Type)` (message khác mô tả cùng mention); (3) unique index `(UserId, SourceId, Type)` lọc NULL — đụng thì message vào `_error`, không retry.
- Tạo notification và gửi email là **2 bước tách rời**: notification commit kèm event `NotificationCreated` (outbox); email chỉ gửi từ event đó, lỗi thì retry riêng — không còn mất email. Email at-least-once (có thể gửi lặp nếu gửi xong mà commit lỗi).
- Push realtime: `NotificationCreated` → realtime gửi `notificationReceived` tới **mọi tab/thiết bị đang mở** của người nhận; offline thì không push (thấy khi tải list). Client bỏ qua id đã có.
- `MarkRead` idempotent; chỉ chủ sở hữu mark được (scope theo `UserId`).
- List scope theo user, newest-first, filter unreadOnly.

## Main Workflows
1. chat-service publish `UserMentioned` (kèm `Preview`) → `UserMentionedConsumer` (inbox) → `CreateMentionNotificationCommand` → lưu + publish `NotificationCreated` (outbox, cùng transaction).
2. `NotificationCreated` → `NotificationCreatedEmailConsumer` (inbox) → `SendNotificationEmailCommand` → email (hiện no-op).
3. `NotificationCreated` → realtime `NotificationCreatedConsumer` → `notificationReceived` tới các connection của người nhận.
4. `GET /api/v1/notifications?unreadOnly=&limit=` · `POST /{id}/read` · `POST /read-all`.

## Definition of Done
- [ ] Dedup consumer (idempotent) + index `(UserId, SourceId, Type)`.
- [ ] Index `(UserId, CreatedAt)` + `(UserId, IsRead)` cho list/unread.
- [ ] Email qua `IEmailSender` (swap SMTP/MailKit sau).

## Edge Cases & Notes
- Preview mention hiện rỗng (event `UserMentioned` không mang preview) — GA có thể enrich bằng cách pull message hoặc thêm preview vào event.
- `NotificationPreference` (mute/quiet-hours/digest) chưa làm — backlog.
