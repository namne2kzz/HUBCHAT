# Presence — Business Doc

## Update Log

| Ngày | Giờ | Title | Thay đổi |
|------|-----|-------|----------|
| 2026-08-02 | 12:58 | Impl P2 | Presence gắn vào realtime-service: Hub OnConnected/Disconnected/Heartbeat ghi Redis, endpoint đọc presence |
| 2026-08-01 | 00:00 | Khởi tạo document | Tạo stub ban đầu |

---

## Purpose
Trạng thái online/away + typing của user, lưu ở **Redis TTL** (không RDBMS), phục vụ hiển thị realtime. Gắn trong **realtime-service** (service giữ WebSocket connection).

## Key Entities & Relationships
- Redis: `conn:{userId}` (set connectionId, TTL 90s) · `presence:{userId}` (status int, TTL 90s).
- `PresenceStatus`: Offline(0) · Online(1) · Away(2).
- `IPresenceStore` / `RedisPresenceStore`.

## Business Rules & Invariants
- User Online khi có ≥1 connection; Offline khi connection cuối rớt.
- Heartbeat (client gọi định kỳ ~30s) refresh TTL → tránh "kẹt online" khi mất mạng đột ngột.
- Typing: broadcast qua Hub (`typingStarted/Stopped`), KHÔNG lưu store.

## Main Workflows
1. `OnConnectedAsync` → `AddConnectionAsync`; nếu vừa online → broadcast `presenceChanged {status:Online}`.
2. `OnDisconnectedAsync` → `RemoveConnectionAsync`; nếu vừa offline → broadcast `presenceChanged {status:Offline}`.
3. `Heartbeat()` → refresh TTL.
4. `GET /api/v1/presence?userIds=g1,g2` → trạng thái hiện tại.

## Definition of Done
- [ ] Online/offline đúng theo số connection (multi-tab).
- [ ] TTL an toàn chống stale.
- [ ] Endpoint đọc presence có auth.

## Edge Cases & Notes
- Presence broadcast hiện tới `Clients.All` (P2) — GA nên scope theo workspace/contacts để giảm noise.
- unread count vẫn thuộc chat-service (`ChannelMember.LastReadAt`), không nằm ở presence.
