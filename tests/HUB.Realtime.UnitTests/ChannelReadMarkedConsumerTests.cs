using HUB.Realtime.WebApi.Consumers;
using HUB.Realtime.WebApi.Hubs;
using HUB.Realtime.WebApi.Presence;
using HUB.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Shouldly;
using Xunit;

namespace HUB.Realtime.UnitTests;

/// <summary>
/// Covers <c>ChannelReadMarkedConsumer</c>: it always syncs the reader's own badge across their devices,
/// and for a DM it also pushes a "Seen" read receipt to the other participant (CF-15) — but never for a
/// regular channel, where receipts would be noise.
/// </summary>
public sealed class ChannelReadMarkedConsumerTests
{
    private static readonly Guid ChannelId = Guid.NewGuid();
    private static readonly Guid Reader    = Guid.NewGuid();

    private sealed class Fixture
    {
        public IHubContext<ChatHub> Hub { get; } = Substitute.For<IHubContext<ChatHub>>();
        public IHubClients Clients { get; } = Substitute.For<IHubClients>();
        public IClientProxy OwnSessions { get; } = Substitute.For<IClientProxy>();
        public IClientProxy GroupOthers { get; } = Substitute.For<IClientProxy>();
        public IPresenceStore Presence { get; } = Substitute.For<IPresenceStore>();

        public Fixture(params string[] readerConnections)
        {
            Hub.Clients.Returns(Clients);
            Clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(OwnSessions);
            Clients.GroupExcept(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns(GroupOthers);
            Presence.GetConnectionsAsync(Reader, Arg.Any<CancellationToken>()).Returns(readerConnections);
        }

        public ChannelReadMarkedConsumer Consumer() => new(Hub, Presence);
    }

    private static ConsumeContext<ChannelReadMarked> Context(bool isDirect)
    {
        var ctx = Substitute.For<ConsumeContext<ChannelReadMarked>>();
        ctx.Message.Returns(new ChannelReadMarked(ChannelId, Reader, DateTime.UtcNow, isDirect));
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    [Fact]
    public async Task AReadInADmPushesAReceiptToTheOtherParticipant()
    {
        var f = new Fixture("conn-reader");

        await f.Consumer().Consume(Context(isDirect: true));

        // Own-session badge sync + a receipt to the group minus the reader's own connections.
        await f.OwnSessions.Received().SendCoreAsync("channelRead", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        await f.GroupOthers.Received().SendCoreAsync("readReceipt", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AReadInARegularChannelPushesNoReceipt()
    {
        var f = new Fixture("conn-reader");

        await f.Consumer().Consume(Context(isDirect: false));

        await f.OwnSessions.Received().SendCoreAsync("channelRead", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        await f.GroupOthers.DidNotReceiveWithAnyArgs().SendCoreAsync(default!, default!, default);
    }

    [Fact]
    public async Task AnOfflineReaderStillSendsTheDmReceipt()
    {
        // The reader has no live connection of their own (read came from another source), but the other
        // participant should still learn it was seen.
        var f = new Fixture();

        await f.Consumer().Consume(Context(isDirect: true));

        await f.OwnSessions.DidNotReceiveWithAnyArgs().SendCoreAsync(default!, default!, default);
        await f.GroupOthers.Received().SendCoreAsync("readReceipt", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }
}
