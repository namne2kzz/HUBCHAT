using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using HUB.Chat.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HUB.Chat.IntegrationTests.Schema;

/// <summary>
/// Checks that the message keyset seek still reaches its index.
/// </summary>
/// <remarks>
/// The Id tie-breaker (BUG-003) turns the seek predicate into <c>a &lt; x OR (a = x AND b &lt; y)</c>, and an
/// OR can stop a planner using an index. That matters because keyset paging exists to stay O(page) as a
/// channel's history grows: a plan that degraded to a sequential scan would still return the right rows,
/// so no functional test would notice — it would surface months later as one slow channel.
///
/// PostgreSQL currently splits the OR into two bitmap index scans over
/// <c>IX_messages_ChannelId_CreatedAt</c>. This test fails if a future change to the predicate, the index
/// or the planner's costing loses that.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
[Trait(TestCategories.Category, TestCategories.RequiresDocker)]
public sealed class KeysetQueryPlanTests(PostgresFixture database)
{
    [Fact]
    public async Task TheKeysetSeekUsesTheChannelCreatedAtIndex()
    {
        await using var context = database.CreateContext();

        var channel = Channel.Create(Guid.NewGuid(), "Plan", ChannelType.Public, Guid.NewGuid());
        context.Channels.Add(channel);

        // Enough rows that an index is worth choosing; on a tiny table the planner picks a sequential
        // scan whatever the predicate looks like, and the assertion would prove nothing.
        for (var i = 0; i < 2000; i++)
            context.Messages.Add(Message.Post(channel.Id, channel.CreatedBy, $"m{i}", MessageFormat.Plain));

        await context.SaveChangesAsync(CancellationToken.None);
        await context.Database.ExecuteSqlRawAsync("ANALYZE messages");

        var boundary = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        var plan = await ExplainAsync(context, $"""
            EXPLAIN (FORMAT TEXT)
            SELECT * FROM messages
            WHERE "ChannelId" = '{channel.Id}'
              AND ("CreatedAt" < '{boundary:O}'
                   OR ("CreatedAt" = '{boundary:O}' AND "Id" < '{Guid.NewGuid()}'))
            ORDER BY "CreatedAt" DESC, "Id" DESC
            LIMIT 50
            """);

        plan.ShouldContain("IX_messages_ChannelId_CreatedAt",
            customMessage: $"the keyset seek must reach its index. Plan was:\n{plan}");

        plan.ShouldNotContain("Seq Scan on messages",
            customMessage: $"the keyset seek fell back to a sequential scan. Plan was:\n{plan}");
    }

    private static async Task<string> ExplainAsync(DbContext context, string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        await context.Database.OpenConnectionAsync();
        try
        {
            var lines = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) lines.Add(reader.GetString(0));
            return string.Join('\n', lines);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
