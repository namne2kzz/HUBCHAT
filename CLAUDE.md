# HUB — Chat/Meeting Microservices (add-on của DASHBOARD) — Claude Instructions

> HUB là **add-on tuỳ chọn** của DASHBOARD: dùng chung SSO (JWT HMAC) + pull data từ DASHBOARD `/internal/v1/*`. DASHBOARD chạy độc lập được; HUB thì không. Xem `PLAN.md` (kiến trúc) và `docs/DASHBOARD-INTERNAL-API.md` (hợp đồng API).

## Stack

| Layer | Tech |
|-------|------|
| Backend | .NET 10, C# 14, **Microservices** + Clean Architecture, DDD, CQRS, MediatR, FluentValidation |
| Messaging | **RabbitMQ** qua **MassTransit** (transactional outbox) — broker riêng của HUB |
| Realtime | **SignalR** + **Redis backplane** |
| Gateway | **YARP** reverse proxy (1 entry point, JWT, rate-limit, sticky WS) |
| Frontend | Angular v20, Signals, Standalone (chưa scaffold — thêm ở phase HUB.VIEW) |
| Database | **PostgreSQL 17** (EF Core 10, database-per-service), **Redis** (StackExchange.Redis) |
| Storage | **MinIO** (S3-compatible) — attachments qua presigned URL |
| Deploy | **Docker / docker-compose** (self-host, KHÔNG cloud managed) |
| Observability | OpenTelemetry → OTel Collector → Jaeger |
| CI | GitHub Actions |

---

## Project Structure

```
HUB/                                            # repo tách biệt (C:\DEV\HUB)
├── HUB.slnx                                     # solution (.slnx, .NET 10)
├── Directory.Build.props · Directory.Packages.props  # CPM, shared build
├── docker-compose.yml · .env.example            # full self-host stack
├── PLAN.md · docs/DASHBOARD-INTERNAL-API.md      # kiến trúc + hợp đồng DASHBOARD
├── src/
│   ├── Gateway/HUB.Gateway/                      # YARP: routing, JWT, rate-limit, WS
│   ├── BuildingBlocks/                           # shared libs (NuGet nội bộ)
│   │   ├── HUB.Shared.Contracts/                 # integration events (versioned)
│   │   ├── HUB.Shared.Auth/                      # JWT (shared secret) + ICurrentUser + service-token
│   │   ├── HUB.Shared.Messaging/                 # MassTransit + RabbitMQ
│   │   └── HUB.Shared.Observability/             # OpenTelemetry + health checks
│   └── Services/
│       ├── DashboardGateway/HUB.DashboardGateway/  # pull DASHBOARD /internal/v1/* + Redis cache
│       ├── Chat/                                 # bounded context Chat (4 lớp)
│       │   ├── HUB.Chat.Domain/                  # aggregate: Channel, Message; enums; invariants
│       │   ├── HUB.Chat.Application/             # CQRS: Commands/Queries/DTOs/Behaviors/Interfaces
│       │   ├── HUB.Chat.Infrastructure/          # EF Core (hub_chat), Configurations, Outbox
│       │   └── HUB.Chat.WebApi/                  # Controllers/{Feature}/Requests, middleware
│       ├── Realtime/HUB.Realtime.WebApi/         # SignalR ChatHub, Redis backplane, consumers
│       ├── Presence/        (planned P2)
│       ├── Notification/    (planned P2)
│       ├── Media/           (planned P2 — MinIO)
│       └── Meeting/         (planned P2 — LiveKit)
├── tests/                                        # xUnit (Domain, handlers, integration)
└── HUB.VIEW/                                     # Angular (planned)
```

Mỗi **service** = 1 bounded context, Clean Architecture 4 lớp riêng. KHÔNG JOIN chéo database; tham chiếu service khác bằng id + pull/cache hoặc integration event.

---

## Hard Constraints

### Clean Architecture — layer dependencies (per service, never violate)
| Layer | May depend on |
|-------|--------------|
| Domain | Nothing |
| Application | Domain only |
| Infrastructure | Application + Domain |
| WebApi | Application (+ Infrastructure cho DI wiring) |

### Microservices rules (non-negotiable)
- **1 service = 1 database** (PostgreSQL), migration EF Core riêng. Không JOIN chéo DB.
- Giao tiếp async giữa service qua **RabbitMQ (MassTransit)** + **transactional outbox** (save + publish atomic). Consumer **idempotent** (dedupe theo event id).
- Lấy data DASHBOARD **chỉ qua** `HUB.DashboardGateway` (REST pull + Redis cache) — không đọc DB DASHBOARD, không nghe broker DASHBOARD.
- Realtime push qua **SignalR + Redis backplane**; group `channel:{id}`, `user:{id}`.

### .NET rules (non-negotiable)
- async/await xuyên suốt — không `.Result`/`.Wait()`. `CancellationToken` trên mọi async method.
- `AsNoTracking()` + `Select()` cho read; **keyset pagination** cho list dài (messages).
- `IHttpClientFactory` (+ Polly resilience) — never `new HttpClient()`.
- `using`/`await using` cho mọi IDisposable.
- C# 14: primary constructors, collection expressions `[]`, pattern matching. `sealed` + `record` cho DTO/command/event.
- Config qua `IOptions<T>` / env; secrets qua Docker secret/env — không hardcode.

### File conventions
- .NET request records → `WebApi/Controllers/{Feature}/Requests/` — never inside Controller.
- Response DTOs → `Application/{Feature}/DTOs/` — never inside Controller.
- EF configurations → `Infrastructure/Persistence/Configurations/`.
- Integration events → `HUB.Shared.Contracts/Events/` (versioned).
- Angular (khi có): 4 files `.ts/.html/.scss/.spec.ts`; interfaces → `models/*.model.ts`.

### Code documentation — every public method
```csharp
/// <summary>One-line description.</summary>
/// <param name="ct">Cancellation token.</param>
/// <returns>Xyz if found; null otherwise.</returns>
```

---

## Automatic Workflow

Khi nhận bất kỳ yêu cầu code nào: **tự động** đọc `.claude/agents/auto.md` và follow flow (detect agent/skill theo keyword → hiện list → generate theo skills → post-gen review → update business doc). Không hỏi "Use auto agent?" trước.

Chỉ bỏ qua flow khi: yêu cầu rõ ràng không phải code (hỏi/giải thích/đọc file), hoặc user nói code "thuần".

Trước khi sinh code .NET, đọc thêm `.claude/memory/mistakes.md` (lỗi thường gặp) và `.claude/memory/patterns.md` (pattern nên dùng).

---

## Skills Reference (`.claude/skills/`)

| Area | Skills |
|------|--------|
| Backend core | `generate-dotnet` · `clean-architecture` · `ddd-cqrs` · `unit-testing` · `testcontainers` · `snapshot-testing` · `api-versioning` · `resilience-patterns` · `opentelemetry` · `aspire-orchestration` |
| Microservices | `microservices-structure` · `yarp-gateway` · `masstransit-rabbitmq` · `outbox-pattern` · `signalr-realtime` |
| Data | `efcore-postgresql` · `migrations` · `query-optimization` · `redis-cache` · `minio-storage` |

Agents: `auto` · `dotnet-coder` · `architect` · `reviewer` · `db-optimizer` · `security-auditor` · `build-error-resolver`.
Commands: `/build-feature` · `/fix-bug` · `/pr-review` · `/tdd` · `/security-scan` · `/health-check` · `/deploy-docker`.

---

## Business Documentation (`.claude/histories/`)

Sau khi thêm/sửa **business logic** của 1 feature (rule/workflow/permission/event mới) → PHẢI update file `.claude/histories/{feature}.dod.md` tương ứng.

- Đọc `.claude/histories/RULES.md` để biết quy tắc (Update Log + Business Doc, tiếng Việt, khi nào update).
- Mỗi feature 1 file `{feature}.dod.md` (channels, messages, reactions, members, presence, notifications, media, meeting, integration-dashboard).
- Thay đổi ảnh hưởng domain tổng thể → update thêm `domain-business.md`.
- Refactor kỹ thuật thuần (không đổi business) → không cần update.
