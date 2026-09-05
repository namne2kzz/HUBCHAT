---
name: clean-architecture
description: Use when reviewing .NET code for Clean Architecture layer-dependency violations, designing a new feature's layer structure, or fixing a detected violation (anemic domain, fat controller, leaking infrastructure).
---

# Skill: Clean Architecture Review & Guidance

Validate and guide Clean Architecture implementation in .NET projects.

## Usage
```
/clean-architecture [review|design|fix] [context]
```

---

## Layer Dependency Rules

```
WebApi → Application → Domain   (allowed)
Infrastructure → Application    (allowed)
Infrastructure → Domain         (allowed)
Domain → Application            (FORBIDDEN)
Domain → Infrastructure         (FORBIDDEN)
Application → Infrastructure    (FORBIDDEN)
WebApi → Infrastructure         (FORBIDDEN — only via DI)
WebApi → Domain                 (FORBIDDEN — except for reading enums/constants)
```

## Prompt Templates

### Architecture Review
```
Review this .NET code for Clean Architecture compliance:

**File/Class**: [filename and class name]
**Layer**: [Domain|Application|Infrastructure|WebApi]
**Code**:
[Paste code]

Check for:
1. Layer dependency violations (references to wrong layers)
2. Business logic in wrong layer
3. Infrastructure leaking into Domain/Application
4. Missing abstractions (interfaces)
5. Anemic domain model (logic in services that should be in entities)
6. Over-engineering vs under-engineering

Provide:
- Violations found with severity (Critical/Warning/Info)
- Corrected code
- Explanation of why the fix is correct
- Pattern name if applicable
```

### Feature Design
```
Design Clean Architecture structure for this feature:

**Feature**: [Feature name and description]
**Domain**: [Business domain]
**Operations needed**:
- Create [Entity]: [details]
- Update [Entity]: [details]
- Query [Entity]: [details]
- [Other operations]

**Business Rules**:
- [Rule 1]
- [Rule 2]

**External dependencies**:
- Database: SQL Server / PostgreSQL
- Cache: Redis
- External API: [if any]

Provide:
1. Domain layer: Entities, Value Objects, Domain Events, Interfaces
2. Application layer: Commands, Queries, DTOs, Validators
3. Infrastructure layer: Repository implementations, DB config
4. WebApi layer: Controller endpoints, request/response models
5. Dependency injection registration
6. Test structure
```

### Fix Violation
```
Fix this Clean Architecture violation:

**Current Code** (violating):
[Paste code]

**Violation Type**: [e.g., "Business logic in controller", "EF Core in Domain"]

Provide:
- Refactored code split into correct layers
- No behavior change — only structural fix
- Updated DI registration if needed
```

---

## Common Violations Quick Reference

| Violation | Symptom | Fix |
|-----------|---------|-----|
| Anemic Domain | Logic in `*Service`, entity is just getters/setters | Move logic into entity methods |
| Fat Controller | Business rules in controller action | Move to Command/Application service |
| Direct DbContext | Controller/Domain uses `DbContext` directly | Inject `IRepository<T>` |
| Missing Abstraction | `Infrastructure` class used directly in `Application` | Extract interface in Domain/Application |
| Cross-Aggregate Reference | Aggregate navigates to another aggregate | Use ID references + Domain Services |
