# Quy tắc Document — `.claude/self-test/` (HUB)

Tài liệu này quy định cách Claude (và bất kỳ ai khác) ghi/đọc/update các file trong `.claude/self-test/`. **Phải đọc file này trước khi tạo hoặc sửa bất kỳ file trong folder.**

## 0. Quy tắc bắt buộc khi chạy test

> **QUAN TRỌNG — không được bỏ qua:**
>
> - **Chạy qua UI, không chạy ngầm.** Mọi test case phải thao tác trực tiếp trên browser (navigate, click, type, upload...) để user quan sát được. **Tuyệt đối không dùng `browser_evaluate` / `fetch` API call ngầm để thay thế thao tác UI** — chỉ được dùng `browser_evaluate` để *đọc/xác nhận* kết quả (response body, SignalR state...) sau khi đã thao tác qua UI xong.
> - **Test data KHÔNG được xoá trừ khi user yêu cầu rõ ràng.** Giữ lại toàn bộ channel, message, reaction, attachment, meeting đã tạo — để hệ thống có data thật, dễ hình dung nghiệp vụ. Chỉ xoá khi case *bắt buộc* test nghiệp vụ xoá, hoặc user nói thẳng.
> - **Tạo nhiều data, data thật.** Tên channel, nội dung message, tên file attachment phải có nghĩa như chat công việc thật. KHÔNG `test123`, `aaa`, `hello`.
> - **Realtime phải test 2 chiều bằng 2 tab.** Với mọi case liên quan SignalR (message mới, typing, presence, reaction, notification): mở 2 tab (`browser_tabs`) — tab A gửi, tab B **phải nhận được không cần reload**. Chỉ test 1 tab rồi reload = KHÔNG chứng minh được realtime, không được tính Pass.

## 1. Mục đích thư mục

`.claude/self-test/` lưu **test plan + kết quả + bug + improvement** phát sinh từ việc tự test UI bằng Playwright MCP (xem `/self-test` và agent `ui-tester`). Khác với `.claude/business/` (business doc — nghiệp vụ "phải đúng như thế nào"), folder này lưu **bằng chứng đã/chưa kiểm chứng đúng nghiệp vụ đó trên app thật**.

## 2. Cấu trúc thư mục

```
.claude/self-test/
├── RULES.md              # file này
├── test-plan.md          # scope, môi trường, quy trình, danh sách module + ưu tiên
├── wip-features.md       # module đang code dở → skip khi self-test
├── bugs.md               # bug tracker — chỉ lỗi hệ thống (sai so với business doc)
├── improvements.md       # ý tưởng cải tiến UX/nghiệp vụ — KHÔNG phải bug
└── modules/
    └── {feature}.test.md # 1 file / module, tên khớp `.claude/business/{feature}.dod.md`
```

## 3. Cấu trúc bên trong `modules/{feature}.test.md`

3 phần, theo đúng thứ tự:

### Phần A — Run Log (luôn ở đầu file)

Append dòng mới lên **đầu bảng**, không xoá dòng cũ. Tổng quan **theo từng lần chạy**:

```markdown
## Run Log

| Ngày | Giờ | Kết quả | Bug mới | Ghi chú |
|------|-----|---------|---------|---------|
| 2026-09-26 | 21:10 | Pass | - | Gửi message + realtime 2 tab OK |
```

- **Kết quả**: `Pass` / `Fail` / `Partial`.
- **Bug mới**: ID bug nếu có (vd `BUG-003`), hoặc `-`.
- **Ghi chú**: 1 câu ngắn, nêu case nào fail nếu có.

### Phần B — Test Cases (bản LATEST, ghi đè khi sửa)

Chỉ chứa **định nghĩa case** — KHÔNG có pass/fail ở đây (trạng thái ở Phần C). Mỗi case:

```markdown
### {feature}-01 — {Tên case ngắn}

- **Business rule**: trích/link rule trong [`{feature}.dod.md`](../../business/{feature}.dod.md) (không có doc → ghi "Chưa có business doc — test theo khám phá UI").
- **Realtime**: `Có (2 tab)` / `Không` — case có cần xác minh push qua SignalR không.
- **Bước thực hiện**: 1, 2, 3...
- **Kết quả mong đợi**: ...
```

Khi business rule trong `.dod.md` đổi → rà lại case liên quan, sửa case (không tạo case trùng).

### Phần C — Case Status (luôn ở CUỐI file)

Mỗi case chỉ có **đúng 1 dòng**, ghi đè sau mỗi lần chạy (không tạo dòng trùng case ID):

```markdown
## Case Status

| Case ID | Trạng thái lần chạy gần nhất | Ngày | Bug liên quan |
|---------|-------------------------------|------|----------------|
| messages-01 | Pass | 2026-09-26 | - |
| messages-02 | Fail | 2026-09-26 | BUG-001 |
| messages-03 | Chưa chạy | - | - |
```

- Case mới thêm vào Phần B phải có dòng tương ứng ở đây ngay (mặc định `Chưa chạy`).
- **Trạng thái**: `Pass` / `Fail` / `Chưa chạy`.
- Lịch sử nhiều lần chạy của từng case **không** giữ ở đây (chỉ trạng thái mới nhất) — tra Run Log (Phần A).

## 4. `bugs.md` — Bug Tracker

Chỉ ghi **lỗi hệ thống thật** — behavior sai so với `.dod.md` hoặc lỗi rõ ràng (crash, exception, mất message, sai validation, message không realtime, attachment 403...). Append dòng mới ở **cuối** bảng:

| ID | Ngày phát hiện | Module | Mức độ | Mô tả + bước repro | Trạng thái | Test case liên quan | Ngày promote lên Nexus |
|----|----------------|--------|--------|---------------------|------------|----------------------|--------------------------|

- **Mức độ**: Critical (mất dữ liệu/crash/security/leak cross-tenant) · High (sai business rule, realtime không chạy) · Medium (sai UX rõ ràng nhưng không chặn flow) · Low (cosmetic).
- **Trạng thái**: Open · Fixed · WontFix.
- ID dạng `BUG-001`, `BUG-002`... tăng dần, không tái sử dụng số đã xoá.
- **Ngày promote lên Nexus**: mặc định `-`. Xem mục 5b.

## 5. `improvements.md` — Improvement Tracker

Ý tưởng cải tiến phát hiện trong lúc test — **không phải lỗi**. Append cuối bảng:

| ID | Ngày | Module | Loại | Đề xuất | Trạng thái | Ngày promote lên Nexus |
|----|------|--------|------|---------|------------|--------------------------|

- **Loại**: UX · Performance · Business logic suggestion.
- **Trạng thái**: Proposed · Accepted · Rejected · Done.
- ID dạng `IMP-001`, `IMP-002`...

## 5b. Promote ticket lên Nexus (DASHBOARD)

"Nexus" = app DASHBOARD (`C:\DEV\DASHBOARD`) — dùng WorkItem/Backlog của nó để track bug/improvement thật của HUB.

- **KHÔNG tự động promote.** Mọi dòng mặc định cột "Ngày promote lên Nexus" = `-`.
- Chỉ promote khi **user yêu cầu rõ ràng** (vd "promote BUG-003 lên Nexus").
- Khi được yêu cầu: tạo work item thật trong DASHBOARD (qua UI Playwright hoặc API), loại Bug/Task, nội dung lấy từ cột "Mô tả + bước repro" (bugs) hoặc "Đề xuất" (improvements). Tạo xong → điền ngày thật vào cột đó (không sửa cột khác).
- 1 dòng chỉ promote 1 lần — đã có ngày thì không tạo ticket trùng trừ khi user yêu cầu lại.

## 6. Khi nào update

Sau **mỗi lần** chạy `/self-test` (toàn bộ hoặc 1 module):

1. Append Run Log (Phần A) của module vừa test.
2. Ghi đè dòng case tương ứng trong Case Status (Phần C) — trạng thái + ngày.
3. Case Fail vì sai behavior → thêm dòng `bugs.md`, điền ID bug vào cột "Bug liên quan" ở Phần C.
4. Có ý tưởng cải tiến → thêm dòng `improvements.md`.
5. Module chưa có case (khung rỗng) → đọc `.dod.md`, soạn case vào Phần B + thêm dòng `Chưa chạy` vào Phần C **trước khi chạy**.

## 7. Ngôn ngữ

Viết tiếng Việt, giữ nguyên tên entity/field/route/component/service bằng tiếng Anh như trong code (đồng bộ `.claude/business/RULES.md`).
