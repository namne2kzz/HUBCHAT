using HUB.Chat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace HUB.Chat.IntegrationTests.Infrastructure;

/// <summary>
/// Starts one PostgreSQL container for the whole integration-test run.
/// </summary>
/// <remarks>
/// A real PostgreSQL is the entire point of this project. The unit tests run the same
/// <see cref="ChatDbContext"/> on SQLite, which cannot honour the things production actually depends
/// on: the <c>jsonb</c> column on <c>messages.mentions</c> (rewritten to <c>TEXT</c> there), the real
/// index definitions, and Npgsql's own LINQ translation. Anything that turns on those belongs here.
///
/// Starting a container costs seconds, so it is shared across every test class in
/// <see cref="IntegrationTestCollection"/>. Classes that migrate a database up and down cannot share
/// one — they would tear the schema out from under their neighbours — so they ask for
/// <see cref="CreateScratchDatabaseAsync"/> and get their own database on the same server.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        // Pinned to the major version the repository targets; "latest" would make a schema test start
        // failing because of an image update rather than because of a change in this codebase.
        .WithImage("postgres:17-alpine")
        .WithDatabase("hub_chat")
        .Build();

    /// <summary>Connection string for the container's default database.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Starts the container and brings the default database up to the latest migration.</summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Migrate rather than EnsureCreated. EnsureCreated builds the schema from the current model and
        // would skip the migration chain entirely — which is the one thing most worth proving, since a
        // model that builds cleanly says nothing about whether the migrations that produce it still run.
        await using var context = CreateContext(ConnectionString);
        await context.Database.MigrateAsync();
    }

    /// <summary>Stops and removes the container.</summary>
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates a context over a given connection string.</summary>
    /// <param name="connectionString">Target database.</param>
    /// <returns>A context the caller owns and must dispose.</returns>
    public static ChatDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ChatDbContext(options);
    }

    /// <summary>Creates a context over the shared, already-migrated database.</summary>
    /// <returns>A context the caller owns and must dispose.</returns>
    public ChatDbContext CreateContext() => CreateContext(ConnectionString);

    /// <summary>
    /// Returns a connection string for a fresh, empty database on the same server.
    /// </summary>
    /// <param name="name">A name unique to the calling test.</param>
    /// <returns>A connection string pointing at a database with no schema yet.</returns>
    /// <remarks>
    /// For tests that need to own a schema outright — migrating from empty, or rolling back. The
    /// database itself is not created here; EF's migrator creates it on first use, and the test drops it
    /// when it finishes.
    /// </remarks>
    public string ScratchConnectionString(string name) =>
        new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
}

/// <summary>Shares one <see cref="PostgresFixture"/> across every integration-test class.</summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgresFixture>
{
    /// <summary>The collection name test classes reference.</summary>
    public const string Name = "chat-integration";
}
