using System.Data.Common;
using HUB.Chat.Infrastructure.Persistence;

namespace HUB.TestKit.Db;

/// <summary>Owns a test <see cref="ChatDbContext"/> and the connection its in-memory database lives on.</summary>
/// <remarks>
/// A SQLite in-memory database exists only while a connection to it is open, so the connection has to
/// outlive any single context. Disposing the lease closes it, which is what discards the database.
/// </remarks>
/// <param name="context">The context tests work through.</param>
/// <param name="connection">The open connection backing the database.</param>
public sealed class ChatDbContextLease(ChatDbContext context, DbConnection connection) : IAsyncDisposable
{
    /// <summary>The context under test.</summary>
    public ChatDbContext Context { get; } = context;

    /// <summary>
    /// Opens a second context over the same database.
    /// </summary>
    /// <returns>A new context the caller must dispose.</returns>
    /// <remarks>
    /// Assert through one of these rather than through <see cref="Context"/> when a test needs to show
    /// that a handler really persisted something: the original context's change tracker would return
    /// the entity from memory whether <c>SaveChangesAsync</c> ran or not.
    /// </remarks>
    public ChatDbContext NewContext() => ChatDbContextFactory.Create(connection);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }
}
