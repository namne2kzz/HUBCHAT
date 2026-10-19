using HUB.Chat.Application.Common.Exceptions;
using HUB.Chat.Application.Messages.Queries.ListChannelAttachments;
using HUB.Chat.Domain.Entities;
using HUB.Chat.Domain.Enums;
using HUB.TestKit.Db;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HUB.Chat.Application.UnitTests.Messages;

/// <summary>
/// Covers the Files/Media panel query (CF-5): it flattens a channel's attachments newest-first, filters by
/// kind, excludes deleted messages, and is gated by channel read access.
/// </summary>
public sealed class ListChannelAttachmentsHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static Message MessageWith(Guid channelId, Guid author, params (AttachmentKind Kind, string Name)[] atts)
    {
        var m = Message.Post(channelId, author, "see files", MessageFormat.Plain, allowEmptyBody: true);
        foreach (var (kind, name) in atts)
            m.AddAttachment(kind, Guid.NewGuid().ToString(), name, 1024, "application/octet-stream");
        return m;
    }

    private static async Task<(ChatDbContextLease Lease, Channel Channel, Guid Member)> SeedAsync()
    {
        var lease = await ChatDbContextFactory.CreateAsync();
        var member = Guid.NewGuid();
        var channel = Channel.Create(Guid.NewGuid(), "General", ChannelType.Public, member);
        lease.Context.Channels.Add(channel);
        await lease.Context.SaveChangesAsync(Ct);
        return (lease, channel, member);
    }

    [Fact]
    public async Task ItReturnsAllAttachmentsNewestFirst()
    {
        var (lease, channel, member) = await SeedAsync();
        await using var _ = lease;

        var older = MessageWith(channel.Id, member, (AttachmentKind.File, "a.pdf"));
        lease.Context.Messages.Add(older);
        await lease.Context.SaveChangesAsync(Ct);
        await Task.Delay(5, Ct);
        var newer = MessageWith(channel.Id, member, (AttachmentKind.Image, "b.png"));
        lease.Context.Messages.Add(newer);
        await lease.Context.SaveChangesAsync(Ct);

        var result = await new ListChannelAttachmentsHandler(lease.Context)
            .Handle(new ListChannelAttachmentsQuery(channel.Id, member), Ct);

        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("b.png"); // newest message first
    }

    [Fact]
    public async Task ItFiltersByKind()
    {
        var (lease, channel, member) = await SeedAsync();
        await using var _ = lease;
        lease.Context.Messages.Add(MessageWith(channel.Id, member,
            (AttachmentKind.Image, "pic.png"), (AttachmentKind.File, "doc.pdf")));
        await lease.Context.SaveChangesAsync(Ct);

        var images = await new ListChannelAttachmentsHandler(lease.Context)
            .Handle(new ListChannelAttachmentsQuery(channel.Id, member, AttachmentKind.Image), Ct);

        images.ShouldHaveSingleItem().Name.ShouldBe("pic.png");
    }

    [Fact]
    public async Task ItExcludesDeletedMessages()
    {
        var (lease, channel, member) = await SeedAsync();
        await using var _ = lease;
        var m = MessageWith(channel.Id, member, (AttachmentKind.Image, "gone.png"));
        m.SoftDelete();
        lease.Context.Messages.Add(m);
        await lease.Context.SaveChangesAsync(Ct);

        var result = await new ListChannelAttachmentsHandler(lease.Context)
            .Handle(new ListChannelAttachmentsQuery(channel.Id, member), Ct);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ANonReaderOfAPrivateChannelIsRefused()
    {
        var lease = await ChatDbContextFactory.CreateAsync();
        await using var _ = lease;
        var owner = Guid.NewGuid();
        var channel = Channel.Create(Guid.NewGuid(), "Secret", ChannelType.Private, owner);
        lease.Context.Channels.Add(channel);
        lease.Context.Messages.Add(MessageWith(channel.Id, owner, (AttachmentKind.Image, "p.png")));
        await lease.Context.SaveChangesAsync(Ct);

        await Should.ThrowAsync<ForbiddenException>(() => new ListChannelAttachmentsHandler(lease.Context)
            .Handle(new ListChannelAttachmentsQuery(channel.Id, Guid.NewGuid()), Ct));
    }
}
