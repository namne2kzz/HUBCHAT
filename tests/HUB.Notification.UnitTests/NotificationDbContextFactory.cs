using HUB.Notification.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HUB.Notification.UnitTests;

/// <summary>Creates an isolated <see cref="NotificationDbContext"/> on a private SQLite database.</summary>
/// <remarks>
/// The real context is used unchanged here. Unlike the Chat service, nothing in this bounded context's
/// mapping is PostgreSQL-specific, so no column types need rewriting — the entity configurations run
/// exactly as they do in production.
///
/// The connection is held open by the returned lease because a SQLite in-memory database lives only as
/// long as a connection to it.
/// </remarks>
public static class NotificationDbContextFactory
{
    /// <summary>Creates a context with the schema already built.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A lease owning the context and its connection.</returns>
    public static async Task<Lease> CreateAsync(CancellationToken ct = default)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(ct);

        var context = Create(connection);
        await context.Database.EnsureCreatedAsync(ct);

        return new Lease(context, connection);
    }

    private static NotificationDbContext Create(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<NotificationDbContext>().UseSqlite(connection).Options);

    /// <summary>Owns a test context and the connection its database lives on.</summary>
    /// <param name="context">The context under test.</param>
    /// <param name="connection">The open connection.</param>
    public sealed class Lease(NotificationDbContext context, SqliteConnection connection) : IAsyncDisposable
    {
        /// <summary>The context under test.</summary>
        public NotificationDbContext Context { get; } = context;

        /// <summary>Opens a second context over the same database, for asserting what was persisted.</summary>
        /// <returns>A new context the caller must dispose.</returns>
        public NotificationDbContext NewContext() => NotificationDbContextFactory.Create(connection);

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
