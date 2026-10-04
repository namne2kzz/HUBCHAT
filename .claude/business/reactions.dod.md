# Reactions — Business Doc

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-10-04 | — | Đồng bộ reaction realtime (MP-4) | Reaction **mới** → outbox `ReactionAdded` → realtime push `reactionAdded` tới group kênh; client khác cập nhật ngay không cần reload. React lặp lại (đã có) không phát event. |
| 2026-08-01 | 20:18 | Khởi tạo document | Tạo doc business ban đầu cho feature Reactions (P1) |

---

## Purpose
Emoji reaction trên message.

## Key Entities & Relationships
- `Reaction` (trong Message aggregate): `MessageId`, `UserId`, `Emoji`.

## Business Rules & Invariants
- 1 user reaction 1 emoji tối đa 1 lần / message (idempotent add; unique `(MessageId, UserId, Emoji)`).
- Không react message đã xoá.
- Chỉ member của kênh mới được react.
- Reaction mới được **đồng bộ realtime** tới mọi người đang mở kênh (`reactionAdded`); react trùng không phát lại. Client bỏ qua reaction đã hiển thị (cùng user + emoji) để chịu được redelivery.

## Main Workflows
1. **Add**: `POST /api/v1/messages/{messageId}/reactions` (Emoji) → verify membership → `Message.AddReaction`.
2. **Remove** *(planned)*: `Message.RemoveReaction(userId, emoji)`.

## Definition of Done
- [ ] Unique index `(MessageId, UserId, Emoji)`.
- [ ] Membership check trước khi react.
- [ ] Unit test: add trùng → không nhân đôi; react message đã xoá → throw.

## Edge Cases & Notes
- Reaction là phần của Message aggregate (load Include Reactions), không phải aggregate riêng.
