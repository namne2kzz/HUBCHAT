using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Messages.Commands.ForwardMessage;
using HUB.Chat.Domain.Common;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using HUB.Shared.Contracts.Events;
using HUB.TestKit.Db;
using HUB.TestKit.Fakes;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Messages;

/// <summary>
/// Covers <c>ForwardMessageHandler</c> (CF-7 forward): copies a readable message into a channel the caller
/// belongs to, records the origin, and announces it.
/// </summary>
public sealed class ForwardMessageHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static async Task<(ChatDbContextLease Lease, Guid User, Message Source, Channel Target)> SeedAsync(
        bool userInTarget = true, bool sourcePublic = true)
    {
        var lease = await ChatDbContextFactory.CreateAsync();
        var user  = Guid.NewGuid();

        var sourceChannel = Channel.Create(Guid.NewGuid(), "source",
            sourcePublic ? ChannelType.Public : ChannelType.Private, user); // user is a member (owner) → can read
        var source = Message.Post(sourceChannel.Id, user, "shared idea", MessageFormat.Markdown);

        var target = Channel.Create(Guid.NewGuid(), "target", ChannelType.Public, Guid.NewGuid());
        if (userInTarget) target.AddMember(user);

        lease.Context.Channels.AddRange(sourceChannel, target);
        lease.Context.Messages.Add(source);
        await lease.Context.SaveChangesAsync(Ct);
        return (lease, user, source, target);
    }

    [Fact]
    public async Task ForwardsTheBodyIntoTheTargetAndRecordsTheOrigin()
    {
        var (lease, user, source, target) = await SeedAsync();
        await using var _ = lease;
        var events = new RecordingEventPublisher();

        var dto = await new ForwardMessageHandler(lease.Context, events)
            .Handle(new ForwardMessageCommand(source.Id, target.Id, user), Ct);

        dto.ChannelId.ShouldBe(target.Id);
        dto.Body.ShouldBe("shared idea");
        dto.ForwardedFromId.ShouldBe(source.Id);
        dto.AuthorId.ShouldBe(user);
        events.Published.OfType<MessageSent>().ShouldHaveSingleItem().ChannelId.ShouldBe(target.Id);

        await using var verify = lease.NewContext();
        (await verify.Messages.CountAsync(m => m.ChannelId == target.Id, Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task CannotForwardIntoAChannelYouAreNotIn()
    {
        var (lease, user, source, target) = await SeedAsync(userInTarget: false);
        await using var _ = lease;

        await Should.ThrowAsync<ForbiddenException>(() => new ForwardMessageHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new ForwardMessageCommand(source.Id, target.Id, user), Ct));
    }

    [Fact]
    public async Task CannotForwardAMessageYouCannotRead()
    {
        // Source is private and the caller is NOT a member of it; they only belong to the target.
        var lease = await ChatDbContextFactory.CreateAsync();
        await using var _ = lease;
        var outsider = Guid.NewGuid();
        var sourceChannel = Channel.Create(Guid.NewGuid(), "secret", ChannelType.Private, Guid.NewGuid());
        var source = Message.Post(sourceChannel.Id, sourceChannel.Members.First().UserId, "secret", MessageFormat.Plain);
        var target = Channel.Create(Guid.NewGuid(), "target", ChannelType.Public, Guid.NewGuid());
        target.AddMember(outsider);
        lease.Context.Channels.AddRange(sourceChannel, target);
        lease.Context.Messages.Add(source);
        await lease.Context.SaveChangesAsync(Ct);

        await Should.ThrowAsync<ForbiddenException>(() => new ForwardMessageHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new ForwardMessageCommand(source.Id, target.Id, outsider), Ct));
    }

    [Fact]
    public async Task CannotForwardADeletedMessage()
    {
        var (lease, user, source, target) = await SeedAsync();
        await using var _ = lease;
        var tracked = await lease.Context.Messages.FirstAsync(m => m.Id == source.Id, Ct);
        tracked.SoftDelete();
        await lease.Context.SaveChangesAsync(Ct);

        await Should.ThrowAsync<DomainException>(() => new ForwardMessageHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new ForwardMessageCommand(source.Id, target.Id, user), Ct));
    }

    [Fact]
    public async Task AMissingSourceIsNotFound()
    {
        var (lease, user, _, target) = await SeedAsync();
        await using var _d = lease;

        await Should.ThrowAsync<NotFoundException>(() => new ForwardMessageHandler(lease.Context, new RecordingEventPublisher())
            .Handle(new ForwardMessageCommand(Guid.NewGuid(), target.Id, user), Ct));
    }
}
