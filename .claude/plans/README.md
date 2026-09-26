# `.claude/plans/` — Implementation plan

Plan chi tiết cho feature lớn **trước khi code** (thiết kế schema, API contract, thứ tự implement, migration strategy). Mỗi feature 1 file `{feature-slug}.md`.

| Folder | Khác gì |
|--------|---------|
| `.claude/plans/` | **Sẽ làm gì** — plan trước khi code, có thể bỏ/đổi |
| `.claude/business/` | **Nghiệp vụ phải đúng thế nào** — business doc, update sau khi code xong |
| `.claude/self-test/` | **Đã kiểm chứng chưa** — bằng chứng test trên app thật |

## Quy ước

- Tên file kebab-case theo feature: `message-pinning.md`, `meeting-recording.md`.
- Plan xong và đã code hết → giữ lại file để trace quyết định, không xoá.
- Plan bị huỷ → thêm dòng `> **Status:** Cancelled — <lý do>` ở đầu file.
- Tiếng Việt, giữ nguyên tên entity/field/route/service bằng tiếng Anh như trong code.
