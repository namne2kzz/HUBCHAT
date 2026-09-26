---
description: Drive the running HUB app in Microsoft Edge via Playwright MCP to self-test chat/realtime UI flows against business rules in .claude/business/, tracking results/bugs/improvements in .claude/self-test/.
argument-hint: '[optional: module name e.g. "auth" | "channels" | "messages" | "all"]'
---

# Workflow: UI Self-Test (Playwright Edge MCP) — HUB

Tự lái Microsoft Edge qua các luồng UI chính của HUB (chat, realtime, attachment, meeting), đối chiếu với business rule trong `.claude/business/`, ghi kết quả/bug/improvement vào `.claude/self-test/` — thay cho việc test tay từng chức năng.

Agent tương ứng: `.claude/agents/ui-tester.md` — **đọc file đó trước**, nó chứa toàn bộ knowledge sources + quy tắc thao tác.

## Usage
```
/self-test              # chạy toàn bộ module theo .claude/self-test/test-plan.md
/self-test messages     # chỉ module messages
```

---

## Pre-flight (xác nhận app sống TRƯỚC khi test)

HUB **không chạy độc lập** — JWT do DASHBOARD phát hành, data pull từ DASHBOARD `/internal/v1/*`.

1. Đọc credential từ file gitignored **`.claude/self-test.local.json`**:

   ```json
   {
     "baseUrl": "http://localhost:4202",
     "gatewayUrl": "http://localhost:8080",
     "dashboardUrl": "http://localhost:4200",
     "email": "...",
     "password": "...",
     "channelName": "..."
   }
   ```

   File không tồn tại hoặc thiếu field → hỏi user rồi mới chạy tiếp.
2. `browser_navigate` tới `{dashboardUrl}` — DASHBOARD không load → **DỪNG**, báo user bật DASHBOARD (repo `C:\DEV\DASHBOARD`: `dotnet run` + `npm start`). Không có SSO thì HUB vô nghĩa.
3. `browser_navigate` tới `{baseUrl}` — không load → **DỪNG**, báo user chạy `docker compose up` (hoặc `npm start` trong `HUB.VIEW/`). KHÔNG tự chạy `docker compose` / `migrate.ps1` / `rebuild.ps1` / seed.
4. Đọc **`.claude/self-test/wip-features.md`** — module nằm trong bảng đó **bỏ qua hoàn toàn** (không navigate, không tạo case) cho tới khi bị gỡ khỏi file.

## Quy trình cho MỖI module được test

> Module có tên trong `wip-features.md` → skip ngay, báo cáo `⏭️ Skipped (WIP)`, không thực hiện 4 bước dưới. User gọi `/self-test {module}` đích danh module đang WIP → vẫn dừng, báo lý do.

1. **Đọc business doc**: `.claude/business/{feature}.dod.md` — nắm rule/invariant hiện tại.
2. **Đọc file test**: `.claude/self-test/modules/{feature}.test.md` (Run Log đầu file · Test Cases giữa · Case Status cuối file).
   - Phần "Test Cases" đã có case → dùng case đó.
   - Khung rỗng → tự soạn case từ business doc vừa đọc, ghi vào Phần B (kèm field **Realtime: Có/Không**) + thêm dòng `Chưa chạy` vào bảng Case Status, **TRƯỚC khi chạy**.
3. **Chạy từng case** bằng Playwright (browser = msedge): `browser_navigate` → `browser_snapshot` (element qua accessibility tree) → thao tác (`browser_click`/`browser_type`/`browser_file_upload`...) → assert. Dùng `browser_network_requests`/`browser_console_messages` để xác nhận request/WS/lỗi runtime. **KHÔNG `browser_take_screenshot`** — mô tả bằng text là đủ.
   - **Case realtime → BẮT BUỘC 2 tab** (`browser_tabs`): tab A thao tác, tab B phải tự nhận không reload. 1 tab + reload = không tính Pass.
   - Reload lại sau khi tạo/sửa để xác minh **đã persist**.
   - Cross-service (RabbitMQ/outbox) eventual consistency → `browser_wait_for`, nêu rõ thời gian chờ.
4. **Ghi kết quả**:
   - Append 1 dòng vào **Run Log** (đầu file) của `modules/{feature}.test.md`.
   - Ghi đè dòng case tương ứng trong **Case Status** (cuối file) — Pass/Fail + ngày, không tạo dòng trùng case ID.
   - Fail vì sai behavior so với business doc → thêm dòng `.claude/self-test/bugs.md` (mức độ theo `self-test/RULES.md` mục 4), điền ID bug vào cột "Bug liên quan".
   - Phát hiện điểm cải tiến (không phải lỗi) → thêm dòng `.claude/self-test/improvements.md`.

Danh sách module + độ ưu tiên: `.claude/self-test/test-plan.md`. `/self-test` không kèm argument → chạy theo ưu tiên (Cao trước). `/self-test {module}` → chỉ module đó.

**KHÔNG sửa code app trong lúc self-test** — chỉ test, ghi nhận bug/improvement, để user quyết định fix.
**KHÔNG tự promote** bug/improvement lên Nexus (work item thật trong DASHBOARD) — chỉ khi user yêu cầu rõ ràng (`RULES.md` mục 5b).

---

## Report cuối cùng

| Module | Kết quả | Case fail | Bug mới | Chi tiết |
|--------|---------|-----------|---------|----------|
| auth | ✅/❌/⚠️ | ... | BUG-00x hoặc - | `.claude/self-test/modules/auth.test.md` |
| meeting | ⏭️ Skipped (WIP) | - | - | xem `.claude/self-test/wip-features.md` |

- ⚠️ = Partial (có case pass, có case fail). ⏭️ = Skipped vì nằm trong `wip-features.md`.
- Với mỗi ❌/⚠️: nêu case nào fail, lý do bằng text (state/message lỗi/request/WS/service liên quan) — không kèm screenshot.
- Cuối bảng: tổng số bug mới ghi vào `bugs.md`, tổng improvement mới ghi vào `improvements.md`.
