# Messages — Business Doc

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-10-04 | — | Idempotent send (MP-3) | `PostMessage` nhận `ClientMessageId` (UUID, tuỳ chọn). Cùng tác giả gửi lại cùng key → trả message gốc (201), **không** publish lại `MessageSent`/`UserMentioned`. Unique `(AuthorId, ClientMessageId)` lọc NULL. Cùng key ở kênh khác → 409. HUB.VIEW sinh key mỗi lần gửi và retry lỗi mạng/502/503/504 với cùng key. |
| 2026-09-28 | — | Fix tie-breaker keyset (BUG-003) | `WHERE` thêm `(CreatedAt == cursor.CreatedAt && Id < cursor.Id)` để khớp `ORDER BY`. Message trùng timestamp không còn bị mất. Không đổi API contract. |
| 2026-09-27 | — | Fix cursor 500 + đo lại tie-breaker gap | `MessageCursor.TryDecode` giờ trả `null` cho tick count ngoài range `DateTime` (trước throw → HTTP 500, xem BUG-001). Tie cùng timestamp **không phải "cực hiếm"** mà là **mất message thật**: test đo được 4 message cùng tick, page size 2 → chỉ trả về 2, 2 cái còn lại không bao giờ xuất hiện. Cập nhật lại mục Edge Cases. |
| 2026-08-01 | 20:18 | Khởi tạo document | Tạo doc business ban đầu cho feature Messages (P1) |

---

## Purpose
Tin nhắn trong kênh: top-level hoặc thread reply, hỗ trợ mention, edit, soft-delete; là nguồn phát integration event cho realtime + notification.

## Key Entities & Relationships
- `Message` (aggregate root): `ChannelId`, `ParentId` (thread), `AuthorId`, `Body`, `Format` (`MessageFormat` Plain/Markdown), `Mentions` (jsonb List<Guid>), `EditedAt`, `DeletedAt`, `Reactions`.
- Publish: `MessageSent`, `UserMentioned` (mỗi mention) qua RabbitMQ (transactional outbox).

## Business Rules & Invariants
- Body bắt buộc (không rỗng) khi Post và Edit.
- Mentions distinct.
- Edit: chặn nếu đã xoá; set `EditedAt`. Chỉ tác giả được edit (enforce ở Application).
- Soft-delete: set `DeletedAt`, idempotent; không hiển thị trong list.
- Post yêu cầu caller là **member** của kênh và kênh **writable** (chưa archive).
- **Idempotent send**: `ClientMessageId` là khoá idempotency **theo tác giả** — `(AuthorId, ClientMessageId)` unique. Retry cùng key trả lại message gốc và không phát event lần 2; không gửi key (client cũ) thì không dedupe (gửi 2 lần cùng nội dung = 2 message). Key `Guid.Empty` bị từ chối. Cùng key nhưng khác kênh → từ chối (409), không trả message của kênh khác.

## Main Workflows
1. **Post**: `POST /api/v1/channels/{channelId}/messages` (Body, Format, ParentId?, MentionedUserIds, ClientMessageId?) → lưu + outbox publish `MessageSent` (+ `UserMentioned` per mention) → realtime fan-out.
2. **List**: `GET /api/v1/channels/{channelId}/messages?cursor=&limit=` → top-level (`ParentId=null`, chưa xoá), **keyset pagination** newest-first, kèm reactions.

## Definition of Done
- [ ] Membership + writable check trước khi post (ForbiddenException/DomainException).
- [ ] Publish event qua outbox (atomic với SaveChanges), không publish trực tiếp.
- [ ] List keyset dựa index `(channel_id, created_at)`; materialize rồi map DTO (mentions/reactions).
- [ ] Unit test domain: empty body→throw, distinct mentions, edit sau delete→throw, reaction idempotent.

## Edge Cases & Notes
- **Cursor keyset có tie-breaker `Id` (fix 2026-09-28 — BUG-003).** `WHERE` phải khớp `ORDER BY (CreatedAt desc, Id desc)`: `(CreatedAt < c.CreatedAt) || (CreatedAt == c.CreatedAt && Id < c.Id)`. Trước đó `WHERE` chỉ so `CreatedAt <` nên **mọi** message cùng timestamp với boundary bị nhảy qua cùng lúc — 4 message cùng tick, page size 2 → client chỉ thấy 2, 2 cái còn lại không bao giờ trả về. Bulk insert / import / seed đều sinh ra trường hợp này, không phải "cực hiếm" như ghi chú ban đầu. Cursor vốn đã mang `Id` sẵn nên fix không đổi API contract.
- **Retry đồng thời cùng key** (double click, retry chồng request cũ): cả hai qua bước tìm, unique index cho đúng 1 insert thắng; request thua rollback (cả outbox) rồi đọc lại message thắng → mọi caller đều nhận 201 cùng 1 message.
- Preview trong `MessageSent` cắt 140 ký tự cho notification.
- Cursor đến trực tiếp từ query string nên phải chịu được input rác: `TryDecode` trả `null` (bỏ qua cursor, về page đầu) cho base64 sai, payload sai format, và tick count ngoài range `DateTime`. Case cuối trước đây throw `ArgumentOutOfRangeException` → HTTP 500 (BUG-001, đã fix 2026-09-27).
