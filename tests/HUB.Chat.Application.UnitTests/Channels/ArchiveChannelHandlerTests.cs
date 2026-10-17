using HUB.Chat.Application.Channels.Commands.ArchiveChannel;
using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using HUB.Shared.Contracts.Events;
using HUB.TestKit.Db;
using HUB.TestKit.Fakes;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Channels;

/// <summary>
/// Covers archive/reopen (CF-13): Owner/Admin only on the public path, trusted (no actor) on the internal
/// path, idempotent, and announced.
/// </summary>
public sealed class ArchiveChannelHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static async Task<(ChatDbContextLease Lease, Channel Channel, Guid Owner)> SeedAsync()
    {
        var lease = await ChatDbContextFactory.CreateAsync();
        var owner = Guid.NewGuid();
        var channel = Channel.Create(Guid.NewGuid(), "General", ChannelType.Public, owner);
        lease.Context.Channels.Add(channel);
        await lease.Context.SaveChangesAsync(Ct);
        return (lease, channel, owner);
    }

    private static ArchiveChannelHandler Handler(ChatDbContextLease lease, RecordingEventPublisher events) =>
        new(lease.Context, events, new FakeWorkspacePermissions());

    private static async Task<bool> IsArchivedAsync(ChatDbContextLease lease, Guid id)
    {
        await using var v = lease.NewContext();
        return (await v.Channels.AsNoTracking().SingleAsync(c => c.Id == id, Ct)).IsArchived;
    }

    [Fact]
    public async Task TheOwnerArchivesAndItIsAnnounced()
    {
        var (lease, channel, owner) = await SeedAsync();
        await using var _ = lease;
        var events = new RecordingEventPublisher();

        await Handler(lease, events).Handle(new ArchiveChannelCommand(channel.Id, Archive: true, owner), Ct);

        (await IsArchivedAsync(lease, channel.Id)).ShouldBeTrue();
        events.Published.OfType<ChannelArchivedChanged>().ShouldHaveSingleItem().IsArchived.ShouldBeTrue();
    }

    [Fact]
    public async Task AnAdminCanArchive()
    {
        var (lease, channel, _) = await SeedAsync();
        await using var _d = lease;
        var admin = Guid.NewGuid();
        channel.AddMember(admin, ChannelMemberRole.Admin);
        await lease.Context.SaveChangesAsync(Ct);

        await Handler(lease, new RecordingEventPublisher()).Handle(new ArchiveChannelCommand(channel.Id, true, admin), Ct);

        (await IsArchivedAsync(lease, channel.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task APlainMemberCannotArchive()
    {
        var (lease, channel, _) = await SeedAsync();
        await using var _d = lease;
        var member = Guid.NewGuid();
        channel.AddMember(member, ChannelMemberRole.Member);
        await lease.Context.SaveChangesAsync(Ct);

        await Should.ThrowAsync<ForbiddenException>(() => Handler(lease, new RecordingEventPublisher())
            .Handle(new ArchiveChannelCommand(channel.Id, true, member), Ct));
    }

    [Fact]
    public async Task TheInternalPathArchivesWithoutAnActor()
    {
        var (lease, channel, _) = await SeedAsync();
        await using var _ = lease;

        // Sprint close calls with no actor — trusted service-to-service.
        await Handler(lease, new RecordingEventPublisher()).Handle(new ArchiveChannelCommand(channel.Id), Ct);

        (await IsArchivedAsync(lease, channel.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task ReopenReversesItAndArchivingTwiceAnnouncesOnce()
    {
        var (lease, channel, owner) = await SeedAsync();
        await using var _ = lease;
        var events = new RecordingEventPublisher();
        var handler = Handler(lease, events);

        await handler.Handle(new ArchiveChannelCommand(channel.Id, true, owner), Ct);
        await handler.Handle(new ArchiveChannelCommand(channel.Id, true, owner), Ct); // no-op
        await handler.Handle(new ArchiveChannelCommand(channel.Id, false, owner), Ct);

        (await IsArchivedAsync(lease, channel.Id)).ShouldBeFalse();
        events.Published.OfType<ChannelArchivedChanged>().Count().ShouldBe(2); // archive + reopen, not the no-op
    }
}
