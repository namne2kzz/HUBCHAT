# Presence — Business Doc

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-10-14 | — | Custom status (CF-19) | Trục **riêng** song song presence: emoji + note + hạn tự xoá. Redis `custom:{userId}` + **native TTL = auto-expiry** (không job). Hub `SetCustomStatus(emoji,text,expiresAtUnixMs?)` → broadcast `customStatusChanged`; endpoint `GET /presence/custom`. Rule thuần (clear khi rỗng/quá hạn, cap 7 ngày) ở `CustomStatusPolicy`. |
| 2026-08-02 | 12:58 | Impl P2 | Presence gắn vào realtime-service: Hub OnConnected/Disconnected/Heartbeat ghi Redis, endpoint đọc presence |
| 2026-08-01 | 00:00 | Khởi tạo document | Tạo stub ban đầu |

---

## Purpose
Trạng thái online/away + typing của user, lưu ở **Redis TTL** (không RDBMS), phục vụ hiển thị realtime. Gắn trong **realtime-service** (service giữ WebSocket connection).

## Key Entities & Relationships
- Redis: `conn:{userId}` (set connectionId, TTL 90s) · `presence:{userId}` (status int, TTL 90s) · `manual:{userId}` (override Away/DnD) · `custom:{userId}` (JSON emoji+text+expiry, TTL = hạn hoặc cap 7 ngày).
- `PresenceStatus`: Offline(0) · Online(1) · Away(2) · DoNotDisturb(3).
- `CustomStatus` (emoji, text, expiresAt) — **trục riêng**, không phải giá trị của `PresenceStatus`.
- `IPresenceStore` / `RedisPresenceStore` · `CustomStatusPolicy` (rule thuần, test không cần Redis).

## Business Rules & Invariants
- User Online khi có ≥1 connection; Offline khi connection cuối rớt.
- Heartbeat (client gọi định kỳ ~30s) refresh TTL → tránh "kẹt online" khi mất mạng đột ngột.
- Typing: broadcast qua Hub (`typingStarted/Stopped`), KHÔNG lưu store.
- **Custom status** độc lập với presence: 1 user có thể vừa Online vừa có "🍔 Lunch". Emoji+text rỗng ⇒ clear; hạn đã qua ⇒ clear; không hạn ⇒ cap 7 ngày.
- Auto-expiry dựa **TTL của Redis** — không có job quét. Client cũng **đọc lại hạn** (`getCustomStatus` ẩn nếu quá hạn) để chống clock skew.

## Main Workflows
1. `OnConnectedAsync` → `AddConnectionAsync`; nếu vừa online → broadcast `presenceChanged {status:Online}`.
2. `OnDisconnectedAsync` → `RemoveConnectionAsync`; nếu vừa offline → broadcast `presenceChanged {status:Offline}`.
3. `Heartbeat()` → refresh TTL.
4. `GET /api/v1/presence?userIds=g1,g2` → trạng thái hiện tại.
5. `SetCustomStatus(emoji,text,expiresAtUnixMs?)` → lưu `custom:{uid}` (TTL) → broadcast `customStatusChanged`. Clear = text+emoji rỗng.
6. `GET /api/v1/presence/custom?userIds=` → custom status chưa hết hạn.

## Definition of Done
- [ ] Online/offline đúng theo số connection (multi-tab).
- [ ] TTL an toàn chống stale.
- [ ] Endpoint đọc presence có auth.

## Edge Cases & Notes
- Presence broadcast hiện tới `Clients.All` (P2) — GA nên scope theo workspace/contacts để giảm noise.
- unread count vẫn thuộc chat-service (`ChannelMember.LastReadAt`), không nằm ở presence.
