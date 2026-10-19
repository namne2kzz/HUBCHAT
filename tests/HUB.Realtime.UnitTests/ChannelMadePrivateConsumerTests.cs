using HUB.Realtime.WebApi.Channels;
using HUB.Realtime.WebApi.Consumers;
using HUB.Realtime.WebApi.Hubs;
using HUB.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace HUB.Realtime.UnitTests;

/// <summary>
/// Covers <c>ChannelMadePrivateConsumer</c>: when a public channel is locked down, every live subscriber
/// who is not a member loses their realtime subscription, while members keep theirs.
/// </summary>
/// <remarks>
/// This is the fix for a real hole: a public channel admits anyone to its realtime group, and the hub only
/// checks membership on join. Without this, making the channel private left the non-members who were
/// watching it still receiving every message until they happened to reconnect.
/// </remarks>
public sealed class ChannelMadePrivateConsumerTests
{
    private static readonly Guid ChannelId = Guid.NewGuid();

    private sealed class Fixture
    {
        public IHubContext<ChatHub> Hub { get; } = Substitute.For<IHubContext<ChatHub>>();
        public IGroupManager Groups { get; } = Substitute.For<IGroupManager>();
        public IHubClients Clients { get; } = Substitute.For<IHubClients>();
        public ISingleClientProxy Proxy { get; } = Substitute.For<ISingleClientProxy>();
        public IChannelSubscriptions Subs { get; } = Substitute.For<IChannelSubscriptions>();
        public IChannelAccessService Access { get; } = Substitute.For<IChannelAccessService>();

        public Fixture(params (string Conn, Guid User)[] subscribers)
        {
            Hub.Groups.Returns(Groups);
            Hub.Clients.Returns(Clients);
            Clients.Client(Arg.Any<string>()).Returns(Proxy);
            Subs.GetSubscribersAsync(ChannelId, Arg.Any<CancellationToken>())
                .Returns(subscribers.Select(s => (s.Conn, s.User)).ToList());
        }

        public ChannelMadePrivateConsumer Consumer() =>
            new(Hub, Subs, Access, NullLogger<ChannelMadePrivateConsumer>.Instance);
    }

    private static ConsumeContext<ChannelMadePrivate> Context(params Guid[] memberIds)
    {
        var ctx = Substitute.For<ConsumeContext<ChannelMadePrivate>>();
        ctx.Message.Returns(new ChannelMadePrivate(ChannelId, memberIds));
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    [Fact]
    public async Task ANonMemberWatcherIsRevoked()
    {
        var member    = Guid.NewGuid();
        var outsider  = Guid.NewGuid();
        var f = new Fixture(("conn-member", member), ("conn-outsider", outsider));

        await f.Consumer().Consume(Context(member));

        var group = ChatHub.ChannelGroup(ChannelId);
        // The outsider is evicted, pulled from the group, forgotten, and told.
        await f.Access.Received().InvalidateAsync(ChannelId, outsider, Arg.Any<CancellationToken>());
        await f.Groups.Received().RemoveFromGroupAsync("conn-outsider", group, Arg.Any<CancellationToken>());
        await f.Subs.Received().RemoveAsync(ChannelId, "conn-outsider", Arg.Any<CancellationToken>());
        await f.Proxy.Received().SendCoreAsync("channelAccessRevoked", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMemberKeepsWatching()
    {
        var member = Guid.NewGuid();
        var f = new Fixture(("conn-member", member));

        await f.Consumer().Consume(Context(member));

        // Nothing is revoked for a member — they are allowed in the now-private channel.
        await f.Groups.DidNotReceiveWithAnyArgs().RemoveFromGroupAsync(default!, default!, default);
        await f.Access.DidNotReceiveWithAnyArgs().InvalidateAsync(default, default, default);
    }

    [Fact]
    public async Task WithNoSubscribersNothingHappens()
    {
        var f = new Fixture();

        await f.Consumer().Consume(Context(Guid.NewGuid()));

        await f.Groups.DidNotReceiveWithAnyArgs().RemoveFromGroupAsync(default!, default!, default);
    }
}
