using HUB.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
using Xunit;

namespace HUB.Notification.IntegrationTests;

/// <summary>One PostgreSQL container for the run; each test gets its own database on it.</summary>
/// <remarks>
/// Real PostgreSQL is required: MassTransit's EF inbox/outbox takes PostgreSQL row locks, and the unique
/// index + 23505 translation do not exist on SQLite. A database per test keeps inbox rows and
/// notifications from one test out of another's assertions.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("hub_notif")
        .Build();

    /// <summary>Starts the container.</summary>
    /// <returns>A task that completes when PostgreSQL accepts connections.</returns>
    public Task InitializeAsync() => _container.StartAsync();

    /// <summary>Stops and removes the container.</summary>
    /// <returns>A task that completes when the container is gone.</returns>
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates a fresh database, optionally migrated only up to <paramref name="targetMigration"/>.</summary>
    /// <param name="targetMigration">Migration to stop at; null = latest.</param>
    /// <returns>Connection string of the new database.</returns>
    public async Task<string> CreateDatabaseAsync(string? targetMigration = null)
    {
        var name = $"notif_{Guid.NewGuid():N}";
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name };

        await using var context = CreateContext(builder.ConnectionString);
        // Migrate (not EnsureCreated): the migration chain itself is part of what these tests prove.
        await context.GetService<IMigrator>().MigrateAsync(targetMigration);
        return builder.ConnectionString;
    }

    /// <summary>Creates a context over a connection string.</summary>
    /// <param name="connectionString">Target database.</param>
    /// <returns>A context the caller disposes.</returns>
    public static NotificationDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(connectionString).Options);
}
