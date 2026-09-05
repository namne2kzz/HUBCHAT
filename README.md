# HUB

Chat & meeting add-on cho hệ sinh thái **DASHBOARD** (Teams-like). Microservices, self-host, deploy bằng Docker. Xem `PLAN.md` (kiến trúc) và `docs/DASHBOARD-INTERNAL-API.md` (hợp đồng API với DASHBOARD).

> HUB là **add-on tuỳ chọn**: DASHBOARD chạy độc lập được, HUB thì không (cần SSO + data từ DASHBOARD).

## Yêu cầu
- .NET 10 SDK, Docker + Docker Compose.

## Chạy local (P0 foundation)
```bash
cp .env.example .env          # điền JWT_SECRET & INTERNAL_API_TOKEN trùng với DASHBOARD
docker compose --env-file .env up -d --build
```
- Gateway (entry point): http://localhost:8080  · health: `/health`
- RabbitMQ UI: http://localhost:15672 · MinIO console: http://localhost:9001 · Jaeger: http://localhost:16686

## Build & test (không Docker)
```bash
dotnet restore HUB.slnx
dotnet build   HUB.slnx -c Release
dotnet test    HUB.slnx -c Release
```

## Cấu trúc
```
src/
  Gateway/HUB.Gateway                     # YARP: entry point, JWT, routing, rate-limit
  BuildingBlocks/
    HUB.Shared.Contracts                  # Integration event contracts (versioned)
    HUB.Shared.Auth                       # JWT (shared secret) + ICurrentUser + service-token
    HUB.Shared.Messaging                  # MassTransit + RabbitMQ
    HUB.Shared.Observability              # OpenTelemetry + health checks
  Services/
    DashboardGateway/HUB.DashboardGateway # Pull DASHBOARD /internal/v1/* + cache Redis
tests/
  HUB.DashboardGateway.Tests
```

## EF Core migrations (bắt buộc — chat / notification / media)
Mỗi service tự `Migrate()` khi khởi động, nhưng **migration phải được tạo trước** (mỗi service 1 DB riêng):
```bash
dotnet tool install --global dotnet-ef      # nếu chưa có

# chat (hub_chat) — gồm cả bảng outbox MassTransit
dotnet ef migrations add InitialCreate -p src/Services/Chat/HUB.Chat.Infrastructure         -s src/Services/Chat/HUB.Chat.WebApi         -o Persistence/Migrations

# notification (hub_notif)
dotnet ef migrations add InitialCreate -p src/Services/Notification/HUB.Notification.Infrastructure -s src/Services/Notification/HUB.Notification.WebApi -o Persistence/Migrations

# media (hub_media) — gồm cả bảng outbox MassTransit
dotnet ef migrations add InitialCreate -p src/Services/Media/HUB.Media.Infrastructure       -s src/Services/Media/HUB.Media.WebApi       -o Persistence/Migrations
```
EF `Migrate()` tự tạo database nếu chưa có.

## Đã có trong P0
SSO validate JWT (HMAC dùng chung DASHBOARD), gateway YARP, dashboard-gateway pull `/internal/v1/*` + cache, MassTransit/RabbitMQ, OpenTelemetry→Jaeger, health checks, docker-compose full, CI.

## Đã có trong P1
- **chat-service** (DDD 4 lớp + CQRS): channel, message, thread, reaction, member, keyset pagination, transactional outbox (publish `MessageSent`/`UserMentioned`).
  - `POST/GET /api/v1/channels`, `POST /api/v1/channels/{id}/members`, `POST /api/v1/channels/{id}/read`
  - `POST/GET /api/v1/channels/{channelId}/messages`, `POST /api/v1/messages/{id}/reactions`
- **realtime-service**: SignalR hub `/hubs/chat` (JWT qua `?access_token=`), Redis backplane, consumer `MessageSent` fan-out `messageReceived` + typing indicators.

## Chưa (P2+)
presence-service, notification-service, media-service (MinIO), meeting (LiveKit — P2), Angular `HUB.VIEW`. Kiểm tra membership trong `ChatHub.JoinChannel` (hiện để TODO trước GA).
