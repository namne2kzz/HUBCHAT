# Bug Tracker — HUB Self-Test

Chỉ ghi **lỗi hệ thống thật** (behavior sai so với `.claude/business/{feature}.dod.md`, crash, mất message, sai validation, realtime không push, attachment lỗi quyền). Ý tưởng cải tiến → `improvements.md`.

Quy tắc chi tiết: `RULES.md` mục 4. Append dòng mới ở **cuối** bảng, ID tăng dần.

| ID | Ngày phát hiện | Module | Mức độ | Mô tả + bước repro | Trạng thái | Test case liên quan | Ngày promote lên Nexus |
|----|----------------|--------|--------|---------------------|------------|----------------------|--------------------------|
| _(chưa có bug)_ | - | - | - | - | - | - | - |

**Mức độ**: Critical (mất dữ liệu/crash/security/leak cross-tenant) · High (sai business rule, realtime không chạy) · Medium (sai UX rõ ràng, không chặn flow) · Low (cosmetic)
**Trạng thái**: Open · Fixed · WontFix
