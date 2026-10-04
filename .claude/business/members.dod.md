# Members (Channel) — Business Doc

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-10-04 | — | Thu hồi quyền realtime khi bị xoá / rời kênh (MP-4) | Remove member (manager, sprint sync) và Leave đều publish `ChannelMemberRemoved` (outbox). Realtime: xoá cache `canjoin`, gỡ **mọi connection** của user khỏi group kênh, gửi `channelAccessRevoked`. Trước đó người bị kick vẫn nhận message kênh private tới khi reconnect (+ cache allow 60s). FE: kênh private biến khỏi sidebar/đóng view; kênh public chuyển về trạng thái chưa join. |
| 2026-10-04 | — | Phân quyền add/remove member (MP-3A, fix bảo mật) | `PUT/DELETE /channels/{id}/members/{userId}` trước đây **không kiểm tra quyền** — ai đăng nhập cũng tự thêm mình vào kênh private (đọc được hết) hoặc kick bất kỳ ai. Giờ chỉ Owner/Admin của kênh **hoặc** người có quyền workspace `ManageChannels` (đọc từ dashboard-gateway bằng chính JWT người gọi; lỗi/timeout → từ chối). Internal API (sprint sync, service token) giữ nguyên. |
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
- **Thêm/xoá member của người khác** (public API): chỉ **Owner/Admin** của kênh, **hoặc** user có quyền workspace `ManageChannels` trong workspace của kênh (khớp `canManage` ở HUB.VIEW). Không đủ quyền → 403. Kiểm tra quyền chạy **trước** nhánh "không phải member → no-op" để người lạ không dò được ai là member.
- `ManageChannels` lấy từ dashboard-gateway `/me/memberships` bằng **JWT của chính người gọi** (Chat không hỏi được quyền của người khác). Chỉ gọi khi role trong kênh không đủ. Gateway lỗi/timeout → coi như không có quyền (fail-closed).
- Internal API (`/internal/channels/.../members`, service token, sprint sync DASHBOARD) không có người thực hiện → tin cậy, không kiểm tra role.
- Xoá tin người khác, archive: theo Role *(hoàn thiện dần)*.

## Main Workflows
1. **Join**: caller tự thêm mình (`POST /channels/{id}/members`).
2. **Mark read**: `POST /channels/{id}/read` → update `LastReadAt` phục vụ unread count.
3. **Add member** (`PUT /channels/{id}/members/{userId}`) / **Remove member** (`DELETE /channels/{id}/members/{userId}`): người quản lý thêm/xoá người khác (xem rule quyền). Tự rời kênh dùng `DELETE /channels/{id}/members/me`.

## Definition of Done
- [ ] Unique `(ChannelId, UserId)` + index `(UserId)`.
- [ ] Membership lookup dùng cho authorization (post/list/react).
- [ ] Unit test membership + mark-read tiến/không lùi.

## Edge Cases & Notes
- Phân biệt với **workspace membership** (pull từ DASHBOARD): quyền vào workspace ≠ member kênh cụ thể.
- **Mất membership có hiệu lực realtime ngay**: bị xoá (bởi manager hoặc sprint sync) hoặc tự rời → server gỡ mọi connection (mọi tab/thiết bị) của user khỏi group kênh + xoá cache `canjoin` → không còn nhận message; join lại bị kiểm tra mới. Kênh **public** vẫn đọc được khi không là member (rule cũ), nên user có thể mở lại ở chế độ chỉ đọc.
- Connection "chết" chưa được dọn khỏi presence (tối đa 90s TTL) có thể làm bước gỡ chậm tối đa 5s/connection — không chặn các connection khác.
- Chưa xử lý: đổi kênh public → private không gỡ những người đang xem mà không phải member (cần event riêng).
- P2: change role, mute.
