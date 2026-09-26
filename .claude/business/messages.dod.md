# Messages — Business Doc

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-08-01 | 20:18 | Khởi tạo document | Tạo doc business ban đầu cho feature Messages (P1) |

---

## Purpose
Tin nhắn trong kênh: top-level hoặc thread reply, hỗ trợ mention, edit, soft-delete; là nguồn phát integration event cho realtime + notification.

## Key Entities & Relationships
- `Message` (aggregate root): `ChannelId`, `ParentId` (thread), `AuthorId`, `Body`, `Format` (`MessageFormat` Plain/Markdown), `Mentions` (jsonb List<Guid>), `EditedAt`, `DeletedAt`, `Reactions`.
- Publish: `MessageSent`, `UserMentioned` (mỗi mention) qua RabbitMQ (transactional outbox).

## Business Rules & Invariants
- Body bắt buộc (không rỗng) khi Post và Edit.
- Mentions distinct.
- Edit: chặn nếu đã xoá; set `EditedAt`. Chỉ tác giả được edit (enforce ở Application).
- Soft-delete: set `DeletedAt`, idempotent; không hiển thị trong list.
- Post yêu cầu caller là **member** của kênh và kênh **writable** (chưa archive).

## Main Workflows
1. **Post**: `POST /api/v1/channels/{channelId}/messages` (Body, Format, ParentId?, MentionedUserIds) → lưu + outbox publish `MessageSent` (+ `UserMentioned` per mention) → realtime fan-out.
2. **List**: `GET /api/v1/channels/{channelId}/messages?cursor=&limit=` → top-level (`ParentId=null`, chưa xoá), **keyset pagination** newest-first, kèm reactions.

## Definition of Done
- [ ] Membership + writable check trước khi post (ForbiddenException/DomainException).
- [ ] Publish event qua outbox (atomic với SaveChanges), không publish trực tiếp.
- [ ] List keyset dựa index `(channel_id, created_at)`; materialize rồi map DTO (mentions/reactions).
- [ ] Unit test domain: empty body→throw, distinct mentions, edit sau delete→throw, reaction idempotent.

## Edge Cases & Notes
- Cursor keyset hiện key theo `CreatedAt` (`<`); tie cùng timestamp cực hiếm — GA cân nhắc thêm tie-breaker Id translatable.
- Preview trong `MessageSent` cắt 140 ký tự cho notification.
