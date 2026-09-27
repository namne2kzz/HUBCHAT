using Microsoft.EntityFrameworkCore;

namespace HUB.Chat.IntegrationTests.Infrastructure;

/// <summary>Reads a table's shape straight from the database, bypassing EF's model.</summary>
/// <remarks>
/// The schema tests need to ask the database what it actually has, which means naming a table in SQL.
/// Table names here come from <c>IEntityType.GetTableName()</c> — EF's own metadata, not user input — so
/// there is no untrusted value to parameterise. A raw command keeps that explicit and avoids EF1002 on
/// an interpolated <c>ExecuteSqlRawAsync</c>, which would otherwise have to be suppressed at each call.
/// </remarks>
public static class SchemaProbe
{
    /// <summary>Selects one row from a table, so a missing table or column surfaces as an exception.</summary>
    /// <param name="context">Context pointed at the database to probe.</param>
    /// <param name="table">Table name, from EF metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task ProbeTableAsync(DbContext context, string table, CancellationToken ct = default)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();

        // Quoted to match the snake_case names the configurations declare; PostgreSQL would otherwise
        // fold them to lower case and still match, but an explicitly quoted identifier cannot be
        // mistaken for a keyword.
        command.CommandText = $"""SELECT * FROM "{table}" LIMIT 1""";

        await context.Database.OpenConnectionAsync(ct);
        try
        {
            await command.ExecuteScalarAsync(ct);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
