# Members (Channel) — Business Doc

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-08-01 | 20:18 | Khởi tạo document | Tạo doc business ban đầu cho feature Channel Members (P1) |

---

## Purpose
Quản lý thành viên trong 1 kênh và vai trò quản trị kênh.

## Key Entities & Relationships
- `ChannelMember`: `ChannelId`, `UserId`, `Role` (`ChannelMemberRole` Member/Admin/Owner), `Muted`, `LastReadAt`, `JoinedAt`.

## Business Rules & Invariants
- Không trùng member trong 1 kênh (unique `(ChannelId, UserId)`).
- Owner mặc định là người tạo kênh.
- `MarkRead(readAt)`: chỉ tiến `LastReadAt` (không lùi).
- Quyền quản trị kênh (thêm/xoá member, xoá tin người khác, archive) theo Role *(hoàn thiện dần)*.

## Main Workflows
1. **Join**: caller tự thêm mình (`POST /channels/{id}/members`).
2. **Mark read**: `POST /channels/{id}/read` → update `LastReadAt` phục vụ unread count.

## Definition of Done
- [ ] Unique `(ChannelId, UserId)` + index `(UserId)`.
- [ ] Membership lookup dùng cho authorization (post/list/react).
- [ ] Unit test membership + mark-read tiến/không lùi.

## Edge Cases & Notes
- Phân biệt với **workspace membership** (pull từ DASHBOARD): quyền vào workspace ≠ member kênh cụ thể.
- P2: invite/remove member, change role, mute.
