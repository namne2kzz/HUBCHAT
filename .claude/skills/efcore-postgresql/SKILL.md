---
name: efcore-postgresql
description: Use when writing or optimizing EF Core queries/configuration against PostgreSQL (Npgsql) — jsonb mapping, indexing, query translation specifics.
---

# Skill: EF Core + PostgreSQL

Patterns, optimization, and best practices for EF Core with PostgreSQL (Npgsql).

## Usage
```
/efcore-postgresql [query|config|jsonb|optimize] [context]
```

---

## Prompt Templates

### Query Optimization (PostgreSQL)
```
Optimize this EF Core + PostgreSQL query:

**Current LINQ**:
[Paste EF Core query]

**EXPLAIN ANALYZE output** (optional):
[Paste from pgAdmin or psql]

**Performance Metrics**:
- Current duration: [ms]
- Row count: [estimate]
- Frequency: [calls/min]

**Schema**:
[Table columns, types, existing indexes]

Check for:
1. Missing AsNoTracking() on reads
2. Inefficient JSONB queries (use jsonb operators, not JSON extract chains)
3. Missing GIN indexes for full-text or JSONB search
4. NOT LIKE queries (prefer pg_trgm with GIN index)
5. Unnecessary ORDER BY on large result sets
6. Timestamp with time zone vs without issues

Provide:
- Optimized LINQ
- PostgreSQL-specific index recommendations (GIN, BRIN, partial)
- EXPLAIN ANALYZE analysis if provided
```

### JSONB Column
```
Design EF Core configuration for a JSONB column:

**Entity**: [EntityName]
**JSONB property**: [PropertyName]
**Type**: [C# type to store as JSONB]

**Query patterns on this JSONB**:
1. Filter by [jsonb field]: [condition]
2. Search in [jsonb array]

Provide:
1. EF Core entity configuration with HasColumnType("jsonb")
2. Query examples using Npgsql JSONB operators
3. GIN index creation
4. Serialization/deserialization setup
```

### Full-Text Search
```
Implement PostgreSQL full-text search with EF Core:

**Table/Entity**: [Name]
**Columns to search**: [list columns]
**Search language**: [english/vietnamese/other]

**Requirements**:
- Fuzzy/partial match: [Yes/No]
- Ranking by relevance: [Yes/No]
- Combined with other filters: [describe]

Provide:
1. tsvector column configuration
2. GIN index on tsvector
3. EF Core configuration using NpgsqlTsVector
4. Query using EF Core full-text search methods
5. Migration SQL
```

---

## Configuration Patterns

### Npgsql Setup
```csharp
// Registration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres"),
        npgsql => npgsql
            .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), null)
            .CommandTimeout(30)
            .MigrationsHistoryTable("__EFMigrationsHistory", "app")
    )
);

// Program.cs — NodaTime or UTC enforcement
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
```

### Entity Configuration (PostgreSQL-specific)
```csharp
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", "app");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");

        // JSONB column for metadata
        builder.Property(p => p.Metadata)
            .HasColumnType("jsonb")
            .HasColumnName("metadata");

        // Full-text search vector
        builder.Property(p => p.SearchVector)
            .HasColumnType("tsvector")
            .HasComputedColumnSql("to_tsvector('english', name || ' ' || description)", stored: true);

        // Indexes
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.SearchVector).HasMethod("GIN");
        builder.HasIndex(p => p.Metadata).HasMethod("GIN");
    }
}
```

### JSONB Queries
```csharp
// Filter by JSONB field (Npgsql EF Core)
var products = await db.Products
    .Where(p => EF.Functions.JsonContains(p.Metadata, """{"featured": true}"""))
    .ToListAsync();

// JSONB path query
var results = await db.Products
    .Where(p => EF.Functions.JsonExists(p.Metadata, "$.tags[*]"))
    .ToListAsync();
```

### Full-Text Search
```csharp
// Search using EF Core Npgsql
var results = await db.Products
    .Where(p => p.SearchVector.Matches("laptop & gaming"))
    .OrderByDescending(p => p.SearchVector.Rank(EF.Functions.WebSearchToTsQuery("laptop gaming")))
    .Take(20)
    .Select(p => new { p.Id, p.Name })
    .ToListAsync();
```

## PostgreSQL vs SQL Server EF Core Differences

| Feature | SQL Server | PostgreSQL |
|---------|-----------|------------|
| UUID default | `NEWSEQUENTIALID()` | `gen_random_uuid()` |
| JSON storage | `nvarchar` or `JSON` type | `jsonb` (indexed, binary) |
| Full-text | `CONTAINS()` / FTS index | `tsvector` + GIN index |
| Auto-increment | `IDENTITY(1,1)` | `SERIAL` / `GENERATED ALWAYS AS IDENTITY` |
| Pagination | `OFFSET FETCH` | `OFFSET LIMIT` (same syntax) |
| Case insensitive | `COLLATE SQL_Latin1_General_CP1_CI_AS` | `ILIKE` or `citext` extension |
