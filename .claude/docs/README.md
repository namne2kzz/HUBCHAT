# `.claude/docs/` — Kiến thức bổ sung (HUB)

Folder **chứa tài liệu tham khảo** cho Claude khi làm việc trong repo HUB. Khác với các folder sẵn có:

| Folder | Nội dung | Ai ghi |
|--------|----------|--------|
| `.claude/business/` | Business doc theo feature (`{feature}.dod.md`) — nghiệp vụ "phải đúng như thế nào" | Claude tự update sau khi đổi business logic |
| `.claude/self-test/` | Test plan + kết quả + bug + improvement từ self-test UI | Claude ghi sau mỗi lần `/self-test` |
| `.claude/memory/` | `mistakes.md` (lỗi thường gặp) · `patterns.md` (pattern nên dùng) | Claude tự bổ sung khi học được |
| **`.claude/docs/`** | **Kiến thức nền do user cung cấp**: domain knowledge, glossary, quy ước nghiệp vụ, spec, ADR, hợp đồng API bên thứ ba, ghi chú hạ tầng | **User bỏ vào** — Claude chỉ đọc, không tự ghi trừ khi được yêu cầu |

Tài liệu kiến trúc HUB nằm ở **root repo**, không phải folder này: `PLAN.md` (kiến trúc tổng) và `docs/DASHBOARD-INTERNAL-API.md` (hợp đồng API với DASHBOARD) — xem `CLAUDE.md`.

## Quy ước

- 1 chủ đề = 1 file `.md`, tên kebab-case (vd `domain-glossary.md`, `chat-retention-policy.md`).
- File nào đọc lúc nào: các agent trong `.claude/agents/` tham chiếu folder này ở mục "Knowledge sources".
- Tiếng Việt, giữ nguyên tên entity/field/route/component bằng tiếng Anh như trong code (đồng bộ `.claude/business/RULES.md`).
- Folder đang trống → chưa có kiến thức bổ sung, bỏ qua.

> Claude: **KHÔNG tự tạo file** trong folder này trừ khi user yêu cầu rõ ràng.
