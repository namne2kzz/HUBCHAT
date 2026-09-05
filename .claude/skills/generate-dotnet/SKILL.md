---
name: generate-dotnet
description: Use when generating production-ready .NET 10/C# 14 code (entity, command, query, controller, service, or repository) following this project's Clean Architecture conventions.
---

# Skill: Generate .NET Code

Generate production-ready .NET C# code following Clean Architecture.

## Usage
```
/generate-dotnet [type] [name] [context]
```

### Types
- `entity` — Domain entity with DDD patterns
- `command` — CQRS command + handler + validator
- `query` — CQRS query + handler + DTO
- `controller` — REST API controller
- `service` — Application/domain service
- `repository` — Repository interface + EF Core implementation

---

## Prompt Template

```
Generate .NET 10 C# code for the following:

**Type**: [entity|command|query|controller|service|repository]
**Name**: [ClassName or feature name]
**Domain**: [Brief domain description]

**Requirements**:
- [Functional requirement 1]
- [Functional requirement 2]

**Related entities/types**:
[List any related classes, enums, or dependencies]

**Layer**: [Domain|Application|Infrastructure|WebApi]

Generate following these standards:
- Clean Architecture layer rules (no upward dependencies)
- C# 14 features (primary constructors, collection expressions, etc.)
- Async/await with CancellationToken on all I/O
- FluentValidation for commands/queries
- MediatR IRequest<TResult> pattern
- Result<T> pattern for error handling (no exceptions for business failures)
- xUnit unit test class for the generated code
- XML doc comments on every method: `<summary>`, `<param>` for each parameter, `<returns>` (omit for void)
```

---

## Examples

### Entity Generation
```
Generate .NET 10 entity:
Type: entity
Name: Product
Domain: E-commerce product catalog

Requirements:
- Product has Name, SKU, Price (Money value object), Stock quantity
- Can be activated/deactivated
- Raises ProductCreated and ProductStockChanged domain events

Related: Money (value object), Category (aggregate)
Layer: Domain
```

### Command Generation
```
Generate .NET 10 command:
Type: command
Name: UpdateProductPrice
Domain: Pricing management

Requirements:
- Accept ProductId and new Price
- Validate: price > 0, product must exist and be active
- Raise PriceChangedDomainEvent with old and new price
- Return Result<Unit>

Related: Product entity, IProductRepository, IUnitOfWork
Layer: Application
```

---

## Output Format

Claude will provide:
1. Full C# code with `using` statements
2. Brief explanation of design decisions
3. Unit test class scaffold
4. Any required supporting types (DTOs, validators)
