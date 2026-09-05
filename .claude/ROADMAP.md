# HUB — Roadmap

Chi tiết kiến trúc & phase ở `PLAN.md` (root). Tóm tắt:

| Phase | Nội dung | Trạng thái |
|-------|----------|-----------|
| **P0** | Foundation: Gateway (YARP), BuildingBlocks (Auth/Contracts/Messaging/Observability), dashboard-gateway (pull `/internal` + cache), docker-compose full, CI | ✅ scaffold |
| **P1** | chat-service (DDD 4 lớp + CQRS, outbox), realtime-service (SignalR + Redis backplane) | ✅ scaffold |
| **P2** | presence (in realtime), notification-service, media-service (MinIO) | ✅ scaffold |
| **P3** | Tích hợp DASHBOARD: mở thread từ work item, deep-link 2 chiều | ✅ scaffold |
| **P4** | Meeting + screen share (LiveKit self-host) | ⏳ |
| **P5** | Hardening: search-service, load test, retention/compliance, Angular HUB.VIEW | ⏳ |

**Việc cần trước khi chạy:** DASHBOARD expose `/internal/v1/*` (spec `docs/DASHBOARD-INTERNAL-API.md`); tạo EF migration cho chat-service (xem `README.md` root).
