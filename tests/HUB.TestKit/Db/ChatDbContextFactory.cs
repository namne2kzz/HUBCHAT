using System.Data.Common;
using HUB.Chat.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HUB.TestKit.Db;

/// <summary>
/// Creates a real <see cref="ChatDbContext"/> backed by a private SQLite database, for handler tests
/// that need query behaviour without a container.
/// </summary>
/// <remarks>
/// The production mapping is written for PostgreSQL, and one property does not survive the move:
/// <c>MessageConfiguration</c> declares the <c>mentions</c> column as <c>jsonb</c>, which SQLite does
/// not know. <see cref="Create"/> therefore rewrites that single column type to <c>TEXT</c> on the
/// SQLite model. Nothing else is altered — the entity configurations, the client-assigned-key fix in
/// <c>OnModelCreating</c>, and the query filters all run exactly as they do in production.
///
/// What that rewrite costs: a test here cannot prove anything about the jsonb column itself, and
/// SQLite is looser than PostgreSQL about types and some translations besides. Anything that turns
/// on the real column type, on a real index, or on Npgsql's LINQ translation belongs in
/// HUB.Chat.IntegrationTests, which runs the genuine migrations against a PostgreSQL container.
/// Treat this factory as a fast way to exercise handler logic, not as evidence about the schema.
/// </remarks>
public static class ChatDbContextFactory
{
    /// <summary>Creates an isolated context with the schema already built.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ChatDbContextLease"/> owning the context and its connection; dispose it to release both.</returns>
    public static async Task<ChatDbContextLease> CreateAsync(CancellationToken ct = default)
    {
        // A shareable in-memory database lives exactly as long as this connection does, which is why
        // the lease holds it open rather than letting the context close it between calls.
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(ct);

        var context = Create(connection);
        await context.Database.EnsureCreatedAsync(ct);

        return new ChatDbContextLease(context, connection);
    }

    /// <summary>Creates a context over an already-open connection, without touching the schema.</summary>
    /// <param name="connection">An open SQLite connection.</param>
    /// <returns>A new context; the caller owns it.</returns>
    /// <remarks>
    /// Use this to get a second context over the same database — the way to prove a handler actually
    /// persisted something rather than merely leaving it in one change tracker.
    /// </remarks>
    public static ChatDbContext Create(DbConnection connection)
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseSqlite(connection)
            .ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCustomizer, SqliteTypeRewriter>()
            .Options;

        return new ChatDbContext(options);
    }
}
