---
name: query-optimization
description: Use when diagnosing and fixing a slow query across SQL Server, PostgreSQL, or EF Core LINQ — execution plan analysis, index recommendations, N+1 detection.
---

# Skill: Query Optimization

Optimize slow queries across SQL Server, PostgreSQL, and EF Core LINQ.

## Usage
```
/query-optimization [analyze|fix|index|plan] [context]
```

---

## Prompt Template

```
Optimize this slow query:

**Database**: [SQL Server | PostgreSQL]
**ORM**: [EF Core LINQ | Raw SQL | Dapper]

**Current Code**:
[Paste LINQ or SQL]

**Performance Metrics**:
- Duration: [ms or seconds]
- Rows returned: [number]
- Rows scanned (from execution plan): [number]
- CPU time: [ms]
- Execution frequency: [times/minute]

**Execution Plan** (paste if available):
- SQL Server: FROM SSMS > Query > Include Actual Execution Plan
- PostgreSQL: EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT) [query];
[Paste output]

**Table Statistics**:
- Table: [name], rows: [count]
- Relevant columns: [list with data types]
- Existing indexes: [list]

**Target**: Response time < [ms]

Analyze:
1. Index usage (seeks vs scans)
2. Join strategy (nested loops vs hash vs merge)
3. Sort operations (can they be eliminated with index?)
4. Key lookup (add included columns to avoid it)
5. Implicit conversions (type mismatch killing index use)
6. Parameter sniffing (SQL Server) / plan instability (PostgreSQL)

Provide:
- Root cause analysis
- Optimized query/LINQ
- Index recommendations with T-SQL/DDL
- Expected improvement
```

---

## Common Anti-Patterns & Fixes

### N+1 Query Problem
```csharp
// ❌ SLOW — N+1
var orders = await db.Orders.ToListAsync();
foreach (var order in orders)
{
    var customer = await db.Customers.FindAsync(order.CustomerId);  // N extra queries!
    // process
}

// ✅ FAST — Single query with projection
var results = await db.Orders
    .Select(o => new {
        OrderId = o.Id,
        CustomerName = o.Customer.FullName,
        TotalAmount = o.TotalAmount
    })
    .ToListAsync();
```

### Loading Too Much Data
```csharp
// ❌ SLOW — loads all columns of all related entities
var orders = await db.Orders
    .Include(o => o.Customer)
    .Include(o => o.Items)
    .ThenInclude(i => i.Product)
    .ToListAsync();

// ✅ FAST — project only needed fields
var orders = await db.Orders
    .Select(o => new OrderListDto(
        o.Id,
        o.Customer.FullName,
        o.Items.Sum(i => i.Price * i.Quantity),
        o.Items.Count))
    .ToListAsync();
```

### Inefficient Pagination
```csharp
// ❌ SLOW on large tables — offset pagination scans all rows
var page = await db.Orders
    .Skip(10000 * pageSize)  // reads 10000 pages before returning data!
    .Take(pageSize)
    .ToListAsync();

// ✅ FAST — keyset pagination (cursor-based)
var page = await db.Orders
    .Where(o => o.Id > lastSeenId)  // or use CreatedAt with composite key
    .OrderBy(o => o.Id)
    .Take(pageSize)
    .ToListAsync();
```

### Non-SARGable WHERE Clause
```sql
-- ❌ SLOW — function on column prevents index seek
SELECT * FROM Orders WHERE YEAR(CreatedAt) = 2024;

-- ✅ FAST — range that allows index seek
SELECT * FROM Orders WHERE CreatedAt >= '2024-01-01' AND CreatedAt < '2025-01-01';
```

```csharp
// ❌ SLOW — EF Core version of the same problem
db.Orders.Where(o => o.CreatedAt.Year == 2024)

// ✅ FAST — range comparison
var start = new DateTime(2024, 1, 1);
var end = new DateTime(2025, 1, 1);
db.Orders.Where(o => o.CreatedAt >= start && o.CreatedAt < end)
```

### Missing Index for FK
```sql
-- After adding FK, always add index
ALTER TABLE OrderItems ADD CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES Orders(Id);

-- ⚠️ SQL Server does NOT auto-create index on FK — must add manually!
CREATE INDEX IX_OrderItems_OrderId ON OrderItems (OrderId);
```

---

## Reading Execution Plans

### SQL Server — Key Metrics
| Metric | Good | Investigate |
|--------|------|-------------|
| Seek vs Scan | Index Seek | Table Scan / Clustered Index Scan |
| Estimated vs Actual rows | Close | Large discrepancy → stale stats |
| Key Lookup | None | Present → add INCLUDE columns |
| Sort | None or index sort | Explicit sort on large set |
| Parallelism | None or intended | Unintended on OLTP queries |

### PostgreSQL — EXPLAIN ANALYZE
```sql
EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT)
SELECT o.id, c.full_name, o.total_amount
FROM orders o
JOIN customers c ON c.id = o.customer_id
WHERE o.status = 'Active'
ORDER BY o.created_at DESC
LIMIT 20;
```
Key nodes to look for:
- `Seq Scan` on large table → needs index
- `Hash Join` → check if index join would be faster
- `Sort` → can it be eliminated with index?
- `Rows Removed by Filter` >> actual rows → bad selectivity estimate
