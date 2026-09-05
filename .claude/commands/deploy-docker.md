---
description: Build, test and deploy HUB to a Docker host via docker-compose (self-host), with migration and health gate.
argument-hint: [target: local | staging | prod]
---

# Workflow: Deploy HUB (Docker self-host)

HUB deploy bằng `docker-compose` (container riêng của HUB). Không dùng cloud managed.

## Usage
```
/deploy-docker [target: local | staging | prod]
```

---

## Bước 1 — Pre-flight
```bash
# .env đã có JWT_SECRET + INTERNAL_API_TOKEN trùng DASHBOARD?
test -f .env || cp .env.example .env
# Build + test solution
dotnet build HUB.slnx -c Release && dotnet test HUB.slnx -c Release
```
**Pass:** build xanh, test pass, `.env` đủ secret.

## Bước 2 — EF migrations (chat-service)
```bash
# Tạo migration nếu chưa có (chỉ lần đầu / khi đổi schema)
dotnet ef migrations add <Name> \
  -p src/Services/Chat/HUB.Chat.Infrastructure \
  -s src/Services/Chat/HUB.Chat.WebApi \
  -o Persistence/Migrations
```
> chat-service tự `Migrate()` khi start (tự tạo DB `hub_chat`). Migration gồm cả bảng outbox.

## Bước 3 — Build & up
```bash
docker compose --env-file .env build
docker compose --env-file .env up -d
docker compose ps
```

## Bước 4 — Health gate
```bash
curl -fsS http://localhost:8080/health            # gateway
docker compose logs --tail=50 chat realtime dashboard-gateway
```
Kiểm tra: RabbitMQ UI :15672 (queues + outbox delivery), Jaeger :16686 (traces), MinIO :9001.

## Bước 5 — Prod (staging/prod)
- Reverse proxy Nginx/Traefik + Let's Encrypt (HTTPS + WebSocket upgrade + sticky cho SignalR).
- Secrets qua Docker secret (không `.env` plaintext trên prod).
- CI: build → test → Trivy scan → push image (GHCR) → SSH `docker compose pull && up -d`.
- Scale: `docker compose up -d --scale chat=3` (realtime scale riêng nhờ Redis backplane).

## Rollback
```bash
docker compose --env-file .env up -d --no-deps <service>=<previous-image-tag>
```
