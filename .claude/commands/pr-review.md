---
description: Run the full-stack PR review workflow (backend, frontend, database) with a structured review checklist. Different from the built-in /code-review skill — this is a project-specific multi-layer guided review.
argument-hint: [PR description or paste code]
---

# Workflow: Full-Stack PR Review

Full-stack code review workflow — backend + frontend + database.

## Usage
```
/pr-review [PR description or paste code]
```

---

## Quick Review (Single File)
```
[reviewer] Review this code:

**File**: [filename]
**Layer**: [Domain|Application|Infrastructure|WebApi|Angular Component|Angular Service]
**Language**: [C# | TypeScript]
**Purpose**: [Brief description of what this does]

**Code**:
[Paste code]

Focus on: [architecture | performance | security | all]
```

---

## Full PR Review

### Backend Review
```
[reviewer] Review this .NET backend change:

**PR Summary**: [What this PR does]
**Files changed**: [list key files]

**Domain/Application code**:
[Paste]

**Infrastructure code**:
[Paste]

**Controller code**:
[Paste]

**Migration** (if any):
[Paste]

**Tests**:
[Paste]

Check:
1. Clean Architecture layer compliance
2. DDD correctness (domain logic in right place)
3. Async/await correct usage (no deadlocks, CancellationToken)
4. EF Core: AsNoTracking, projections, N+1 prevention
5. FluentValidation on all commands
6. Logging: info/warning/error at right levels, no sensitive data
7. Security: authorization attributes, input validation
8. Migration: Down() implemented, safe for production
9. Test coverage: domain logic covered, edge cases
```

### Frontend Review
```
[reviewer] Review this Angular v20+ change:

**PR Summary**: [What this PR does]
**Files changed**: [list]

**Service code**:
[Paste]

**Component code** (template + class):
[Paste]

Check:
1. Standalone with correct imports
2. Signals used correctly (no direct mutation)
3. @if/@for/@defer (not *ngIf/*ngFor)
4. inject() over constructor injection
5. track in @for
6. OnPush on display components
7. HTTP in service, not component
8. RxJS: no memory leaks (takeUntilDestroyed or toSignal)
9. No sensitive data in localStorage
10. Error states handled in template
11. Loading states shown to user
12. Test spec present
```

### Database Review
```
[db-optimizer] Review this database change:

**Migration code**:
[Paste]

**New/changed queries** (EF Core or raw SQL):
[Paste]

Check:
1. Migration: Down() implemented
2. NOT NULL column added safely (nullable first)
3. Index for new FK columns
4. Online index creation on large tables
5. No raw SQL without parameterization
6. AsNoTracking on read queries
7. No N+1 risks introduced
8. Redis TTL set on all new cache entries
```

---

## Review Checklist Template
```markdown
## PR Review Checklist

### Architecture
- [ ] Layer dependencies correct (no violations)
- [ ] Business logic in correct layer
- [ ] No unnecessary abstractions

### Backend
- [ ] CancellationToken on all async methods
- [ ] AsNoTracking on read queries
- [ ] FluentValidation validator present
- [ ] Authorization attributes correct
- [ ] No sensitive data logged

### Frontend
- [ ] Standalone: true with correct imports
- [ ] Signals used (not BehaviorSubject)
- [ ] @if/@for control flow used
- [ ] OnPush change detection on display components
- [ ] track expression in @for

### Database
- [ ] Migration Down() implemented
- [ ] Index on new FK columns
- [ ] Redis TTL set

### Tests
- [ ] Unit tests for business logic
- [ ] Angular spec file present
- [ ] Edge cases covered
```
