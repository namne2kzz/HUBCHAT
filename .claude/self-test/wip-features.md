# WIP Features — KHÔNG self-test

Module nằm trong bảng này đang code dở → self-test **bỏ qua hoàn toàn** (không navigate, không tạo case), báo cáo là `⏭️ Skipped (WIP)`.

| Module | Từ ngày | Lý do | Ai gỡ khỏi list |
|--------|---------|-------|-----------------|
| _(trống — mọi module đều được test)_ | - | - | - |

## Quy tắc

- Chỉ **user** thêm/gỡ dòng trong file này. Claude không tự thêm module vào đây để né test.
- User gọi `/self-test {module}` đích danh 1 module đang WIP → vẫn DỪNG, báo lý do, không cố chạy.
- Module được gỡ khỏi list → lần self-test sau chạy bình thường.
