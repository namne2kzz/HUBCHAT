using HUB.Realtime.WebApi.Consumers;
using HUB.Realtime.WebApi.Hubs;
using HUB.Realtime.WebApi.Presence;
using HUB.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Xunit;

namespace HUB.Realtime.UnitTests;

/// <summary>
/// Covers <c>NotificationCreatedConsumer</c>: a stored notification reaches every open session of its
/// recipient — and only theirs.
/// </summary>
public sealed class NotificationCreatedConsumerTests
{
    private static readonly Guid Recipient = Guid.NewGuid();

    private static (NotificationCreatedConsumer Consumer, IHubClients Clients, IClientProxy Proxy) Build(params string[] connections)
    {
        var hub      = Substitute.For<IHubContext<ChatHub>>();
        var clients  = Substitute.For<IHubClients>();
        var proxy    = Substitute.For<IClientProxy>();
        var presence = Substitute.For<IPresenceStore>();
        hub.Clients.Returns(clients);
        clients.Clients(Arg.Any<IReadOnlyList<string>>()).Returns(proxy);
        presence.GetConnectionsAsync(Recipient, Arg.Any<CancellationToken>()).Returns(connections);
        return (new NotificationCreatedConsumer(hub, presence), clients, proxy);
    }

    private static ConsumeContext<NotificationCreated> Context()
    {
        var ctx = Substitute.For<ConsumeContext<NotificationCreated>>();
        ctx.Message.Returns(new NotificationCreated(Guid.NewGuid(), Recipient, 0, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "hi", DateTime.UtcNow));
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    [Fact]
    public async Task PushesToEveryConnectionOfTheRecipient()
    {
        var (consumer, clients, proxy) = Build("tab-1", "phone-1");

        await consumer.Consume(Context());

        // Addressed by the recipient's own connection ids — never a group, never Clients.All.
        clients.Received().Clients(Arg.Is<IReadOnlyList<string>>(c => c.SequenceEqual(new[] { "tab-1", "phone-1" })));
        await proxy.Received(1).SendCoreAsync("notificationReceived", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnOfflineRecipientGetsNoPush()
    {
        var (consumer, clients, proxy) = Build();

        await consumer.Consume(Context());

        // Stored already; it shows up on their next list load.
        clients.DidNotReceiveWithAnyArgs().Clients(default(IReadOnlyList<string>)!);
        await proxy.DidNotReceiveWithAnyArgs().SendCoreAsync(default!, default!, default);
    }
}
