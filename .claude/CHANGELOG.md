# HUB — Changelog

Ghi các mốc lớn của repo HUB. Mới nhất trên cùng.

## 2026-08-01
- **P1 scaffold**: chat-service (Domain/Application/Infrastructure/WebApi — Channel/Message aggregate, CQRS, keyset pagination, MassTransit transactional outbox) + realtime-service (SignalR `/hubs/chat`, Redis backplane, MessageSent consumer fan-out, typing).
- **P0 scaffold**: Gateway YARP, BuildingBlocks (Auth/Contracts/Messaging/Observability), dashboard-gateway (pull `/internal/v1/*` + Polly + Redis cache), docker-compose full (PG/Redis/RabbitMQ/MinIO/otel/jaeger), CI, xUnit tests.
- **`.claude`**: clone & adapt từ DASHBOARD cho stack microservices/RabbitMQ/SignalR/MinIO/Docker + business chat/meeting.
- Tài liệu: `PLAN.md`, `docs/DASHBOARD-INTERNAL-API.md`.
