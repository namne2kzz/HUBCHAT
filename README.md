# NHub

A real-time chat and collaboration add-on for [NFlow](../DASHBOARD). Built as a set of independent microservices — chat, notifications, media, and real-time presence — communicating over RabbitMQ and exposed through a single YARP gateway. Self-hosted via Docker Compose.

> Formerly *Nexus HUB*. The repository, solution and service projects keep the `HUB` name.

> **NHub is an optional extension.** NFlow runs independently; NHub requires a running NFlow instance for authentication and user data.

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
| Frontend | Angular 19 (`HUB.VIEW`) |

## Services

| Service | Responsibility |
| --- | --- |
| `HUB.Gateway` | YARP entry point — JWT validation, routing, rate limiting |
| `HUB.DashboardGateway` | Pulls member/user data from NFlow `/internal/v1/*` with Redis caching |
| `HUB.Chat` | Channels, messages, threads, reactions, keyset pagination, outbox |
| `HUB.Realtime` | SignalR hub, Redis backplane, presence, message fan-out |
| `HUB.Notification` | Notification persistence and delivery |
| `HUB.Media` | File uploads/downloads via MinIO presigned URLs |

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose v2)
- A running **NFlow** instance (for SSO and internal API)
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
| Web (Angular) | <http://localhost:4202> |
| Gateway (entry point) | <http://localhost:8080> |
| Gateway health | <http://localhost:8080/health> |
| RabbitMQ UI (shared with NFlow) | <http://localhost:15672> |
| MinIO console | <http://localhost:9001> |
| Jaeger UI | <http://localhost:16686> |
| PgAdmin | <http://localhost:5480> |

## Integration with NFlow

NHub uses NFlow as its identity provider via a shared HMAC-SHA256 JWT secret. All tokens issued by NFlow are accepted by NHub's gateway without an additional round-trip.

Two service-to-service tokens guard internal API calls:

| Token | Direction | Env variable |
| --- | --- | --- |
| Internal API token | NHub → NFlow | `INTERNAL_API_TOKEN` |
| NHub Chat token | NFlow → NHub | `HUB_CHAT_INTERNAL_TOKEN` |

Both values must be identical on each side. Set them in both `DASHBOARD/.env` and `HUB/.env`.

## Environment Variables

See `.env.example` for the full list. Key variables:

| Variable | Description |
| --- | --- |
| `JWT_SECRET` | Shared JWT signing secret — **must match** `DASHBOARD/.env` |
| `JWT_ISSUER / AUDIENCE` | Must match NFlow's JWT configuration |
| `INTERNAL_API_TOKEN` | Token for NHub → NFlow service calls |
| `HUB_CHAT_INTERNAL_TOKEN` | Token for NFlow → NHub service calls |
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
    ├── DashboardGateway/                # NFlow internal API proxy + Redis cache
    ├── Media/                           # MinIO file storage
    ├── Notification/                    # Notification persistence
    └── Realtime/                        # SignalR hub + presence + fan-out

tests/
├── HUB.TestKit                          # shared builders + fakes
├── HUB.Chat.Domain.UnitTests            # domain invariants (no DB)
├── HUB.Chat.Application.UnitTests       # CQRS handlers, validators, cursor
├── HUB.Chat.IntegrationTests            # migrations + WebApi end-to-end (Testcontainers Postgres)
├── HUB.Notification.Domain.UnitTests
├── HUB.Notification.UnitTests           # consumer idempotency + handlers
├── HUB.Realtime.UnitTests               # SignalR fan-out consumer
├── HUB.Media.Domain.UnitTests
└── HUB.DashboardGateway.UnitTests

HUB.VIEW/                               # Angular 19 frontend
```

## Local Development (without Docker)

```bash
# Build & test
dotnet restore HUB.slnx
dotnet build   HUB.slnx -c Release
dotnet test    HUB.slnx -c Release

# Integration tests start a PostgreSQL container via Testcontainers. Without a Docker
# daemon, skip them and run the unit suites only:
dotnet test    HUB.slnx -c Release --filter "Category!=RequiresDocker"
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
