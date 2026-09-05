---
name: ddd-cqrs
description: Use when designing a DDD aggregate, domain event flow, CQRS query, or a saga/process manager in .NET — covers invariants, domain events, read-model optimization, and compensating transactions.
---

# Skill: DDD & CQRS Patterns

Design and implement Domain-Driven Design patterns with CQRS in .NET.

## Usage
```
/ddd-cqrs [aggregate|event|saga|query|service] [context]
```

---

## Prompt Templates

### Aggregate Design
```
Design a DDD aggregate for this domain concept:

**Aggregate Name**: [Name]
**Business Context**: [Describe the bounded context]

**Business Rules** (invariants the aggregate must enforce):
1. [Rule 1 — always true]
2. [Rule 2 — always true]
3. [Rule 3]

**Operations** (behaviors on this aggregate):
- [Create]: [conditions and effects]
- [Update X]: [conditions and effects]
- [Delete]: [conditions and effects]

**Relationships**:
- Owns: [list of child entities/value objects owned by aggregate]
- References by ID: [other aggregates this knows about via ID only]

**Domain Events to raise**:
- [EventName] when [condition]
- [EventName] when [condition]

Provide:
1. Aggregate root class with all invariant enforcement
2. Child entity classes
3. Value object classes
4. Domain event record classes
5. Repository interface
6. Unit tests for each business rule
```

### Domain Event Design
```
Design domain event flow for this business process:

**Process**: [Business process name]
**Trigger**: [What starts this process]

**Event Chain**:
1. [AggregateA] raises [EventX] when [condition]
2. [HandlerB] handles [EventX] and creates/updates [AggregateB]
3. [AggregateB] raises [EventY]
4. [External notification] triggered by [EventY]

**Consistency Requirements**:
- [Operation A and B] must be atomic (same DB transaction)
- [Operation C and D] can be eventually consistent (outbox pattern)

Provide:
1. Domain event record definitions
2. Event handler classes (INotificationHandler<T>)
3. Outbox pattern setup if needed
4. Integration event for cross-service communication
5. Saga class if long-running process involved
```

### CQRS Query Design
```
Design an optimized CQRS query for:

**Query Name**: [Name]
**Business Need**: [What data is needed and why]
**Consumer**: [API endpoint / UI page / Report]

**Data Required**:
- Fields: [list of fields needed]
- Filters: [optional filters]
- Sorting: [sort options]
- Pagination: [page size, cursor vs offset]

**Performance Requirements**:
- Response time target: [ms]
- Data volume: [rows expected]
- Call frequency: [times/minute]

**Allowed Data Sources**:
- [ ] Main write database (EF Core read model)
- [ ] Read-optimized view or materialized view
- [ ] Redis cache
- [ ] Separate read database (CQRS read side)

Provide:
1. Query record class
2. Query handler with optimized data access
3. DTO class
4. Cache strategy (if applicable)
5. Index recommendation for the query
```

### Saga / Process Manager
```
Design a Saga for this long-running business process:

**Process**: [Name]
**Steps**:
1. [Step 1] — can fail: [failure mode]
2. [Step 2] — can fail: [failure mode]
3. [Step 3] — can fail: [failure mode]

**Compensations** (rollback steps):
- If Step 2 fails: [undo step 1]
- If Step 3 fails: [undo step 1 and 2]

**Timeout**: [Max allowed duration]

Provide:
1. Saga state machine class
2. Command classes for each step
3. Compensation command classes
4. Saga persistence strategy (DB state)
5. Timeout handling
```

---

## DDD Quick Reference

| Pattern | When to Use | C# Implementation |
|---------|------------|-------------------|
| Value Object | Equality by value, immutable | `record` with validation |
| Entity | Identity-based equality, mutable | `class` with private setters |
| Aggregate Root | Consistency boundary owner | `class` inheriting `Entity` |
| Domain Event | State change notification | `record` implementing `IDomainEvent` |
| Domain Service | Operation spanning multiple aggregates | `class` with no state |
| Repository | Persistence abstraction | `interface IXRepo` in Domain |
| Specification | Complex query encapsulation | `class` implementing `ISpecification<T>` |
