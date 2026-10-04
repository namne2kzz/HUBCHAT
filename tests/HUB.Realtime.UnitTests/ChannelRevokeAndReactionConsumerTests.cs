using HUB.Realtime.WebApi.Channels;
using HUB.Realtime.WebApi.Consumers;
using HUB.Realtime.WebApi.Hubs;
using HUB.Realtime.WebApi.Presence;
using HUB.Shared.Contracts.Events;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace HUB.Realtime.UnitTests;

/// <summary>
/// Covers <c>ChannelMemberRemovedConsumer</c> (revoking a removed member's live subscription) and
/// <c>ReactionAddedConsumer</c> (pushing reactions to the channel group).
/// </summary>
/// <remarks>
/// The revoke consumer is the only thing that stops a kicked user from reading a private channel in real
/// time: the hub checks membership on join and never again. The assertions pin the three steps and their
/// order — evicting the cached "allow" after the group removal would let a fast re-join slip back in.
/// </remarks>
public sealed class ChannelRevokeAndReactionConsumerTests
{
    private static readonly Guid ChannelId = Guid.NewGuid();
    private static readonly Guid UserId    = Guid.NewGuid();

    private sealed class Fixture
    {
        public IHubContext<ChatHub> Hub { get; } = Substitute.For<IHubContext<ChatHub>>();
        public IGroupManager Groups { get; } = Substitute.For<IGroupManager>();
        public IHubClients Clients { get; } = Substitute.For<IHubClients>();
        public IClientProxy Proxy { get; } = Substitute.For<IClientProxy>();
        public IPresenceStore Presence { get; } = Substitute.For<IPresenceStore>();
        public IChannelAccessService Access { get; } = Substitute.For<IChannelAccessService>();

        public Fixture(params string[] connections)
        {
            Hub.Groups.Returns(Groups);
            Hub.Clients.Returns(Clients);
            Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(Proxy);
            Clients.Group(Arg.Any<string>()).Returns(Proxy);
            Presence.GetConnectionsAsync(UserId, Arg.Any<CancellationToken>()).Returns(connections);
        }

        public ChannelMemberRemovedConsumer Consumer() =>
            new(Hub, Presence, Access, NullLogger<ChannelMemberRemovedConsumer>.Instance);
    }

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var ctx = Substitute.For<ConsumeContext<T>>();
        ctx.Message.Returns(message);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    [Fact]
    public async Task ARemovedMemberIsEvictedThenPulledOutOfTheGroupThenTold()
    {
        var f = new Fixture("conn-a", "conn-b");

        await f.Consumer().Consume(Context(new ChannelMemberRemoved(ChannelId, UserId)));

        var group = ChatHub.ChannelGroup(ChannelId);
        Received.InOrder(() =>
        {
            f.Access.InvalidateAsync(ChannelId, UserId, Arg.Any<CancellationToken>());
            f.Groups.RemoveFromGroupAsync("conn-a", group, Arg.Any<CancellationToken>());
            f.Groups.RemoveFromGroupAsync("conn-b", group, Arg.Any<CancellationToken>());
            f.Proxy.SendCoreAsync("channelAccessRevoked", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        });

        // Every tab/device of the user, not just one.
        f.Clients.Received().Clients(Arg.Is<IReadOnlyList<string>>(c => c.SequenceEqual(new[] { "conn-a", "conn-b" })));
    }

    [Fact]
    public async Task AnOfflineUserOnlyHasTheCacheEvicted()
    {
        var f = new Fixture();

        await f.Consumer().Consume(Context(new ChannelMemberRemoved(ChannelId, UserId)));

        // Their next join is checked fresh against chat-service — which now says no.
        await f.Access.Received().InvalidateAsync(ChannelId, UserId, Arg.Any<CancellationToken>());
        await f.Groups.DidNotReceiveWithAnyArgs().RemoveFromGroupAsync(default!, default!, default);
        await f.Proxy.DidNotReceiveWithAnyArgs().SendCoreAsync(default!, default!, default);
    }

    [Fact]
    public async Task AStaleConnectionDoesNotStopTheOthersBeingRevoked()
    {
        var f = new Fixture("dead-conn", "live-conn");
        // A connection that died without a clean disconnect lingers in presence until its TTL; removing it
        // waits on an ack nobody sends.
        f.Groups.RemoveFromGroupAsync("dead-conn", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        await Should.NotThrowAsync(() => f.Consumer().Consume(Context(new ChannelMemberRemoved(ChannelId, UserId))));

        await f.Groups.Received().RemoveFromGroupAsync("live-conn", ChatHub.ChannelGroup(ChannelId), Arg.Any<CancellationToken>());
        await f.Proxy.Received().SendCoreAsync("channelAccessRevoked", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AReactionIsPushedToItsChannelGroup()
    {
        var f = new Fixture();
        var messageId = Guid.NewGuid();

        await new ReactionAddedConsumer(f.Hub).Consume(Context(new ReactionAdded(messageId, ChannelId, UserId, ":+1:")));

        f.Clients.Received().Group($"channel:{ChannelId}");
        await f.Proxy.Received().SendCoreAsync("reactionAdded", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BothConsumersAreWiredToTheirEvents()
    {
        // Real MassTransit dispatch: proves the consumers are registered for these message types at all.
        var f = new Fixture("conn-a");
        await using var provider = new ServiceCollection()
            .AddSingleton(f.Hub)
            .AddSingleton(f.Presence)
            .AddSingleton(f.Access)
            .AddLogging()
            .AddMassTransitTestHarness(bus =>
            {
                bus.AddConsumer<ChannelMemberRemovedConsumer>();
                bus.AddConsumer<ReactionAddedConsumer>();
            })
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new ChannelMemberRemoved(ChannelId, UserId));
        await harness.Bus.Publish(new ReactionAdded(Guid.NewGuid(), ChannelId, UserId, ":tada:"));

        (await harness.Consumed.Any<ChannelMemberRemoved>()).ShouldBeTrue();
        (await harness.Consumed.Any<ReactionAdded>()).ShouldBeTrue();
        await f.Access.Received().InvalidateAsync(ChannelId, UserId, Arg.Any<CancellationToken>());

        await harness.Stop();
    }
}
