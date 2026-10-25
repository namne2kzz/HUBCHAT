using HUB.Chat.Application.Channels.Commands.OpenGroupDm;
using HUB.Chat.Domain.Enums;
using HUB.TestKit.Db;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Channels;

/// <summary>
/// Covers the group-DM find-or-create handler: it always includes the caller, is idempotent for the same
/// set of people, and treats a different set as a different group.
/// </summary>
public sealed class OpenGroupDmHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task OpeningAGroupCreatesItWithTheCallerAndEveryMember()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var me = Guid.NewGuid();
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());

        var result = await new OpenGroupDmHandler(lease.Context)
            .Handle(new OpenGroupDmCommand(Guid.NewGuid(), [a, b], me), Ct);

        result.Type.ShouldBe(ChannelType.GroupDm);
        result.IsPrivate.ShouldBeTrue();
        result.MemberCount.ShouldBe(3); // the caller plus the two others

        await using var verify = lease.NewContext();
        var stored = await verify.Channels.AsNoTracking().Include(c => c.Members).SingleAsync(Ct);
        stored.Members.Select(m => m.UserId).ShouldBe([me, a, b], ignoreOrder: true);
    }

    [Fact]
    public async Task OpeningTheSameGroupTwiceReturnsTheSameChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var me = Guid.NewGuid();
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var handler = new OpenGroupDmHandler(lease.Context);

        var first  = await handler.Handle(new OpenGroupDmCommand(Guid.NewGuid(), [a, b], me), Ct);
        // The member order and who opens it should not matter — same people, same group.
        var second = await handler.Handle(new OpenGroupDmCommand(Guid.NewGuid(), [b, a], me), Ct);

        second.Id.ShouldBe(first.Id);

        await using var verify = lease.NewContext();
        (await verify.Channels.CountAsync(Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task AnyMemberReopeningTheGroupResolvesToTheSameChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var (me, a, b) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var handler = new OpenGroupDmHandler(lease.Context);

        var created = await handler.Handle(new OpenGroupDmCommand(Guid.NewGuid(), [a, b], me), Ct);
        // 'a' opens "the group with me and b" — same three people.
        var reopened = await handler.Handle(new OpenGroupDmCommand(Guid.NewGuid(), [me, b], a), Ct);

        reopened.Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task ADifferentSetOfPeopleIsADifferentGroup()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();
        var me = Guid.NewGuid();
        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var handler = new OpenGroupDmHandler(lease.Context);

        var g1 = await handler.Handle(new OpenGroupDmCommand(Guid.NewGuid(), [a, b], me), Ct);
        var g2 = await handler.Handle(new OpenGroupDmCommand(Guid.NewGuid(), [a, b, c], me), Ct);

        g2.Id.ShouldNotBe(g1.Id);
        (await lease.NewContext().Channels.CountAsync(Ct)).ShouldBe(2);
    }
}
