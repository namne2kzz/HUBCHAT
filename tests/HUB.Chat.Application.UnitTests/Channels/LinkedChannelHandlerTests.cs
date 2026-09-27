using HUB.Chat.Application.Channels.Commands.FindOrCreateSprintChannel;
using HUB.Chat.Application.Channels.Commands.OpenDirectMessage;
using HUB.Chat.Application.Channels.Commands.OpenLinkedThread;
using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Enums;
using HUB.TestKit.Db;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Channels;

/// <summary>
/// Covers the three find-or-create handlers: direct messages, DASHBOARD-linked discussion threads, and
/// sprint channels.
/// </summary>
/// <remarks>
/// All three are called repeatedly with the same arguments — opening a DM from a profile, clicking
/// "discuss" on a work item, syncing a sprint — so each has to return the existing channel rather than
/// make another. A duplicate is not a visible error; it silently splits one conversation into two, and
/// half the participants end up in the wrong copy.
/// </remarks>
public sealed class LinkedChannelHandlerTests
{
    // ── Direct messages ─────────────────────────────────────────────────────

    [Fact]
    public async Task OpeningADirectMessageCreatesItWithBothParticipants()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var me   = Guid.NewGuid();
        var them = Guid.NewGuid();

        var result = await new OpenDirectMessageHandler(lease.Context)
            .Handle(new OpenDirectMessageCommand(Guid.NewGuid(), them, me), CancellationToken.None);

        result.Type.ShouldBe(ChannelType.Dm);
        result.IsPrivate.ShouldBeTrue();
        result.MemberCount.ShouldBe(2);
        result.OtherUserId.ShouldBe(them);
    }

    [Fact]
    public async Task OpeningTheSameDirectMessageTwiceReturnsTheSameChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var me   = Guid.NewGuid();
        var them = Guid.NewGuid();

        var handler = new OpenDirectMessageHandler(lease.Context);
        var first  = await handler.Handle(
            new OpenDirectMessageCommand(Guid.NewGuid(), them, me), CancellationToken.None);
        var second = await handler.Handle(
            new OpenDirectMessageCommand(Guid.NewGuid(), them, me), CancellationToken.None);

        second.Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task TheOtherPersonOpeningItFindsTheSameChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var me   = Guid.NewGuid();
        var them = Guid.NewGuid();

        var handler = new OpenDirectMessageHandler(lease.Context);
        var mine   = await handler.Handle(
            new OpenDirectMessageCommand(Guid.NewGuid(), them, me), CancellationToken.None);
        var theirs = await handler.Handle(
            new OpenDirectMessageCommand(Guid.NewGuid(), me, them), CancellationToken.None);

        // The lookup matches on both members rather than on a name, so the pair is canonical in either
        // direction. Without that, each person would get their own half of the conversation.
        theirs.Id.ShouldBe(mine.Id);
        theirs.OtherUserId.ShouldBe(me, "each side sees the other as the counterpart");
    }

    [Fact]
    public async Task ADirectMessageIsFoundEvenFromAnotherWorkspace()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var me   = Guid.NewGuid();
        var them = Guid.NewGuid();

        var handler = new OpenDirectMessageHandler(lease.Context);
        var first  = await handler.Handle(
            new OpenDirectMessageCommand(Guid.NewGuid(), them, me), CancellationToken.None);
        var second = await handler.Handle(
            new OpenDirectMessageCommand(Guid.NewGuid(), them, me), CancellationToken.None);

        // DMs are person-to-person, so switching repository must not start a second conversation.
        second.Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task YouCannotOpenADirectMessageWithYourself()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var me = Guid.NewGuid();

        await Should.ThrowAsync<DomainException>(() => new OpenDirectMessageHandler(lease.Context)
            .Handle(new OpenDirectMessageCommand(Guid.NewGuid(), me, me), CancellationToken.None));
    }

    [Fact]
    public async Task TheDirectMessageNameIsTheSameWhicheverSideOpensIt()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var result = await new OpenDirectMessageHandler(lease.Context)
            .Handle(new OpenDirectMessageCommand(Guid.NewGuid(), b, a), CancellationToken.None);

        // The pair is sorted before building the name, so the generated name does not depend on who
        // clicked first. The display name is resolved client-side from OtherUserId.
        var (first, second) = a.CompareTo(b) < 0 ? (a, b) : (b, a);
        result.Name.ShouldBe($"dm-{first}-{second}");
    }

    // ── Linked discussion threads ───────────────────────────────────────────

    [Fact]
    public async Task OpeningALinkedThreadCreatesAChannelPointingAtTheResource()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var workspace  = Guid.NewGuid();
        var externalId = Guid.NewGuid();

        var result = await new OpenLinkedThreadHandler(lease.Context).Handle(
            new OpenLinkedThreadCommand(workspace, LinkedResourceType.WorkItem, externalId,
                "DASH-142", "Fix the thing", "http://localhost:4200/work-items/142", Guid.NewGuid()),
            CancellationToken.None);

        result.Name.ShouldBe("Fix the thing");
        result.LinkType.ShouldBe(LinkedResourceType.WorkItem);
        result.LinkExternalKey.ShouldBe("DASH-142");
        result.LinkUrl.ShouldBe("http://localhost:4200/work-items/142");
    }

    [Fact]
    public async Task ALinkedThreadWithNoTitleFallsBackToTheResourceKey()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        // Title is a non-nullable string on the command, so an absent title arrives as empty rather than
        // null — which is what the handler's IsNullOrWhiteSpace check is really guarding against.
        var result = await new OpenLinkedThreadHandler(lease.Context).Handle(
            new OpenLinkedThreadCommand(Guid.NewGuid(), LinkedResourceType.WikiPage, Guid.NewGuid(),
                "DASH-7", "", "http://x", Guid.NewGuid()),
            CancellationToken.None);

        // Better a channel named after the ticket than one named nothing at all.
        result.Name.ShouldBe("DASH-7");
    }

    [Fact]
    public async Task OpeningTheSameLinkedThreadTwiceReturnsTheSameChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var workspace  = Guid.NewGuid();
        var externalId = Guid.NewGuid();
        var handler    = new OpenLinkedThreadHandler(lease.Context);

        var first = await handler.Handle(
            new OpenLinkedThreadCommand(workspace, LinkedResourceType.WorkItem, externalId,
                "DASH-1", "First", "http://x", Guid.NewGuid()),
            CancellationToken.None);

        var second = await handler.Handle(
            new OpenLinkedThreadCommand(workspace, LinkedResourceType.WorkItem, externalId,
                "DASH-1", "First", "http://x", Guid.NewGuid()),
            CancellationToken.None);

        second.Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task ASecondPersonOpeningALinkedThreadJoinsIt()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var workspace  = Guid.NewGuid();
        var externalId = Guid.NewGuid();
        var handler    = new OpenLinkedThreadHandler(lease.Context);

        await handler.Handle(
            new OpenLinkedThreadCommand(workspace, LinkedResourceType.WorkItem, externalId,
                "DASH-1", "Thread", "http://x", Guid.NewGuid()),
            CancellationToken.None);

        var joiner = Guid.NewGuid();
        var result = await handler.Handle(
            new OpenLinkedThreadCommand(workspace, LinkedResourceType.WorkItem, externalId,
                "DASH-1", "Thread", "http://x", joiner),
            CancellationToken.None);

        // Clicking "discuss" on a work item should put you in the conversation, not merely show it.
        result.MemberCount.ShouldBe(2);

        await using var verify = lease.NewContext();
        var stored = await verify.Channels.AsNoTracking()
            .Include(c => c.Members).SingleAsync(c => c.Id == result.Id);
        stored.HasMember(joiner).ShouldBeTrue();
    }

    [Fact]
    public async Task ReopeningALinkedThreadDoesNotAddTheSamePersonTwice()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var workspace  = Guid.NewGuid();
        var externalId = Guid.NewGuid();
        var opener     = Guid.NewGuid();
        var handler    = new OpenLinkedThreadHandler(lease.Context);

        var command = new OpenLinkedThreadCommand(workspace, LinkedResourceType.Sprint, externalId,
            "SPRINT-1", "Thread", "http://x", opener);

        await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        second.MemberCount.ShouldBe(1);
    }

    [Fact]
    public async Task TheSameResourceIdInAnotherWorkspaceGetsItsOwnThread()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var externalId = Guid.NewGuid();
        var handler    = new OpenLinkedThreadHandler(lease.Context);

        var first = await handler.Handle(
            new OpenLinkedThreadCommand(Guid.NewGuid(), LinkedResourceType.WorkItem, externalId,
                "DASH-1", "A", "http://x", Guid.NewGuid()),
            CancellationToken.None);

        var second = await handler.Handle(
            new OpenLinkedThreadCommand(Guid.NewGuid(), LinkedResourceType.WorkItem, externalId,
                "DASH-1", "B", "http://x", Guid.NewGuid()),
            CancellationToken.None);

        // The lookup is scoped by workspace, so an id colliding across repositories does not merge two
        // unrelated discussions.
        second.Id.ShouldNotBe(first.Id);
    }

    // ── Sprint channels (called by DASHBOARD) ───────────────────────────────

    [Fact]
    public async Task ASprintChannelIsPrivateAndLinkedToTheSprint()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var workspace = Guid.NewGuid();
        var sprintId  = Guid.NewGuid();

        var result = await new FindOrCreateSprintChannelHandler(lease.Context).Handle(
            new FindOrCreateSprintChannelCommand(workspace, sprintId, "Sprint 42", Guid.NewGuid()),
            CancellationToken.None);

        result.IsPrivate.ShouldBeTrue("a sprint channel is for its team, not the whole workspace");
        result.Type.ShouldBe(ChannelType.Private);
        result.LinkType.ShouldBe(LinkedResourceType.Sprint);
        result.Name.ShouldBe("sprint-sprint-42");
    }

    [Fact]
    public async Task CreatingTheSameSprintChannelTwiceReturnsTheSameChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var workspace = Guid.NewGuid();
        var sprintId  = Guid.NewGuid();
        var handler   = new FindOrCreateSprintChannelHandler(lease.Context);

        var first = await handler.Handle(
            new FindOrCreateSprintChannelCommand(workspace, sprintId, "Sprint 1", Guid.NewGuid()),
            CancellationToken.None);

        var second = await handler.Handle(
            new FindOrCreateSprintChannelCommand(workspace, sprintId, "Sprint 1", Guid.NewGuid()),
            CancellationToken.None);

        // DASHBOARD calls this whenever a sprint opens and does not remember whether it called before.
        second.Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task ARenamedSprintKeepsItsExistingChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var workspace = Guid.NewGuid();
        var sprintId  = Guid.NewGuid();
        var handler   = new FindOrCreateSprintChannelHandler(lease.Context);

        var first = await handler.Handle(
            new FindOrCreateSprintChannelCommand(workspace, sprintId, "Sprint 1", Guid.NewGuid()),
            CancellationToken.None);

        var second = await handler.Handle(
            new FindOrCreateSprintChannelCommand(workspace, sprintId, "Sprint One Renamed", Guid.NewGuid()),
            CancellationToken.None);

        // The lookup is by sprint id, not by name, so renaming a sprint in DASHBOARD does not orphan its
        // channel and start a fresh one. The channel keeps its original name.
        second.Id.ShouldBe(first.Id);
        second.Name.ShouldBe("sprint-sprint-1");
    }

    [Fact]
    public async Task TheSameSprintIdInAnotherWorkspaceGetsItsOwnChannel()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var sprintId = Guid.NewGuid();
        var handler  = new FindOrCreateSprintChannelHandler(lease.Context);

        var first = await handler.Handle(
            new FindOrCreateSprintChannelCommand(Guid.NewGuid(), sprintId, "Sprint 1", Guid.NewGuid()),
            CancellationToken.None);

        var second = await handler.Handle(
            new FindOrCreateSprintChannelCommand(Guid.NewGuid(), sprintId, "Sprint 1", Guid.NewGuid()),
            CancellationToken.None);

        second.Id.ShouldNotBe(first.Id);
    }

    [Fact]
    public async Task TheSprintCreatorBecomesTheChannelOwner()
    {
        await using var lease = await ChatDbContextFactory.CreateAsync();

        var creator = Guid.NewGuid();
        var result  = await new FindOrCreateSprintChannelHandler(lease.Context).Handle(
            new FindOrCreateSprintChannelCommand(Guid.NewGuid(), Guid.NewGuid(), "Sprint 1", creator),
            CancellationToken.None);

        await using var verify = lease.NewContext();
        var stored = await verify.Channels.AsNoTracking()
            .Include(c => c.Members).SingleAsync(c => c.Id == result.Id);

        stored.Members.ShouldHaveSingleItem().Role.ShouldBe(ChannelMemberRole.Owner);
    }
}
