# HUB

A real-time chat and collaboration add-on for [DASHBOARD](../DASHBOARD). Built as a set of independent microservices — chat, notifications, media, and real-time presence — communicating over RabbitMQ and exposed through a single YARP gateway. Self-hosted via Docker Compose.

> **HUB is an optional extension.** DASHBOARD runs independently; HUB requires a running DASHBOARD instance for authentication and user data.

## Tech Stack

| Layer | Technology |
| --- | --- |
| Backend | .NET 10 · C# 14 · Clean Architecture · DDD · CQRS · MediatR · FluentValidation |
| Gateway | YARP reverse proxy · JWT authentication · rate limiting |
| Messaging | RabbitMQ via MassTransit · transactional outbox |
| Real-time | SignalR · Redis backplane |
| Database | PostgreSQL 17 (database-per-service) · Redis 7 |
| Object Storage | MinIO (S3-compatible) |
| Observability | OpenTelemetry → Jaeger |
| Infrastructure | Docker · Docker Compose · GitHub Actions |
| Frontend | Angular 20 (planned — `HUB.VIEW`) |

## Services

| Service | Responsibility |
| --- | --- |
| `HUB.Gateway` | YARP entry point — JWT validation, routing, rate limiting |
| `HUB.DashboardGateway` | Pulls member/user data from DASHBOARD `/internal/v1/*` with Redis caching |
| `HUB.Chat` | Channels, messages, threads, reactions, keyset pagination, outbox |
| `HUB.Realtime` | SignalR hub, Redis backplane, presence, message fan-out |
| `HUB.Notification` | Notification persistence and delivery |
| `HUB.Media` | File uploads/downloads via MinIO presigned URLs |

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose v2)
- A running **DASHBOARD** instance (for SSO and internal API)
- .NET 10 SDK — only required for local development without Docker

## Quick Start

```bash
# 1. Copy and fill environment variables
#    JWT_SECRET and INTERNAL_API_TOKEN must match the values in DASHBOARD/.env
cp .env.example .env

# 2. Start all services
docker compose --env-file .env up -d --build
```

### Service Endpoints (local)

| Service | URL |
| --- | --- |
| Gateway (entry point) | <http://localhost:8080> |
| Gateway health | <http://localhost:8080/health> |
| RabbitMQ UI | <http://localhost:15672> |
| MinIO console | <http://localhost:9001> |
| Jaeger UI | <http://localhost:16686> |
| PgAdmin | <http://localhost:5050> |

## Integration with DASHBOARD

HUB uses DASHBOARD as its identity provider via a shared HMAC-SHA256 JWT secret. All tokens issued by DASHBOARD are accepted by HUB's gateway without an additional round-trip.

Two service-to-service tokens guard internal API calls:

| Token | Direction | Env variable |
| --- | --- | --- |
| Internal API token | HUB → DASHBOARD | `INTERNAL_API_TOKEN` |
| HUB Chat token | DASHBOARD → HUB | `HUB_CHAT_INTERNAL_TOKEN` |

Both values must be identical on each side. Set them in both `DASHBOARD/.env` and `HUB/.env`.

## Environment Variables

See `.env.example` for the full list. Key variables:

| Variable | Description |
| --- | --- |
| `JWT_SECRET` | Shared JWT signing secret — **must match** `DASHBOARD/.env` |
| `JWT_ISSUER / AUDIENCE` | Must match DASHBOARD's JWT configuration |
| `INTERNAL_API_TOKEN` | Token for HUB → DASHBOARD service calls |
| `HUB_CHAT_INTERNAL_TOKEN` | Token for DASHBOARD → HUB service calls |
| `POSTGRES_USER / PASSWORD` | PostgreSQL credentials |
| `RABBITMQ_USER / PASSWORD` | RabbitMQ credentials |
| `MINIO_ROOT_USER / PASSWORD` | MinIO credentials |

## Project Structure

```text
src/
├── Gateway/
│   └── HUB.Gateway                      # YARP: entry point, JWT, routing, rate limiting
├── BuildingBlocks/
│   ├── HUB.Shared.Auth                  # JWT validation, ICurrentUser, service-token middleware
│   ├── HUB.Shared.Contracts             # Versioned integration event contracts
│   ├── HUB.Shared.Messaging             # MassTransit + RabbitMQ setup
│   └── HUB.Shared.Observability         # OpenTelemetry, health checks
└── Services/
    ├── Chat/                            # Channel, message, thread, reaction, member
    ├── DashboardGateway/                # DASHBOARD internal API proxy + Redis cache
    ├── Media/                           # MinIO file storage
    ├── Notification/                    # Notification persistence
    └── Realtime/                        # SignalR hub + presence + fan-out

tests/
└── HUB.DashboardGateway.Tests

HUB.VIEW/                               # Angular 20 frontend (planned)
```

## Local Development (without Docker)

```bash
# Build & test
dotnet restore HUB.slnx
dotnet build   HUB.slnx -c Release
dotnet test    HUB.slnx -c Release
```

## Database Migrations

Each service owns its own PostgreSQL database and runs `Migrate()` automatically on startup. Migrations must be created before the first run:

```bash
dotnet tool install --global dotnet-ef

# Chat service (hub_chat)
dotnet ef migrations add InitialCreate \
  -p src/Services/Chat/HUB.Chat.Infrastructure \
  -s src/Services/Chat/HUB.Chat.WebApi \
  -o Persistence/Migrations

# Notification service (hub_notif)
dotnet ef migrations add InitialCreate \
  -p src/Services/Notification/HUB.Notification.Infrastructure \
  -s src/Services/Notification/HUB.Notification.WebApi \
  -o Persistence/Migrations

# Media service (hub_media)
dotnet ef migrations add InitialCreate \
  -p src/Services/Media/HUB.Media.Infrastructure \
  -s src/Services/Media/HUB.Media.WebApi \
  -o Persistence/Migrations
```

EF Core creates the database automatically if it does not exist.

## CI

GitHub Actions runs on every push to `main` and `develop`:

1. `dotnet restore` → `dotnet build` → `dotnet test`
2. On `main`: build Docker images for all services

See [`.github/workflows/ci.yml`](.github/workflows/ci.yml).

## License

Private repository — all rights reserved.
