# HUB — Docker / self-host

Toàn bộ hạ tầng chạy `docker-compose`, container **riêng của HUB** (add-on tách rời DASHBOARD).

## Chạy
```bash
cp .env.example .env      # điền JWT_SECRET + INTERNAL_API_TOKEN trùng DASHBOARD
docker compose --env-file .env up -d --build
```

## Services
| Service | Port | Ghi chú |
|---------|------|---------|
| gateway (YARP) | 8080 | entry point duy nhất; health `/health` |
| dashboard-gateway | (internal) | pull DASHBOARD `/internal/v1/*` + Redis cache |
| chat | (internal) | REST chat API; tự `Migrate()` DB `hub_chat` |
| realtime | (internal) | SignalR `/hubs/chat` + Redis backplane |
| postgres | 5432 | database-per-service (hub_chat, hub_directory...) |
| redis | 6379 | SignalR backplane + cache + presence |
| rabbitmq | 5672 / 15672 | broker riêng; UI :15672 |
| minio | 9000 / 9001 | object storage; console :9001 |
| otel-collector / jaeger | 4317 / 16686 | trace; Jaeger UI :16686 |

## Prod
- Reverse proxy Nginx/Traefik trước gateway + Let's Encrypt (HTTPS + WebSocket upgrade + sticky).
- Secrets qua Docker secret / `.env` (không commit). RabbitMQ nên mirror/cluster; DLQ + idempotent consumer.
- Scale: `docker compose up --scale chat=3`; realtime scale riêng nhờ Redis backplane.
