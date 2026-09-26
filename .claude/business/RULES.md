# Quy tắc Document — `.claude/business/` (HUB)

Rule bắt buộc — **đọc trước khi tạo/sửa bất kỳ `.dod.md` hoặc `domain-business.md`.**

## 1. Mục đích
`.claude/business/` lưu document **business** của HUB (không phải kỹ thuật) — để PM/BA/dev mới hiểu mỗi feature làm gì, rule gì, workflow nào, mà không cần đọc code.

## 2. Cấu trúc
```
.claude/business/
├── RULES.md                    # file này
├── domain-business.md          # tổng quan domain/business toàn HUB
├── channels.dod.md             # kênh chat (public/private/DM)
├── messages.dod.md             # tin nhắn, thread, edit/delete
├── reactions.dod.md            # emoji reaction
├── members.dod.md              # thành viên kênh + vai trò
├── presence.dod.md             # online/typing/unread (P2)
├── notifications.dod.md        # mention/digest (P2)
├── media.dod.md                # file/attachment MinIO (P2)
├── meeting.dod.md              # audio/video/screen share (P4)
└── integration-dashboard.dod.md # SSO + pull data DASHBOARD
```
- Mỗi file `{feature}.dod.md` ~ 1 bounded context / feature (map theo service trong `src/Services/`).
- Tên file kebab-case, số ít.

## 3. Cấu trúc 1 file `.dod.md`
### Phần A — Update Log (đầu file). Không xoá dòng cũ, thêm dòng mới lên **đầu bảng**.
```markdown
## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-08-01 | 20:17 | Khởi tạo document | Tạo business doc ban đầu cho feature X |
```
### Phần B — Business Doc (LATEST, ghi đè khi update): Purpose · Key Entities & Relationships · Business Rules & Invariants · Main Workflows · Definition of Done · Edge Cases & Notes.

## 4. Khi nào update
Thêm/sửa/xoá code làm đổi **business** (rule/workflow/permission/**integration event** mới) → sau khi code xong: thêm 1 dòng Update Log + sửa Business Doc cho khớp state mới. Ảnh hưởng domain tổng thể → update thêm `domain-business.md`. Refactor kỹ thuật thuần → không cần.

## 5. Feature mới
Tạo `{feature-kebab}.dod.md` theo bố cục mục 3, Update Log dòng đầu "Khởi tạo document", thêm vào danh sách mục 2 + `domain-business.md` nếu có khái niệm domain mới.

## 6. Ngôn ngữ
Tiếng Việt, giữ nguyên tên entity/field/enum tiếng Anh như trong code (`Channel`, `MessageSent`, `ChannelMemberRole`, không dịch).
