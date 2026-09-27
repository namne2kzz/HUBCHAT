using HUB.Chat.Infrastructure.Persistence;
using HUB.Chat.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;
using Xunit;

namespace HUB.Chat.IntegrationTests.Schema;

/// <summary>
/// Builds a throwaway database, then migrates it down and back up again.
/// </summary>
/// <remarks>
/// <c>Down()</c> is the only code in the repository that can be completely broken while everything else
/// stays green. The scaffolder writes it, nobody reads it, and it does not run until somebody is
/// reverting a bad release — which is the worst possible moment to discover it does not work.
///
/// The sibling DASHBOARD repository added exactly these tests and they failed immediately, on three
/// separate faults, all of them inside <c>Down()</c>: a <c>DeleteData</c> call EF could not translate
/// once the model no longer mapped the table, an <c>ALTER COLUMN</c> issued while a dependent index
/// still existed, and a scaffolder-generated <c>UpdateData</c> with an empty column list that emitted
/// <c>UPDATE ... SET</c> followed straight by <c>WHERE</c>. None was reachable by review. HUB's
/// migrations use none of those constructs today, so these tests currently pass — their value is that
/// the next migration to introduce one fails here instead of during an incident.
///
/// Each test owns its own database, because migrating down on the shared one would remove the schema
/// from underneath every other class in the collection. The database is dropped even when a test fails.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
[Trait(TestCategories.Category, TestCategories.RequiresDocker)]
public sealed class MigrationRollbackTests(PostgresFixture database) : IAsyncLifetime
{
    private string _connectionString = string.Empty;

    /// <summary>Points this class at a database name no other run will collide with.</summary>
    public Task InitializeAsync()
    {
        _connectionString = database.ScratchConnectionString($"hub_chat_rb_{Guid.NewGuid():N}"[..30]);
        return Task.CompletedTask;
    }

    /// <summary>Drops the throwaway database.</summary>
    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    private ChatDbContext CreateContext() => PostgresFixture.CreateContext(_connectionString);

    private static IReadOnlyList<string> AllMigrations(ChatDbContext context) =>
        [.. context.Database.GetService<IMigrationsAssembly>().Migrations.Keys];

    [Fact]
    public async Task TheChainSurvivesAFullDownAndUpCycle()
    {
        await using var context = CreateContext();
        var migrator = context.Database.GetService<IMigrator>();

        await migrator.MigrateAsync();
        (await context.Database.GetAppliedMigrationsAsync()).Count().ShouldBe(AllMigrations(context).Count);

        // "0" is EF's name for the empty database: every Down() in reverse order.
        await migrator.MigrateAsync("0");
        (await context.Database.GetAppliedMigrationsAsync())
            .ShouldBeEmpty("migrating to 0 must unwind the whole chain");

        // The assertion that matters. A Down() that left the schema subtly wrong — a dropped table whose
        // index was never recreated, a column quietly retyped — makes this second Up() fail where the
        // first one passed.
        await migrator.MigrateAsync();
        (await context.Database.GetPendingMigrationsAsync())
            .ShouldBeEmpty("the chain must be re-appliable after a full rollback");
    }

    [Fact]
    public async Task RollingBackOnlyTheLastMigrationAndReapplyingItWorks()
    {
        await using var context = CreateContext();
        var migrator = context.Database.GetService<IMigrator>();

        await migrator.MigrateAsync();

        var migrations = AllMigrations(context);
        if (migrations.Count < 2) return; // nothing to step back to yet

        var previous = migrations[^2];
        var last     = migrations[^1];

        // The realistic incident shape: a release ships, something is wrong, and only the newest
        // migration is reverted. A full down-and-up can hide a Down() that only breaks while the rest of
        // the schema is still in place.
        await migrator.MigrateAsync(previous);
        (await context.Database.GetAppliedMigrationsAsync()).ShouldNotContain(last);

        await migrator.MigrateAsync(last);
        (await context.Database.GetAppliedMigrationsAsync()).ShouldContain(last);
    }

    [Fact]
    public async Task AfterAFullCycleTheSchemaStillMatchesTheModel()
    {
        await using var context = CreateContext();
        var migrator = context.Database.GetService<IMigrator>();

        await migrator.MigrateAsync();
        await migrator.MigrateAsync("0");
        await migrator.MigrateAsync();

        // Re-applying without an error is not proof the schema came back the same. Selecting from every
        // mapped table names each mapped column, so a table rebuilt without one of them fails here.
        var failures = new List<string>();

        foreach (var entityType in context.Model.GetEntityTypes().Where(t => !t.IsOwned()))
        {
            var table = entityType.GetTableName();
            if (table is null) continue;

            try
            {
                await SchemaProbe.ProbeTableAsync(context, table);
            }
            catch (Exception ex)
            {
                failures.Add($"{entityType.ClrType.Name} ({table}): {ex.Message}");
            }
        }

        failures.ShouldBeEmpty("a rebuilt schema must still carry every table and column the model maps");
    }

    [Fact]
    public async Task DataSurvivesRollingBackAndReapplyingTheLastMigration()
    {
        await using var context = CreateContext();
        var migrator = context.Database.GetService<IMigrator>();

        await migrator.MigrateAsync();

        var migrations = AllMigrations(context);
        if (migrations.Count < 2) return;

        // A Down() that drops and recreates a table rather than reversing the specific change loses every
        // row in it. That is survivable in staging and not survivable in production, and the difference
        // does not show up in a schema-only assertion.
        var channel = HUB.Chat.Domain.Entities.Channel.Create(
            Guid.NewGuid(), "General", HUB.Chat.Domain.Enums.ChannelType.Public, Guid.NewGuid());
        context.Channels.Add(channel);
        await context.SaveChangesAsync(CancellationToken.None);

        await migrator.MigrateAsync(migrations[^2]);
        await migrator.MigrateAsync(migrations[^1]);

        await using var verify = CreateContext();
        (await verify.Channels.AsNoTracking().AnyAsync(c => c.Id == channel.Id))
            .ShouldBeTrue("rolling the last migration back and forward must not discard existing rows");
    }
}
