# Bug Tracker — HUB Self-Test

Chỉ ghi **lỗi hệ thống thật** (behavior sai so với `.claude/business/{feature}.dod.md`, crash, mất message, sai validation, realtime không push, attachment lỗi quyền). Ý tưởng cải tiến → `improvements.md`.

Quy tắc chi tiết: `RULES.md` mục 4. Append dòng mới ở **cuối** bảng, ID tăng dần.

| ID | Ngày phát hiện | Module | Mức độ | Mô tả + bước repro | Trạng thái | Test case liên quan | Ngày promote lên Nexus |
|----|----------------|--------|--------|---------------------|------------|----------------------|--------------------------|
| BUG-001 | 2026-09-27 | messages | Medium | `MessageCursor.TryDecode` throw `ArgumentOutOfRangeException` thay vì trả `null` khi tick count ngoài range `DateTime`. Doc comment ghi "returns null when the input is null/empty/invalid" nhưng `catch` chỉ bắt `FormatException`. Repro: `GET /api/v1/channels/{id}/messages?cursor=` + base64 của `"9223372036854775807:<guid>"` → `ExceptionHandlingMiddleware` rơi vào catch-all → **HTTP 500** thay vì bỏ qua cursor. Cursor đến trực tiếp từ query string nên client gửi được. | Fixed | `MessageCursorTests.ATickCountOutsideDateTimeRangeDecodesToNullRatherThanThrowing` | - |
| BUG-002 | 2026-09-27 | channels | Medium | `ChannelDto.MyRole` trả `null` ở 4 endpoint trả channel, dù caller đúng là member. `ChannelMappings.ToDto()` có param `myRole` optional mặc định `null`, và 4 handler gọi `ToDto()` **không truyền**: `CreateChannel` (creator là Owner), `UpdateChannel`, `OpenLinkedThread` (cả nhánh existing lẫn nhánh tạo mới). Thêm: `ListChannelsHandler` projection cũng không set `MyRole` → sidebar không phân biệt được channel mình sở hữu với channel chỉ join. `GetChannelHandler` thì project `MyRole` đầy đủ → cùng 1 channel, POST trả `MyRole: null` còn GET trả `2`. Hệ quả: client dựa vào `MyRole` để hiện nút owner (rename/archive/transfer) sẽ không hiện cho tới khi refetch. `IsMember` bị hardcode `true` ở `ToDto()` nên không bù được. Repro: `POST /api/v1/channels` → response `IsMember: true, MyRole: null`; `GET /api/v1/channels/{id}` cùng token → `MyRole: 2`. | Open | `AuthenticationTests.TheUidClaimIsWhatIdentifiesTheCaller` | - |

**Mức độ**: Critical (mất dữ liệu/crash/security/leak cross-tenant) · High (sai business rule, realtime không chạy) · Medium (sai UX rõ ràng, không chặn flow) · Low (cosmetic)
**Trạng thái**: Open · Fixed · WontFix
