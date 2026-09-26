---
name: microservices-structure
description: Use when adding a new HUB microservice or bounded context — folder layout, Clean Architecture layers, DI wiring, database-per-service, cross-service communication rules.
---

# Skill: HUB Microservice Structure

Mỗi service = 1 bounded context, Clean Architecture 4 lớp, DB riêng. Copy khung từ `Chat` service.

## Layout
```
src/Services/{Name}/
├── HUB.{Name}.Domain/           # entity/aggregate, enums, invariants (no deps)
├── HUB.{Name}.Application/       # CQRS Commands/Queries/DTOs/Behaviors/Interfaces (dep: Domain, Contracts)
├── HUB.{Name}.Infrastructure/    # EF Core (hub_{name}), Configurations, Outbox, IIntegrationEventPublisher
└── HUB.{Name}.WebApi/            # Controllers/{Feature}/Requests, ExceptionMiddleware, Program
```

## Layer deps (never violate)
Domain ← Application ← Infrastructure ← WebApi. Domain phụ thuộc **nothing**.

## DI wiring (WebApi Program)
```csharp
builder.Services.AddHubObservability("hub-{name}");
builder.Services.AddHubJwtAuth(builder.Configuration);
builder.Services.Add{Name}Application();       // MediatR + FluentValidation
builder.Services.Add{Name}Infrastructure(cfg); // EF Npgsql + MassTransit outbox
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
```

## Rule cross-service
- **1 service 1 database**; migration EF riêng; **không JOIN chéo DB**.
- Đồng bộ async qua **RabbitMQ** (outbox + idempotent consumer), không gọi DB service khác.
- Lấy data DASHBOARD chỉ qua `HUB.DashboardGateway` (pull + cache).
- Thêm service mới: đăng ký project vào `HUB.slnx`, thêm vào `docker-compose.yml` (+ route ở gateway nếu expose), tạo `business/{name}.dod.md`.

## Đừng
- ❌ Chia sẻ DbContext/DB giữa service. ❌ Reference chéo Domain của service khác (dùng Contracts + id).
