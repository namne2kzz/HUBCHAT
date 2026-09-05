---
description: Run the end-to-end full-stack feature workflow (architect → DB schema → backend CQRS → controller → Angular UI → review) for a new feature.
argument-hint: [feature-description]
---

# Workflow: Build Full-Stack Feature

End-to-end workflow for building a feature across backend (.NET) + frontend (Angular) + database.

> Lưu ý: Angular UI là bước tuỳ chọn (chưa scaffold HUB.VIEW). Hiện tập trung backend microservices.

## Usage
```
/build-feature [feature-description]
```

---

## Workflow Steps

### Step 1 — Architect (Agent: architect)
```
[architect] Design this feature end-to-end:

Feature: [Name]
Domain: [Business context]
User Story: As [user], I want to [action] so that [value]

Requirements:
- [Functional requirement 1]
- [Functional requirement 2]
- [Non-functional: performance, security, etc.]

Output needed:
- Domain model (entities, value objects)
- API contracts (endpoints, DTOs)
- Database schema changes
- Angular page/component structure
- Redis caching needs
```

### Step 2 — Database Schema
```
[db-optimizer] Design DB schema for this feature:

[Paste architect output — domain model section]

Current relevant tables:
[List existing related tables]

Requirements:
- [Query patterns this schema must support]
- [Expected data volume]

Provide:
- EF Core entity classes
- Entity configurations (IEntityTypeConfiguration)
- EF Core migration
- Index strategy
```

### Step 3 — Backend CQRS
```
[dotnet-coder] Generate backend code for this feature:

Domain model:
[Paste from Step 2]

Operations needed:
- Create [Entity]: [details]
- Update [Entity]: [details]
- Get [Entity]: [details]
- List [Entity]: [filtering, sorting, pagination]

For each operation provide:
- Command/Query record
- Handler class
- FluentValidation validator
- Repository interface method
- Unit tests
```

### Step 4 — API Controller
```
[dotnet-coder] Generate REST controller for this feature:

Commands/Queries available:
[Paste from Step 3]

API Requirements:
- Route prefix: /api/v1/[resource]
- Auth: [public | [Authorize] | [Authorize(Policy="X")]]
- Request/Response DTOs

Provide:
- Controller class with all endpoints
- Request record classes
- Response record classes
- OpenAPI summary attributes
```

### Step 5 — Angular Frontend
```
[angular-coder] Build Angular feature for:

Feature: [Name]
API endpoints:
[Paste from Step 4 — endpoint list]

Pages needed:
1. List page: [describe]
2. Detail/Form page: [describe]

Angular structure (HUB.VIEW/src/app (planned)/):
- pages/[feature-name]-page/      # Route-level smart component
- components/[component-name]/    # Shared display components
- services/[feature].service.ts   # Feature service
- models/[feature].model.ts       # Interfaces & types

Provide in order:
1. Feature service (Signals, HTTP)
2. List page component
3. Detail/Form component
4. Routing setup
5. Test specs
```

### Step 6 — Review
```
[reviewer] Review this full-stack feature:

Backend:
[Paste domain model + handlers + controller]

Frontend:
[Paste service + components]

Database:
[Paste entity config + migration]

Check for:
- Consistency between layers (naming, types)
- Missing validation (backend FluentValidation + frontend form validation)
- Security gaps (auth, input sanitization)
- Performance concerns (N+1, missing cache, Angular re-renders)
- Test coverage gaps
```

---

## Checklist
- [ ] Domain model reviewed for DDD correctness
- [ ] API contract agreed before implementation
- [ ] Migration is reversible (Down() implemented)
- [ ] Backend unit tests written
- [ ] Frontend test specs written
- [ ] Security review done
- [ ] Performance baseline measured
