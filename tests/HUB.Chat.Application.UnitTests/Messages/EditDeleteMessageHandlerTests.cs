using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Messages.Commands.DeleteMessage;
using HUB.Chat.Application.Messages.Commands.EditMessage;
using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using HUB.Shared.Contracts.Events;
using HUB.TestKit.Builders;
using HUB.TestKit.Db;
using HUB.TestKit.Fakes;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Messages;

/// <summary>
/// Covers edit and delete: edit is author-only within the window; delete allows the author (within the
/// window) or a channel moderator; both announce their change for realtime, and a delete leaves a tombstone.
/// </summary>
public sealed class EditDeleteMessageHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static async Task<(ChatDbContextLease Lease, Channel Channel, Message Message, Guid Author)> SeedAsync(
        ChannelMemberRole authorRole = ChannelMemberRole.Owner, int ageMinutes = 1)
    {
        var lease  = await ChatDbContextFactory.CreateAsync();
        var author = Guid.NewGuid();
        var channel = Channel.Create(Guid.NewGuid(), "General", ChannelType.Public, author); // author is Owner
        if (authorRole == ChannelMemberRole.Member)
        {
            // Re-seed so the author is a plain member, with a separate owner holding the channel.
            channel = Channel.Create(Guid.NewGuid(), "General", ChannelType.Public, Guid.NewGuid());
            channel.AddMember(author, ChannelMemberRole.Member);
        }
        lease.Context.Channels.Add(channel);
        var message = Message.Post(channel.Id, author, "original").WithCreatedAt(DateTime.UtcNow.AddMinutes(-ageMinutes));
        lease.Context.Messages.Add(message);
        await lease.Context.SaveChangesAsync(Ct);
        return (lease, channel, message, author);
    }

    private static DeleteMessageHandler Deleter(ChatDbContextLease lease, RecordingEventPublisher events, FakeWorkspacePermissions? perms = null) =>
        new(lease.Context, events, perms ?? new FakeWorkspacePermissions());

    private static async Task<Message> ReloadAsync(ChatDbContextLease lease, Guid id)
    {
        await using var verify = lease.NewContext();
        return await verify.Messages.AsNoTracking().SingleAsync(m => m.Id == id, Ct);
    }

    // ── Edit ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheAuthorEditsTheirMessageAndItIsAnnounced()
    {
        var (lease, _, message, author) = await SeedAsync();
        await using var _d = lease;
        var events = new RecordingEventPublisher();

        var dto = await new EditMessageHandler(lease.Context, events)
            .Handle(new EditMessageCommand(message.Id, "fixed typo", author), Ct);

        dto.Body.ShouldBe("fixed typo");
        dto.EditedAt.ShouldNotBeNull();
        var e = events.Published.OfType<MessageEdited>().ShouldHaveSingleItem();
        e.Body.ShouldBe("fixed typo");
        (await ReloadAsync(lease, message.Id)).Body.ShouldBe("fixed typo");
    }

    [Fact]
    public async Task OnlyTheAuthorCanEdit_EvenAModerator()
    {
        var (lease, channel, message, _) = await SeedAsync();
        await using var _d = lease;
        // The owner here is the author; add a different Admin and have them try to edit.
        channel.AddMember(Guid.NewGuid(), ChannelMemberRole.Admin);
        await lease.Context.SaveChangesAsync(Ct);
        var admin = channel.Members.Single(m => m.Role == ChannelMemberRole.Admin).UserId;

        // Editing reauthors content, so a moderator must not do it — they may only remove.
        await Should.ThrowAsync<ForbiddenException>(() => new EditMessageHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new EditMessageCommand(message.Id, "tampered", admin), Ct));
    }

    [Fact]
    public async Task EditingOutsideTheWindowIsRejected()
    {
        var (lease, _, message, author) = await SeedAsync(ageMinutes: 60 * 25); // 25h old
        await using var _d = lease;

        await Should.ThrowAsync<DomainException>(() => new EditMessageHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new EditMessageCommand(message.Id, "too late", author), Ct));
    }

    // ── Delete ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheAuthorDeletesWithinTheWindowAndAnnouncesIt()
    {
        var (lease, _, message, author) = await SeedAsync();
        await using var _d = lease;
        var events = new RecordingEventPublisher();

        await Deleter(lease, events).Handle(new DeleteMessageCommand(message.Id, author), Ct);

        events.Published.OfType<MessageDeleted>().ShouldHaveSingleItem().MessageId.ShouldBe(message.Id);
        (await ReloadAsync(lease, message.Id)).IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task AModeratorDeletesSomeoneElsesMessageEvenOldOnes()
    {
        // Author is a plain member; the message is older than the window.
        var (lease, channel, message, _) = await SeedAsync(ChannelMemberRole.Member, ageMinutes: 60 * 48);
        await using var _d = lease;
        var owner = channel.Members.Single(m => m.Role == ChannelMemberRole.Owner).UserId;
        var events = new RecordingEventPublisher();

        // A moderator removing abuse is not bound by the author's 24h window.
        await Deleter(lease, events).Handle(new DeleteMessageCommand(message.Id, owner), Ct);

        (await ReloadAsync(lease, message.Id)).IsDeleted.ShouldBeTrue();
        events.Published.OfType<MessageDeleted>().Count().ShouldBe(1);
    }

    [Fact]
    public async Task AnAuthorCannotDeleteTheirOwnMessageAfterTheWindow()
    {
        // Author is a plain member (not a moderator), message older than the window.
        var (lease, _, message, author) = await SeedAsync(ChannelMemberRole.Member, ageMinutes: 60 * 25);
        await using var _d = lease;

        // Past the window and not a moderator → refused (a moderator still could, see above).
        await Should.ThrowAsync<ForbiddenException>(() => Deleter(lease, new RecordingEventPublisher())
            .Handle(new DeleteMessageCommand(message.Id, author), Ct));
    }

    [Fact]
    public async Task AStrangerCannotDelete()
    {
        var (lease, _, message, _) = await SeedAsync();
        await using var _d = lease;

        await Should.ThrowAsync<ForbiddenException>(() => Deleter(lease, new RecordingEventPublisher())
            .Handle(new DeleteMessageCommand(message.Id, Guid.NewGuid()), Ct));
    }

    [Fact]
    public async Task DeletingAnAlreadyDeletedMessageIsANoOp()
    {
        var (lease, _, message, author) = await SeedAsync();
        await using var _d = lease;
        var events = new RecordingEventPublisher();
        var handler = Deleter(lease, events);

        await handler.Handle(new DeleteMessageCommand(message.Id, author), Ct);
        await handler.Handle(new DeleteMessageCommand(message.Id, author), Ct);

        // A retry or double click must not re-announce the deletion.
        events.Published.OfType<MessageDeleted>().Count().ShouldBe(1);
    }
}
