using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Messages.Queries.SearchMessages;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using HUB.TestKit.Db;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Messages;

/// <summary>
/// Covers <c>SearchMessagesHandler</c> (CF-8): case-insensitive, full-history, read-access-gated search
/// within a channel.
/// </summary>
/// <remarks>
/// SQLite's LIKE is case-insensitive for ASCII (as PostgreSQL's LOWER+LIKE is here), so the case tests are
/// meaningful on the unit DB; wildcard escaping against the real collation is re-checked in integration.
/// </remarks>
public sealed class SearchMessagesHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static async Task<(ChatDbContextLease Lease, Channel Channel, Guid Member)> SeedAsync(
        IEnumerable<string> bodies, ChannelType type = ChannelType.Public)
    {
        var lease = await ChatDbContextFactory.CreateAsync();
        var member = Guid.NewGuid();
        var channel = Channel.Create(Guid.NewGuid(), "General", type, member);
        lease.Context.Channels.Add(channel);
        foreach (var b in bodies)
            lease.Context.Messages.Add(Message.Post(channel.Id, member, b, MessageFormat.Plain));
        await lease.Context.SaveChangesAsync(Ct);
        return (lease, channel, member);
    }

    [Fact]
    public async Task FindsMessagesContainingTheTerm_CaseInsensitive()
    {
        var (lease, channel, member) = await SeedAsync(["Deploy is DONE", "lunch?", "deploying now"]);
        await using var _ = lease;

        var results = await new SearchMessagesHandler(lease.Context)
            .Handle(new SearchMessagesQuery(channel.Id, "deploy", member), Ct);

        results.Select(m => m.Body).ShouldBe(["deploying now", "Deploy is DONE"], ignoreOrder: true);
    }

    [Fact]
    public async Task ABlankTermReturnsNothing()
    {
        var (lease, channel, member) = await SeedAsync(["anything"]);
        await using var _ = lease;

        (await new SearchMessagesHandler(lease.Context).Handle(new SearchMessagesQuery(channel.Id, "   ", member), Ct))
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task DeletedMessagesAreNotSearchable()
    {
        var (lease, channel, member) = await SeedAsync(["secret plan"]);
        await using var _ = lease;
        var msg = await System.Threading.Tasks.Task.FromResult(lease.Context.Messages.First());
        msg.SoftDelete();
        await lease.Context.SaveChangesAsync(Ct);

        (await new SearchMessagesHandler(lease.Context).Handle(new SearchMessagesQuery(channel.Id, "secret", member), Ct))
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task ANonMemberCannotSearchAPrivateChannel()
    {
        var (lease, channel, _) = await SeedAsync(["classified"], ChannelType.Private);
        await using var _d = lease;

        await Should.ThrowAsync<ForbiddenException>(() => new SearchMessagesHandler(lease.Context)
            .Handle(new SearchMessagesQuery(channel.Id, "classified", Guid.NewGuid()), Ct));
    }

    [Fact]
    public async Task WildcardCharactersInTheTermAreTakenLiterally()
    {
        var (lease, channel, member) = await SeedAsync(["100% done", "100 and more"]);
        await using var _ = lease;

        // "100%" must match the literal "100%" line, not "100<anything>" — otherwise escaping is broken.
        var results = await new SearchMessagesHandler(lease.Context)
            .Handle(new SearchMessagesQuery(channel.Id, "100%", member), Ct);

        results.Select(m => m.Body).ShouldBe(["100% done"]);
    }
}
