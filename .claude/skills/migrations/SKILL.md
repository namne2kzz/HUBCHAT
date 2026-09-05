---
name: migrations
description: Use when creating, reviewing, or planning the rollout/rollback strategy for an EF Core migration on SQL Server or PostgreSQL — safety rules for reversibility and multi-phase schema changes.
---

# Skill: EF Core Migrations

Safe migration strategy for EF Core with SQL Server and PostgreSQL.

## Usage
```
/migrations [create|review|rollback|strategy] [context]
```

---

## Migration Safety Rules

1. **Always implement `Down()`** — every migration must be reversible
2. **Never drop columns in one step** — use multi-phase deprecation
3. **Add nullable first, then enforce NOT NULL** — zero-downtime column addition
4. **Create indexes CONCURRENTLY** (PostgreSQL) or with `ONLINE = ON` (SQL Server)
5. **Test migration on a copy of production data** before applying
6. **One migration per PR** — small, focused changes

---

## Prompt Templates

### Create Migration
```
Design an EF Core migration for this schema change:

**Change Type**: [Add column | Drop column | Add table | Rename | Add index | Add FK | Alter type]
**Entity/Table**: [EntityName / table_name]
**Database**: [SQL Server | PostgreSQL | Both]

**Current State**:
[Describe current schema or paste entity class]

**Target State**:
[Describe what it should look like after migration]

**Data Migration Needed**: [Yes/No]
If yes: [Describe how existing data should be transformed]

**Zero-downtime Required**: [Yes/No]
If yes: [Multi-phase migration needed — explain constraints]

**Risks**:
- Table size: [rows]
- Locking concern: [describe]

Provide:
1. EF Core entity change (C# code)
2. Migration Up() and Down() code
3. If data migration: SQL data transformation script
4. Index creation strategy (online/concurrent)
5. Rollback procedure
6. Testing checklist
```

### Review Migration
```
Review this EF Core migration for safety:

**Migration Code**:
[Paste Up() and Down() methods]

**Database**: [SQL Server | PostgreSQL]
**Table row count**: [estimate]
**Production traffic during migration**: [Yes/No]

Check for:
1. Missing Down() implementation
2. Destructive operations without safety (drop column with data)
3. NOT NULL column added without default (will fail on existing rows)
4. Missing index for new FK column
5. Long-running lock risk on large tables
6. Data type change that could lose data
7. Rename without backward-compatible alias period

Provide:
- Risk assessment (Low/Medium/High/Critical)
- Issues found
- Safe migration alternative
```

### Zero-Downtime Migration Strategy
```
Design zero-downtime migration for:

**Change**: [Describe the schema change needed]
**Constraint**: Cannot take downtime, rolling deployment

Provide multi-phase strategy:
- Phase 1: What to deploy first (backward compatible)
- Phase 2: Data migration (if needed)
- Phase 3: Cleanup (remove old structure)
- Code changes required at each phase
- Feature flag strategy if needed
```

---

## Migration Templates

### Add Nullable Column (Safe)
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Step 1: Add as nullable
    migrationBuilder.AddColumn<string>(
        name: "ExternalRef",
        table: "Orders",
        type: "nvarchar(100)",
        nullable: true);  // nullable first!
}

// Separate migration later to enforce NOT NULL after backfill
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Step 2: Backfill data
    migrationBuilder.Sql("UPDATE Orders SET ExternalRef = 'LEGACY-' + CAST(Id AS varchar(36)) WHERE ExternalRef IS NULL");

    // Step 3: Alter to NOT NULL
    migrationBuilder.AlterColumn<string>(
        name: "ExternalRef",
        table: "Orders",
        type: "nvarchar(100)",
        nullable: false,
        oldClrType: typeof(string), oldType: "nvarchar(100)", oldNullable: true);
}
```

### Add Index (Online — SQL Server)
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("""
        CREATE INDEX IX_Orders_CustomerId_Status
        ON Orders (CustomerId, Status)
        INCLUDE (TotalAmount, CreatedAt)
        WITH (ONLINE = ON);  -- non-blocking on SQL Server
        """);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropIndex("IX_Orders_CustomerId_Status", "Orders");
}
```

### Add Index (CONCURRENTLY — PostgreSQL)
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Note: CONCURRENTLY cannot run inside a transaction
    // Use migrationBuilder.Sql and set SuppressTransaction = true
    migrationBuilder.Sql("""
        CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_orders_customerid_status
        ON orders (customer_id, status)
        INCLUDE (total_amount, created_at);
        """, suppressTransaction: true);
}
```

## Migration Commands Reference
```bash
# Add migration
dotnet ef migrations add MigrationName --project Infrastructure --startup-project WebApi

# Apply to dev DB
dotnet ef database update --project Infrastructure --startup-project WebApi

# Generate SQL script (for production)
dotnet ef migrations script PreviousMigration NewMigration --output migration.sql

# Rollback one migration
dotnet ef database update PreviousMigrationName

# Remove last migration (if not applied)
dotnet ef migrations remove
```
