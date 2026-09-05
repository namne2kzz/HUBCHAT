# Hook: Pre-Generation Checks

Checks to run BEFORE generating code to ensure the request is well-formed.

## When to Apply
Before Claude generates any significant code (entities, commands, controllers, Angular components).

---

## Pre-Generation Checklist

### Context Completeness
Before generating code, verify:
- [ ] Layer is specified (Domain / Application / Infrastructure / WebApi / Angular)
- [ ] Feature/domain context is clear
- [ ] Related entities/types are mentioned
- [ ] Performance/scale requirements stated (if relevant)
- [ ] Auth requirements clear (public / authenticated / role-based)

### Architecture Consistency
Ask Claude to check:
```
Before generating, confirm:
1. Does this belong in the [Layer] layer per Clean Architecture rules?
2. Are there existing similar classes I should follow the pattern of?
3. Does this require any new dependencies (NuGet / npm packages)?
4. Are there any security implications (user input, PII, financial)?
```

### Naming Consistency
```
Before generating, confirm naming follows project conventions:
- C# classes: PascalCase
- C# fields: _camelCase
- C# methods: PascalCase, async suffix Async
- Angular components: PascalCase class, kebab-case selector
- Angular services: PascalCase ending in Service
- DB tables: PascalCase (PostgreSQL) / snake_case (PostgreSQL)
- Redis keys: {service}:{entity}:{id} format
```

---

## Pre-Generation Prompt
```
Before generating code for [feature/component]:

1. Confirm the layer: [Domain|Application|Infrastructure|WebApi|Angular]
2. List any assumptions you're making about:
   - Existing types/interfaces this will depend on
   - Database schema
   - Auth/authorization requirements
3. Flag any ambiguities that could affect the design
4. Confirm tech stack:
   - Backend: .NET 10, C# 14, EF Core 10, MediatR 12
   - Frontend: Angular 20, TypeScript strict, Signals
   - DB: PostgreSQL + Redis
5. Confirm test framework:
   - Backend: xUnit + NSubstitute + Shouldly
   - Frontend: Jasmine + Angular Testing Library

Then generate the code.
```
