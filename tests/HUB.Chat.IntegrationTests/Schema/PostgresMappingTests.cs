using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using HUB.Chat.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HUB.Chat.IntegrationTests.Schema;

/// <summary>
/// Covers the parts of the mapping that only a real PostgreSQL can answer.
/// </summary>
/// <remarks>
/// The unit tests run the same context on SQLite, which required rewriting the <c>jsonb</c> column on
/// <c>messages.mentions</c> to <c>TEXT</c> to build at all. That rewrite is exactly why these tests
/// exist: with it in place, nothing in the unit suite can say whether the real column type, the real
/// indexes, or Npgsql's translation of a query over them still work.
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
[Trait(TestCategories.Category, TestCategories.RequiresDocker)]
public sealed class PostgresMappingTests(PostgresFixture database)
{
    [Fact]
    public async Task TheMentionsColumnIsJsonb()
    {
        // The column type is declared in MessageConfiguration and asserted here rather than there,
        // because a configuration can declare anything — this reads what the migration actually built.
        await using var context = database.CreateContext();

        var dataType = await ScalarAsync(context, """
            SELECT data_type FROM information_schema.columns
            WHERE table_name = 'messages' AND column_name = 'mentions'
            """);

        dataType.ShouldBe("jsonb");
    }

    [Fact]
    public async Task MentionsRoundTripThroughJsonb()
    {
        await using var context = database.CreateContext();

        var channel = Channel.Create(Guid.NewGuid(), "Mentions", ChannelType.Public, Guid.NewGuid());
        context.Channels.Add(channel);

        Guid[] mentioned = [Guid.NewGuid(), Guid.NewGuid()];
        var message = Message.Post(channel.Id, channel.CreatedBy, "hi both", MessageFormat.Markdown, mentions: mentioned);
        context.Messages.Add(message);
        await context.SaveChangesAsync(CancellationToken.None);

        await using var verify = database.CreateContext();
        var reloaded = await verify.Messages.AsNoTracking().SingleAsync(m => m.Id == message.Id);

        reloaded.Mentions.ShouldBe(mentioned);
    }

    [Fact]
    public async Task TheKeysetIndexOnChannelAndCreatedAtExists()
    {
        // The message list pages with a keyset seek and its comment names this index as what makes that
        // cheap. Without it the query still returns correct rows, so no functional test would notice —
        // it just degrades to a scan as a channel's history grows.
        await using var context = database.CreateContext();

        var indexes = await ListAsync(context, """
            SELECT indexdef FROM pg_indexes WHERE tablename = 'messages'
            """);

        indexes.ShouldContain(
            definition => definition.Contains("ChannelId", StringComparison.OrdinalIgnoreCase)
                       && definition.Contains("CreatedAt", StringComparison.OrdinalIgnoreCase),
            "the keyset page relies on a (ChannelId, CreatedAt) index");
    }

    [Fact]
    public async Task DeletingAMessageCascadesToItsReactions()
    {
        // Configured with OnDelete(Cascade) on the model; this checks the database enforces it, which is
        // what stops orphaned reaction rows accumulating.
        await using var context = database.CreateContext();

        var channel = Channel.Create(Guid.NewGuid(), "Cascade", ChannelType.Public, Guid.NewGuid());
        context.Channels.Add(channel);

        var message = Message.Post(channel.Id, channel.CreatedBy, "react to me", MessageFormat.Plain);
        message.AddReaction(channel.CreatedBy, ":+1:");
        context.Messages.Add(message);
        await context.SaveChangesAsync(CancellationToken.None);

        context.Messages.Remove(message);
        await context.SaveChangesAsync(CancellationToken.None);

        await using var verify = database.CreateContext();
        var orphans = await ScalarAsync(verify,
            $"""SELECT count(*) FROM reactions WHERE "MessageId" = '{message.Id}'""");

        orphans.ShouldBe("0");
    }

    private static async Task<string?> ScalarAsync(Microsoft.EntityFrameworkCore.DbContext context, string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        await context.Database.OpenConnectionAsync();
        try
        {
            return (await command.ExecuteScalarAsync())?.ToString();
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task<List<string>> ListAsync(Microsoft.EntityFrameworkCore.DbContext context, string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        await context.Database.OpenConnectionAsync();
        try
        {
            var results = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) results.Add(reader.GetString(0));
            return results;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
