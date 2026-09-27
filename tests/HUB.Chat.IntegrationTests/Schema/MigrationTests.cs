using HUB.Chat.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;
using Xunit;

namespace HUB.Chat.IntegrationTests.Schema;

/// <summary>
/// Proves the migration chain runs forward on an empty PostgreSQL database and leaves a schema that
/// matches the model.
/// </summary>
/// <remarks>
/// This is what a fresh environment and a CI run both depend on, and it is not covered anywhere else:
/// the unit tests build their schema with <c>EnsureCreated</c> from the current model, which skips the
/// migrations entirely. A model that compiles says nothing about whether the migrations that are
/// supposed to produce it still run.
///
/// The checks here are shaped by what actually went wrong in the sibling DASHBOARD repository, where
/// the equivalent tests found two serious faults on their first run: four migration files had been
/// dropped from the repository during a folder move, and one migration had been committed with an
/// empty <c>Up()</c>. Both were invisible on developer machines, whose databases had already run the
/// originals, and both would have surfaced as a broken schema on the next fresh deploy. HUB's
/// migrations are clean of all three patterns today — these tests are here to keep it that way.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
[Trait(TestCategories.Category, TestCategories.RequiresDocker)]
public sealed class MigrationTests(PostgresFixture database)
{
    [Fact]
    public async Task TheSharedDatabaseIsFullyMigrated()
    {
        // The fixture migrated it on start-up; this asserts that finished cleanly rather than leaving
        // the rest of the suite to fail in less obvious ways.
        await using var context = database.CreateContext();

        (await context.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        (await context.Database.GetAppliedMigrationsAsync()).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AFreshDatabaseReachesTheLatestMigration()
    {
        // From genuinely nothing, which is what a new environment does and what no developer machine
        // ever repeats after the first time.
        var name = ScratchName();
        await using var context = PostgresFixture.CreateContext(database.ScratchConnectionString(name));

        try
        {
            await context.Database.MigrateAsync();

            (await context.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task NoMigrationHasAnEmptyUpBody()
    {
        // An AddOrganizations migration in the sibling DASHBOARD repository was committed with an empty
        // Up(). Every machine that had already run the original looked healthy, while a fresh database
        // came out missing the table it was supposed to create — and nothing failed until a query
        // touched it at runtime. An empty Up() is occasionally legitimate, but it should never pass
        // silently.
        await using var context = database.CreateContext();
        var assembly = context.Database.GetService<IMigrationsAssembly>();

        var empty = assembly.Migrations
            .Where(pair => ((Migration)Activator.CreateInstance(pair.Value.AsType())!).UpOperations.Count == 0)
            .Select(pair => pair.Key)
            .ToList();

        empty.ShouldBeEmpty(
            "a migration whose Up() does nothing leaves a fresh database missing whatever it should have created");
    }

    [Fact]
    public async Task EveryMigrationInTheAssemblyHasBeenApplied()
    {
        // Guards the reverse of a missing file: a migration present in the assembly but never applied
        // means the chain stopped early without the migrator reporting it.
        await using var context = database.CreateContext();

        var known   = context.Database.GetService<IMigrationsAssembly>().Migrations.Keys;
        var applied = await context.Database.GetAppliedMigrationsAsync();

        applied.ShouldBe(known, ignoreOrder: true);
    }

    [Fact]
    public async Task TheMigratedSchemaCarriesEveryMappedTableAndColumn()
    {
        // Re-applying without error is not the same as arriving at the right schema. Selecting from every
        // mapped table names each mapped column, so a migration that created a table without one of them
        // fails here rather than at the first request that needs it.
        await using var context = database.CreateContext();

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

        failures.ShouldBeEmpty("the migrated schema must carry every table and column the model maps");
    }

    [Fact]
    public async Task TheModelHasNoChangesLeftUnmigrated()
    {
        // The drift check: an entity configuration edited without scaffolding a migration builds and
        // passes every unit test, then fails on deploy. EF compares the model against the snapshot the
        // last migration recorded.
        await using var context = database.CreateContext();

        var differ   = context.Database.GetService<IMigrationsModelDiffer>();
        var snapshot = context.Database.GetService<IMigrationsAssembly>().ModelSnapshot;

        snapshot.ShouldNotBeNull("the migrations assembly must contain a model snapshot");

        // The snapshot's model is not finalized as it comes off the assembly, and GetRelationalModel
        // refuses to run on one that is not. Running it through the initializer is what EF's own tooling
        // does before diffing.
        var initializer  = context.Database.GetService<IModelRuntimeInitializer>();
        var snapshotModel = initializer.Initialize(((IMutableModel)snapshot!.Model).FinalizeModel());

        var designTimeModel = context.Database.GetService<IDesignTimeModel>().Model;

        var differences = differ.GetDifferences(
            snapshotModel.GetRelationalModel(),
            designTimeModel.GetRelationalModel());

        differences.ShouldBeEmpty(
            "the entity configurations have changed since the last migration was scaffolded — run dotnet ef migrations add");
    }

    /// <summary>A database name unique to one test run, short enough for PostgreSQL's 63-byte limit.</summary>
    private static string ScratchName() => $"hub_chat_fresh_{Guid.NewGuid():N}"[..30];
}
