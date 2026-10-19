<div align="center">

# 💬 NHub

### Real-time chat & collaboration, built as microservices

**Slack-grade messaging for [NFlow](../DASHBOARD)** — channels, threads, reactions, presence and file sharing,
delivered by independent .NET services over RabbitMQ, SignalR and a single YARP gateway.

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Angular](https://img.shields.io/badge/Angular-19-DD0031?logo=angular&logoColor=white)](https://angular.dev/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Redis](https://img.shields.io/badge/Redis-7-DC382D?logo=redis&logoColor=white)](https://redis.io/)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-MassTransit-FF6600?logo=rabbitmq&logoColor=white)](https://masstransit.io/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white)](https://www.docker.com/)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20%C2%B7%20DDD%20%C2%B7%20CQRS-2ea44f)](#-architecture)

</div>

> [!NOTE]
> Formerly *Nexus HUB* — the repository, solution and service projects keep the `HUB` name.
> **NHub is an optional extension:** NFlow runs on its own; NHub needs a running NFlow instance for
> authentication and user data.

---

## 📑 Table of Contents

- [Why NHub](#-why-nhub)
- [Chat features](#-chat-features)
- [Architecture](#-architecture)
- [Services](#-services)
- [Tech stack](#-tech-stack)
- [Quick start](#-quick-start)
- [Integration with NFlow](#-integration-with-nflow)
- [Configuration](#-configuration)
- [Project structure](#-project-structure)
- [Local development](#-local-development)
- [Testing](#-testing)
- [Database migrations](#-database-migrations)
- [Observability](#-observability)
- [CI](#-ci)

---

## ✨ Why NHub

NHub is a study in **production-grade distributed messaging** — not a monolith with a chat table, but a
fleet of services that stay correct under concurrency, reconnection and partial failure.

- 🧩 **Microservices, done properly** — database-per-service, Clean Architecture + DDD + CQRS in each.
- 📨 **Exactly-once-ish messaging** — MassTransit **transactional outbox**: state and events commit in one
  transaction, so a message is never saved without its realtime fan-out (or vice versa).
- ⚡ **Real-time at scale** — SignalR with a **Redis backplane**, so any node can push to any connection.
- 🔁 **Idempotent by design** — unique indexes + client message ids make retries and broker redeliveries safe.
- 🔒 **Access enforced live** — making a channel private evicts non-member watchers *immediately*, not on
  their next reconnect.
- 🪶 **Lean storage** — file bytes live in MinIO; services keep references and short-lived presigned URLs.

---

## 🚀 Chat features

A Slack/Teams-style feature set, each backed by real domain logic and tests.

| | Feature | What it does |
|---|---|---|
| ✅ | **@Mentions** | Autocomplete in the composer; server filters mentions to channel members; highlighted inline |
| ✅ | **Reactions** | Emoji reactions grouped with counts + tooltips; optimistic add/remove |
| ✅ | **Unread counts** | Server-side read point — accurate after reload and across devices |
| ✅ | **Edit / delete** | Edit within 24 h (author) · delete (author or moderator) → tombstone, realtime |
| ✅ | **File & media** | 2-phase presigned upload to MinIO; inline image/video, file chips, **Files & Media** panel |
| ✅ | **Threads** | Reply in a side panel; reply counts + last-reply time on the parent |
| ✅ | **Quote & forward** | Quote-reply with a preview, or forward a message to another channel |
| ✅ | **Server search** | Full-history search per channel, scoped to read access |
| ✅ | **Notification prefs** | "Turn off until…" + "only notify @mentions while muted" |
| ✅ | **Pinned messages** | Pin/unpin (idempotent); dialog from the header, realtime sync |
| ✅ | **Archive / reopen** | Owner/Admin lock a channel read-only; internal path for sprint close |
| ✅ | **Read receipts (DM)** | "Seen" under your last message, pushed only in direct messages |
| ✅ | **Drafts** | Unsent text per channel, restored when you switch back |
| ✅ | **Custom status** | Emoji + note with a Redis-TTL auto-expiry, independent of presence |
| ⬜ | Group DM · link unfurl · member roles · bookmarks · scheduled send · voice/video | On the roadmap |

<sub>Full tracker and design notes: `C:\DEV\docs\CHAT-FEATURES-HUB.md`.</sub>

---

## 🏗 Architecture

Every client request enters through one gateway; services never talk to each other synchronously for
chat flows — they publish events and react.

```
                           ┌──────────────────────────────┐
         Browser  ───────▶ │   HUB.Gateway  (YARP)        │  JWT · routing · rate limit
      (Angular 19)         └───────────────┬──────────────┘
                                           │ HTTP / WebSocket
         ┌─────────────────┬───────────────┼───────────────┬──────────────────┐
         ▼                 ▼               ▼               ▼                  ▼
   ┌───────────┐    ┌─────────────┐  ┌───────────┐   ┌─────────────┐   ┌───────────────────┐
   │ HUB.Chat  │    │HUB.Realtime │  │HUB.Media  │   │HUB.Notif.   │   │HUB.DashboardGateway│
   │ channels  │    │ SignalR hub │  │ MinIO     │   │ inbox/email │   │ NFlow /internal    │
   │ messages  │    │ presence    │  │ presigned │   │             │   │ + Redis cache      │
   └─────┬─────┘    └──────┬──────┘  └─────┬─────┘   └──────┬──────┘   └─────────┬──────────┘
         │ outbox          │ backplane     │               │                    │
         ▼                 ▼               ▼               ▼                    ▼
   ╔═══════════════════════════════════════════════════════════════════════════════════════╗
   ║   RabbitMQ (MassTransit)     Redis (backplane · presence · cache)     MinIO     Postgres ║
   ║                                                                            (db per svc) ║
   ╚═══════════════════════════════════════════════════════════════════════════════════════╝
                                           ▲
                                           │  shared JWT secret + service tokens
                                   ┌───────┴────────┐
                                   │     NFlow      │   identity · users · work items
                                   │  (DASHBOARD)   │
                                   └────────────────┘
```

**A message's journey** — `POST /messages` → Chat persists the row **and** an outbox event in one
transaction → MassTransit delivers `MessageSent` on RabbitMQ → Realtime fans it out to the channel's
SignalR group (via the Redis backplane) → Notification raises mentions. A crash between the save and the
publish is impossible: they share the transaction.

---

## 🧱 Services

| Service | Responsibility |
|---|---|
| **`HUB.Gateway`** | YARP entry point — JWT validation, routing, rate limiting, WebSocket passthrough |
| **`HUB.DashboardGateway`** | Pulls member/user data from NFlow `/internal/v1/*`, cached in Redis |
| **`HUB.Chat`** | Channels, messages, threads, reactions, pins, attachments — keyset pagination, outbox |
| **`HUB.Realtime`** | SignalR hub, Redis backplane, presence, custom status, realtime fan-out consumers |
| **`HUB.Notification`** | Notification inbox + email delivery (mention/DM) |
| **`HUB.Media`** | File uploads/downloads via MinIO presigned URLs; scan-status download gate |

---

## 🛠 Tech stack

| Layer | Technology |
|---|---|
| **Backend** | .NET 10 · C# 14 · Clean Architecture · DDD · CQRS · MediatR · FluentValidation |
| **Gateway** | YARP reverse proxy · JWT auth · rate limiting |
| **Messaging** | RabbitMQ via MassTransit · EF Core transactional outbox |
| **Real-time** | SignalR · Redis backplane · Redis presence store |
| **Database** | PostgreSQL 17 (database-per-service) · Redis 7 |
| **Object storage** | MinIO (S3-compatible), presigned PUT/GET |
| **Observability** | OpenTelemetry → Jaeger (distributed tracing) |
| **Infrastructure** | Docker · Docker Compose · GitHub Actions |
| **Frontend** | Angular 19 (standalone + Signals) — `HUB.VIEW` |

---

## ⚡ Quick start

> [!IMPORTANT]
> `JWT_SECRET` and `INTERNAL_API_TOKEN` **must match** the values in `DASHBOARD/.env`, and NFlow must be
> running — NHub trusts its tokens and pulls user data from it.

```bash
# 1. Copy and fill environment variables
cp .env.example .env

# 2. Build and start the whole stack
docker compose --env-file .env up -d --build
```

### Endpoints (local)

| What | URL |
|---|---|
| 🌐 Web (Angular) | <http://localhost:4202> |
| 🚪 Gateway (entry point) | <http://localhost:8080> |
| ❤️ Gateway health | <http://localhost:8080/health> |
| 🐇 RabbitMQ UI | <http://localhost:15672> |
| 🪣 MinIO console | <http://localhost:9001> |
| 🔍 Jaeger UI | <http://localhost:16686> |
| 🗄 pgAdmin | <http://localhost:5480> |

---

## 🔗 Integration with NFlow

NHub uses NFlow as its identity provider through a **shared HMAC-SHA256 JWT secret** — every token NFlow
issues is accepted by NHub's gateway with no extra round-trip. Two service tokens guard the internal APIs:

| Token | Direction | Env variable |
|---|---|---|
| Internal API token | NHub → NFlow | `INTERNAL_API_TOKEN` |
| NHub Chat token | NFlow → NHub | `HUB_CHAT_INTERNAL_TOKEN` |

Both must be identical on each side — set them in **both** `DASHBOARD/.env` and `HUB/.env`.

---

## ⚙️ Configuration

See `.env.example` for the full list. Key variables:

| Variable | Description |
|---|---|
| `JWT_SECRET` | Shared JWT signing secret — **must match** `DASHBOARD/.env` |
| `JWT_ISSUER` / `JWT_AUDIENCE` | Must match NFlow's JWT configuration |
| `INTERNAL_API_TOKEN` | Token for NHub → NFlow service calls |
| `HUB_CHAT_INTERNAL_TOKEN` | Token for NFlow → NHub service calls |
| `POSTGRES_USER` / `POSTGRES_PASSWORD` | PostgreSQL credentials |
| `RABBITMQ_USER` / `RABBITMQ_PASSWORD` | RabbitMQ credentials |
| `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` | MinIO credentials |

---

## 📂 Project structure

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
    ├── Chat/                            # Channels, messages, threads, reactions, pins, attachments
    ├── DashboardGateway/                # NFlow internal API proxy + Redis cache
    ├── Media/                           # MinIO file storage
    ├── Notification/                    # Notification inbox + email
    └── Realtime/                        # SignalR hub + presence + fan-out consumers

tests/
├── HUB.TestKit                          # shared builders + fakes
├── HUB.Chat.Domain.UnitTests            # domain invariants (no DB)
├── HUB.Chat.Application.UnitTests       # CQRS handlers, validators, cursor (SQLite)
├── HUB.Chat.IntegrationTests            # migrations + WebApi end-to-end (Testcontainers Postgres)
├── HUB.Realtime.UnitTests               # SignalR fan-out + revoke consumers
├── HUB.Notification.*                   # consumer idempotency + handlers
├── HUB.Media.Domain.UnitTests
└── HUB.DashboardGateway.UnitTests

HUB.VIEW/                                # Angular 19 frontend
```

---

## 💻 Local development

No Docker required for the services themselves (you still need Postgres/Redis/RabbitMQ, or run only the
unit suites).

```bash
dotnet restore HUB.slnx
dotnet build   HUB.slnx -c Release
```

Frontend:

```bash
cd HUB.VIEW
npm install
npm start          # http://localhost:4202
```

---

## 🧪 Testing

| Suite | Scope | Needs |
|---|---|---|
| `*.Domain.UnitTests` | Entity invariants, pure rules | — |
| `*.Application.UnitTests` | CQRS handlers, validators, keyset cursor | SQLite (in-proc) |
| `*.IntegrationTests` | Full HTTP → PostgreSQL → events | **Docker** (Testcontainers) |
| `HUB.Realtime.UnitTests` | SignalR fan-out, revoke/receipt consumers | — |

```bash
# Everything
dotnet test HUB.slnx -c Release

# Unit only — no Docker daemon needed
dotnet test HUB.slnx -c Release --filter "Category!=RequiresDocker"
```

> [!TIP]
> On Windows, if freshly built DLLs fail to load with **`0x800711C7` (An Application Control policy has
> blocked this file)**, that's **Smart App Control**, not your code. Turn it off in *Windows Security →
> App & browser control → Smart App Control*, or clean `bin/obj` and rebuild.

---

## 🗃 Database migrations

Each service owns its own PostgreSQL database and runs `Migrate()` on startup. Create migrations before
the first run:

```bash
dotnet tool install --global dotnet-ef

# Chat service (hub_chat)
dotnet ef migrations add InitialCreate \
  -p src/Services/Chat/HUB.Chat.Infrastructure \
  -s src/Services/Chat/HUB.Chat.WebApi \
  -o Persistence/Migrations

# Notification (hub_notif) and Media (hub_media) follow the same shape
```

EF Core creates the database automatically if it does not exist.

---

## 🔭 Observability

Every service is instrumented with **OpenTelemetry** and exports traces to **Jaeger** (<http://localhost:16686>).
Because chat flows are asynchronous (outbox → RabbitMQ → consumer), a single user action spans several
services — open a trace to see the hop from `HUB.Chat` through the broker into `HUB.Realtime`.

---

## 🔄 CI

GitHub Actions runs on every push to `main` and `develop`:

1. `dotnet restore` → `dotnet build` → `dotnet test`
2. On `main`: build Docker images for all services

See [`.github/workflows/ci.yml`](.github/workflows/ci.yml).

---

<div align="center">

**Private repository — all rights reserved.**

<sub>Built with .NET 10 · Angular 19 · a lot of integration events.</sub>

</div>
