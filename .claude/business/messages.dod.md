# Messages — Business Doc

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-10-17 | — | Gửi file/ảnh E2E (CF-5) | `PostMessage` nhận `Attachments[]` (fileId + kind/name/size/mime/w/h); Chat lưu `Attachment.Url = fileId` (snapshot, không gọi chéo Media lúc list). Tin **chỉ-attachment** (body rỗng) hợp lệ; giới hạn ≤10 file, mỗi file ≤25MB (validator). `MessageSent` mang `Attachments` để realtime render ngay; preview "📎 N attachments" khi không có text. Query mới `ListChannelAttachments` (kind filter, loại tin đã xoá, gated read access) cho panel Files/Media. Upload 2-phase qua Media presigned (MinIO); download URL presigned 5' resolve lúc render. |
| 2026-10-14 | — | Draft giữ khi đổi kênh (CF-17) | Thuần FE: composer lưu bản nháp per-kênh vào `localStorage` (`hub.draft.{channelId}`), restore khi quay lại kênh, xoá khi gửi. **Không** đẩy server — draft là tiện ích per-browser, không phải dữ liệu domain (không sync đa thiết bị, mất khi clear site-data là chấp nhận được). Không đổi API/event/rule. |
| 2026-10-14 | — | Quote-reply + Forward (CF-7) | PostMessage nhận `ReplyToId` (trích 1 tin, vẫn là tin top-level + ô preview, bấm nhảy tới gốc). `POST /messages/{id}/forward` (TargetChannelId) — copy body sang kênh đích, ghi `ForwardedFromId`; chặn nếu không đọc được nguồn / không là member đích / nguồn đã xoá. UI: menu Reply (→ reply-bar trên composer) + Forward (→ dialog chọn kênh); hiện ô quote + nhãn "Forwarded". |
| 2026-10-14 | — | Thread / trả lời theo luồng (CF-6) | `GET /channels/{id}/messages/{messageId}/replies` (reply của 1 tin gốc, cũ→mới); list kênh trả thêm `ReplyCount` + `LastReplyAt` cho tin top-level (tin xoá không tính). Reply = message có `ParentId`; realtime dùng lại `MessageSent` (đã mang ParentId). UI: dòng "N replies" dưới tin → mở **thread panel** bên phải (tin gốc + reply + composer gửi reply). |
| 2026-10-11 | — | Sửa / xoá tin (CF-4) | PATCH `/messages/{id}` (sửa, tác giả, trong 24h) → `MessageEdited`; DELETE `/messages/{id}` (tác giả trong 24h HOẶC Owner/Admin/ManageChannels, không giới hạn giờ) → `MessageDeleted`, soft-delete thành **tombstone**. List vẫn trả tin đã xoá nhưng body rỗng + `IsDeleted`; unread không tính tin đã xoá. Realtime push `messageEdited`/`messageDeleted` (sửa lại tên camelCase — trước PascalCase nên FE không nhận). UI: hover hiện nút sửa/xoá, sửa inline (Enter lưu/Esc huỷ), xoá có confirm, optimistic. |
| 2026-10-10 | — | @mention từ UI + lọc mention phía server | Composer: gõ `@` → gợi ý thành viên kênh (↑/↓, Enter/Tab, Esc), gửi `mentionedUserIds` (chỉ người còn `@Tên` trong nội dung). Server: kênh private/DM chỉ giữ mention là **member** (chặn lộ preview nội dung kênh private qua notification); không báo khi tự mention; tối đa 50 mention/tin. |
| 2026-10-08 | — | Realtime push message đầy đủ (MP-5B) | `MessageSent` mang thêm `Body`, `Format`, `ParentId`, `CreatedAt` (tuỳ chọn, cuối record — tương thích event cũ). Người nhận thấy đúng message (đủ độ dài, render markdown) ngay, không còn bản rút gọn 140 ký tự. Reply trong thread không còn lọt vào timeline chính qua realtime. |
| 2026-10-08 | — | Catch-up sau reconnect + chống trùng phía client (MP-5) | Endpoint mới `GET /channels/{id}/messages/after/{messageId}?limit=` — message top-level mới hơn mốc, cũ→mới, keyset `(CreatedAt, Id)`; mốc không thuộc kênh → 404. HUB.VIEW: reconnect → join lại group **rồi** tải phần lỡ (tối đa 5 trang × 100, quá thì tải lại trang mới nhất); mọi đường thêm message gộp qua `mergeMessages` (dedupe theo id, bản đầy đủ thay bản preview realtime). |
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
- Mentions distinct. **Ai được mention** (luật `Channel.MentionableAmong`): kênh public → bất kỳ ai; kênh private/DM/GroupDM → chỉ member (người ngoài bị bỏ khỏi mentions, không nhận notification — tránh lộ preview kênh private). Tự mention: vẫn lưu trên message nhưng không phát `UserMentioned`. Tối đa 50 mention/tin (validator).
- **Edit**: chỉ **tác giả**, trong **24h** (`Message.AuthorEditWindow`), tin chưa xoá, body ≤ 8000. Moderator KHÔNG sửa hộ (sửa = đổi nội dung). Set `EditedAt`; phát `MessageEdited`.
- **Soft-delete → tombstone**: tác giả (trong 24h) hoặc moderator kênh (Owner/Admin/ManageChannels, không giới hạn giờ). Giữ chỗ trong list (giữ thread) nhưng DTO xoá body/reactions/attachments + `IsDeleted=true`. Idempotent (xoá lại = no-op, không phát event lần 2). Phát `MessageDeleted`.
- Post yêu cầu caller là **member** của kênh và kênh **writable** (chưa archive).
- **Idempotent send**: `ClientMessageId` là khoá idempotency **theo tác giả** — `(AuthorId, ClientMessageId)` unique. Retry cùng key trả lại message gốc và không phát event lần 2; không gửi key (client cũ) thì không dedupe (gửi 2 lần cùng nội dung = 2 message). Key `Guid.Empty` bị từ chối. Cùng key nhưng khác kênh → từ chối (409), không trả message của kênh khác.

## Main Workflows
1. **Post**: `POST /api/v1/channels/{channelId}/messages` (Body, Format, ParentId?, MentionedUserIds, ClientMessageId?) → lưu + outbox publish `MessageSent` (+ `UserMentioned` per mention) → realtime fan-out.
3. **Catch-up**: `GET /api/v1/channels/{channelId}/messages/after/{messageId}?limit=` → message top-level (chưa xoá) **mới hơn** mốc, cũ→mới, tối đa 100; client lặp với id cuối tới khi nhận ít hơn `limit`. Mốc không thuộc kênh → 404 (client tải lại). Cùng quyền đọc như List.
2. **List**: `GET /api/v1/channels/{channelId}/messages?cursor=&limit=` → top-level. Mỗi tin top-level kèm `ReplyCount`/`LastReplyAt` (summary thread).
5. **Thread**: `GET /api/v1/channels/{channelId}/messages/{messageId}/replies` → reply (ParentId = tin gốc), cũ→mới, cùng quyền đọc. Gửi reply = PostMessage với `ParentId`. (`ParentId=null`, chưa xoá), **keyset pagination** newest-first, kèm reactions.

## Definition of Done
- [ ] Membership + writable check trước khi post (ForbiddenException/DomainException).
- [ ] Publish event qua outbox (atomic với SaveChanges), không publish trực tiếp.
- [ ] List keyset dựa index `(channel_id, created_at)`; materialize rồi map DTO (mentions/reactions).
- [ ] Unit test domain: empty body→throw, distinct mentions, edit sau delete→throw, reaction idempotent.

## Edge Cases & Notes
- **Cursor keyset có tie-breaker `Id` (fix 2026-09-28 — BUG-003).** `WHERE` phải khớp `ORDER BY (CreatedAt desc, Id desc)`: `(CreatedAt < c.CreatedAt) || (CreatedAt == c.CreatedAt && Id < c.Id)`. Trước đó `WHERE` chỉ so `CreatedAt <` nên **mọi** message cùng timestamp với boundary bị nhảy qua cùng lúc — 4 message cùng tick, page size 2 → client chỉ thấy 2, 2 cái còn lại không bao giờ trả về. Bulk insert / import / seed đều sinh ra trường hợp này, không phải "cực hiếm" như ghi chú ban đầu. Cursor vốn đã mang `Id` sẵn nên fix không đổi API contract.
- **Retry đồng thời cùng key** (double click, retry chồng request cũ): cả hai qua bước tìm, unique index cho đúng 1 insert thắng; request thua rollback (cả outbox) rồi đọc lại message thắng → mọi caller đều nhận 201 cùng 1 message.
- **Realtime không lưu message** (Redis backplane là pub/sub): rớt kết nối là mất push trong khoảng đó → client bù bằng Catch-up sau khi đã join lại group (join trước, fetch sau — không có khe).
- Push realtime mang **message đầy đủ lúc post** (body, format, parent, createdAt) → người nhận thấy giống hệt sau reload. Event cũ (trước MP-5B) chỉ có `preview` → client hiển thị preview dạng Plain tới khi reload/catch-up. Reply (`ParentId` ≠ null) không được đưa vào timeline chính (khớp API list top-level).
- Preview trong `MessageSent` cắt 140 ký tự cho notification.
- Cursor đến trực tiếp từ query string nên phải chịu được input rác: `TryDecode` trả `null` (bỏ qua cursor, về page đầu) cho base64 sai, payload sai format, và tick count ngoài range `DateTime`. Case cuối trước đây throw `ArgumentOutOfRangeException` → HTTP 500 (BUG-001, đã fix 2026-09-27).
