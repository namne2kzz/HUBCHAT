# Domain & Business — Tổng quan HUB

> Đọc `RULES.md` trước khi sửa file này.

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-08-01 | 20:17 | Khởi tạo document | Tạo tổng quan domain/business ban đầu cho HUB (chat/meeting add-on) |

---

## 1. HUB là gì
Lớp **collaboration/communication** kiểu MS Teams của hệ sinh thái DASHBOARD: team đang quản lý sprint/backlog/work-item trên DASHBOARD có thể **chat theo kênh, DM, (sắp tới) họp** ngay trong hệ sinh thái. HUB là **add-on tuỳ chọn** — dùng chung SSO + pull data từ DASHBOARD, không tồn tại độc lập.

## 2. Khái niệm domain cốt lõi

| Khái niệm | Aggregate/Entity | Vai trò |
|---|---|---|
| Workspace | *(= Repository/Project của DASHBOARD)* | Phân vùng; `Channel.WorkspaceId = dashboard_repo_id` |
| Kênh chat | `Channel` | public / private / DM / group DM; có `Members`, slug, topic, archive |
| Thành viên kênh | `ChannelMember` | `ChannelMemberRole` (Member/Admin/Owner), last-read, muted |
| Tin nhắn | `Message` | body (Markdown/plain), thread (`ParentId`), mentions (jsonb), edit/soft-delete |
| Reaction | `Reaction` | emoji, unique per (message,user,emoji) |
| Directory user | `DirectoryUser` (read-model) | cache hồ sơ user pull từ DASHBOARD (name/email/avatarClass) |
| Presence *(P2)* | Redis TTL | online/away/typing/unread — không lưu RDBMS |
| Notification *(P2)* | `Notification` | mention/DM/digest |
| File *(P2)* | `FileObject` | metadata MinIO, presigned URL |
| Meeting *(P4)* | `Meeting` | phòng họp, participant, recording (LiveKit) |

## 3. Luồng nghiệp vụ tổng (chat)
```
User login ở DASHBOARD (JWT HMAC dùng chung)
  → HUB validate token → biết userId
    → HUB pull /internal/v1/me (DASHBOARD) → user thuộc Repository/Project nào = workspace nào
      → trong workspace: tạo/join Channel (public/private/DM)
        → PostMessage → chat-service lưu DB + outbox publish MessageSent/UserMentioned (RabbitMQ)
          → realtime-service consume → fan-out "messageReceived" tới group channel:{id} (SignalR + Redis backplane)
          → notification-service consume UserMentioned (inbox) → lưu notification + outbox NotificationCreated
            → email consumer (retry riêng) · realtime push "notificationReceived" tới mọi connection người nhận
        → AddReaction → outbox ReactionAdded → realtime push "reactionAdded" tới group channel:{id}
        → Remove member / Leave → outbox ChannelMemberRemoved → realtime: xoá cache canjoin, gỡ mọi connection của user khỏi group, gửi "channelAccessRevoked"
        → thread reply, mark-read (unread count) — edit/soft-delete message: chưa có tính năng
```

## 4. Phân quyền (cross-cutting)
- **Identity** từ JWT DASHBOARD (`sub`/`uid`). HUB không login/refresh.
- **Vào workspace nào**: pull membership từ DASHBOARD (`RepositoryMember` → role/permissions), cache Redis. `IsGlobalAdmin` bypass.
- **Trong kênh**: `ChannelMemberRole` (HUB tự quản) quyết định quyền quản trị kênh (thêm/xoá member, xoá tin người khác, archive).

## 5. Pattern chung xuyên suốt
- **Transactional outbox** (MassTransit + EF): "lưu DB + publish event" atomic; consumer **idempotent** (dedupe theo event id).
- **Keyset (seek) pagination** cho message list (index `(channel_id, created_at)`).
- **Soft-delete** message (`DeletedAt`); archive Channel (`IsArchived`, read-only).
- **Database-per-service**; không JOIN chéo — tham chiếu bằng id + pull/cache.
- **Realtime tách khỏi business**: chat-service ghi DB + publish; realtime-service chỉ fan-out.
- **Avatar** = CSS class (`AvatarClass`) pull từ DASHBOARD, không phải URL.

## 6. Danh sách feature document
- [channels.dod.md](channels.dod.md) · [messages.dod.md](messages.dod.md) · [reactions.dod.md](reactions.dod.md) · [members.dod.md](members.dod.md)
- [presence.dod.md](presence.dod.md) *(P2)* · [notifications.dod.md](notifications.dod.md) *(P2)* · [media.dod.md](media.dod.md) *(P2)* · [meeting.dod.md](meeting.dod.md) *(P4)*
- [integration-dashboard.dod.md](integration-dashboard.dod.md) — SSO + pull DASHBOARD
