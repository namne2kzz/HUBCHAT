using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace HUB.TestKit.Db;

/// <summary>
/// Model customizer that retypes PostgreSQL-only column types so the production mapping can also be
/// built on SQLite.
/// </summary>
/// <remarks>
/// <c>MessageConfiguration</c> declares the <c>mentions</c> column as <c>jsonb</c>, a type SQLite does
/// not have, so building the real model against SQLite fails outright. <see cref="ChatDbContext"/> is
/// sealed, so this runs through EF's own <see cref="IModelCustomizer"/> hook instead of a subclass —
/// production code stays untouched, which is the point: a test project should not be the reason a
/// class gets unsealed.
///
/// The rewrite is deliberately narrow, and it is also the boundary of what SQLite-backed tests may
/// claim. With the column retyped, nothing here proves anything about the real <c>jsonb</c> column,
/// and SQLite differs from PostgreSQL on several translations besides. Schema, index and translation
/// questions belong in HUB.Chat.IntegrationTests, which applies the genuine migrations to a
/// PostgreSQL container.
/// </remarks>
/// <param name="dependencies">Dependencies EF supplies to the base customizer.</param>
public sealed class SqliteTypeRewriter(ModelCustomizerDependencies dependencies)
    : RelationalModelCustomizer(dependencies)
{
    /// <inheritdoc />
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        // Scan rather than name the one property, so a jsonb column added later is handled too
        // instead of failing with an error that points at SQLite rather than at the new mapping.
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(entityType => entityType.GetProperties()))
        {
            if (string.Equals(property.GetColumnType(), "jsonb", StringComparison.OrdinalIgnoreCase))
                property.SetColumnType("TEXT");
        }
    }
}
