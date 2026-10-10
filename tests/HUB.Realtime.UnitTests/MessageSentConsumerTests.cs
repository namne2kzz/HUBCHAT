using HUB.Realtime.WebApi.Consumers;
using HUB.Realtime.WebApi.Hubs;
using HUB.Shared.Contracts.Events;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace HUB.Realtime.UnitTests;

/// <summary>
/// Covers <c>MessageSentConsumer</c>: that a message published by the Chat service reaches the right
/// SignalR group, and only that group.
/// </summary>
/// <remarks>
/// This consumer is the whole of realtime delivery. Chat saves a message and publishes
/// <see cref="MessageSent"/>; nothing else tells a connected client the message exists. If the fan-out
/// picks the wrong group name, every client stays silent until somebody refreshes — and no test of the
/// Chat service would notice, because Chat's job ended when it published.
///
/// The group name is the part worth pinning. <c>ChatHub.JoinChannel</c> subscribes a connection to
/// <c>channel:{id}</c> and this consumer sends to the same string; they are computed by one helper for
/// exactly that reason, and a divergence would be silent in both directions.
/// </remarks>
public sealed class MessageSentConsumerTests
{
    private static MessageSent AnEvent(Guid channelId, Guid[]? mentions = null) =>
        new(Guid.NewGuid(), channelId, Guid.NewGuid(), "preview text", mentions ?? []);

    [Fact]
    public async Task AMessageIsSentToItsChannelGroup()
    {
        var clients = Substitute.For<IHubClients>();
        var group   = Substitute.For<IClientProxy>();
        var hub     = Substitute.For<IHubContext<ChatHub>>();

        hub.Clients.Returns(clients);
        clients.Group(Arg.Any<string>()).Returns(group);

        var channelId = Guid.NewGuid();
        var consumer  = new MessageSentConsumer(hub);

        await consumer.Consume(ConsumeContextFor(AnEvent(channelId)));

        // The group name is the contract between JoinChannel and this consumer.
        clients.Received(1).Group($"channel:{channelId}");
    }

    [Fact]
    public async Task TheGroupNameMatchesWhatTheHubSubscribesTo()
    {
        // Asserted against the hub's own helper rather than a literal, so that renaming the scheme in one
        // place and not the other cannot pass. A mismatch means every client subscribes to one name while
        // the server broadcasts to another, and realtime silently stops working.
        var channelId = Guid.NewGuid();

        ChatHub.ChannelGroup(channelId).ShouldBe($"channel:{channelId}");
    }

    [Fact]
    public async Task TheClientEventIsNamedMessageReceived()
    {
        var clients = Substitute.For<IHubClients>();
        var group   = Substitute.For<IClientProxy>();
        var hub     = Substitute.For<IHubContext<ChatHub>>();

        hub.Clients.Returns(clients);
        clients.Group(Arg.Any<string>()).Returns(group);

        await new MessageSentConsumer(hub).Consume(ConsumeContextFor(AnEvent(Guid.NewGuid())));

        // The frontend registers a handler by this exact name; renaming it server-side would drop every
        // live message without an error anywhere.
        await group.Received(1).SendCoreAsync(
            "messageReceived", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ThePayloadCarriesTheFullMessageWhenTheEventHasIt()
    {
        var clients = Substitute.For<IHubClients>();
        var group   = Substitute.For<IClientProxy>();
        var hub     = Substitute.For<IHubContext<ChatHub>>();
        hub.Clients.Returns(clients);
        clients.Group(Arg.Any<string>()).Returns(group);

        var createdAt = new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc);
        var parentId  = Guid.NewGuid();
        var sent = AnEvent(Guid.NewGuid()) with { Body = "full body", Format = 1, ParentId = parentId, CreatedAt = createdAt };

        object?[]? captured = null;
        await group.SendCoreAsync(Arg.Any<string>(), Arg.Do<object?[]>(a => captured = a), Arg.Any<CancellationToken>());

        await new MessageSentConsumer(hub).Consume(ConsumeContextFor(sent));

        var payload = captured!.ShouldHaveSingleItem()!;
        var type    = payload.GetType();
        type.GetProperty("body")!.GetValue(payload).ShouldBe("full body");
        type.GetProperty("format")!.GetValue(payload).ShouldBe(1);
        type.GetProperty("parentId")!.GetValue(payload).ShouldBe(parentId);
        type.GetProperty("createdAt")!.GetValue(payload).ShouldBe(createdAt);
    }

    [Fact]
    public async Task AnOldEventWithoutTheFullFieldsStillPushes()
    {
        // Events already in the outbox/queue when this deploys lack Body etc.; they must not fail the
        // consumer — the client falls back to the preview.
        var clients = Substitute.For<IHubClients>();
        var group   = Substitute.For<IClientProxy>();
        var hub     = Substitute.For<IHubContext<ChatHub>>();
        hub.Clients.Returns(clients);
        clients.Group(Arg.Any<string>()).Returns(group);

        await Should.NotThrowAsync(() => new MessageSentConsumer(hub).Consume(ConsumeContextFor(AnEvent(Guid.NewGuid()))));
        await group.Received(1).SendCoreAsync("messageReceived", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ThePayloadCarriesTheMessageDetails()
    {
        var clients = Substitute.For<IHubClients>();
        var group   = Substitute.For<IClientProxy>();
        var hub     = Substitute.For<IHubContext<ChatHub>>();

        hub.Clients.Returns(clients);
        clients.Group(Arg.Any<string>()).Returns(group);

        var channelId = Guid.NewGuid();
        var mention   = Guid.NewGuid();
        var sent      = AnEvent(channelId, [mention]);

        object?[]? captured = null;
        await group.SendCoreAsync(
            Arg.Any<string>(),
            Arg.Do<object?[]>(args => captured = args),
            Arg.Any<CancellationToken>());

        await new MessageSentConsumer(hub).Consume(ConsumeContextFor(sent));

        captured.ShouldNotBeNull();
        var payload = captured!.ShouldHaveSingleItem();

        // Read reflectively because the consumer builds an anonymous type. The client needs the message id
        // to de-duplicate against what it already rendered, and the channel id to route it.
        var type = payload!.GetType();
        type.GetProperty("messageId")!.GetValue(payload).ShouldBe(sent.MessageId);
        type.GetProperty("channelId")!.GetValue(payload).ShouldBe(channelId);
        type.GetProperty("authorId")!.GetValue(payload).ShouldBe(sent.AuthorId);
        type.GetProperty("preview")!.GetValue(payload).ShouldBe("preview text");
    }

    [Fact]
    public async Task TwoMessagesInDifferentChannelsGoToDifferentGroups()
    {
        var clients = Substitute.For<IHubClients>();
        var group   = Substitute.For<IClientProxy>();
        var hub     = Substitute.For<IHubContext<ChatHub>>();

        hub.Clients.Returns(clients);
        clients.Group(Arg.Any<string>()).Returns(group);

        var first  = Guid.NewGuid();
        var second = Guid.NewGuid();

        var consumer = new MessageSentConsumer(hub);
        await consumer.Consume(ConsumeContextFor(AnEvent(first)));
        await consumer.Consume(ConsumeContextFor(AnEvent(second)));

        // A consumer that cached or reused the group would cross-post one channel's messages into another,
        // which is a confidentiality problem for private channels, not just a display bug.
        clients.Received(1).Group($"channel:{first}");
        clients.Received(1).Group($"channel:{second}");
    }

    [Fact]
    public async Task TheConsumerIsWiredUpAndReceivesPublishedEvents()
    {
        // Goes through real MassTransit dispatch rather than calling Consume directly, which is what shows
        // the consumer is registered for this message type at all.
        var clients = Substitute.For<IHubClients>();
        var group   = Substitute.For<IClientProxy>();
        var hub     = Substitute.For<IHubContext<ChatHub>>();

        hub.Clients.Returns(clients);
        clients.Group(Arg.Any<string>()).Returns(group);

        await using var provider = new ServiceCollection()
            .AddSingleton(hub)
            .AddMassTransitTestHarness(bus => bus.AddConsumer<MessageSentConsumer>())
            .BuildServiceProvider(validateScopes: true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var channelId = Guid.NewGuid();
        await harness.Bus.Publish(AnEvent(channelId));

        (await harness.Consumed.Any<MessageSent>()).ShouldBeTrue();
        clients.Received().Group($"channel:{channelId}");

        await harness.Stop();
    }

    [Fact]
    public async Task ARedeliveredEventFansOutAgainRatherThanBeingSuppressed()
    {
        // Recorded rather than asserted as desirable. This consumer keeps no dedupe state, so a broker
        // redelivery pushes the same message to the group twice — unlike the Notification consumer, which
        // checks for an existing row first.
        //
        // That is defensible here: the client already needs to de-duplicate by message id, since a
        // reconnect can replay recent history. But it is worth knowing explicitly, because the
        // repository's rules call for idempotent consumers and this one meets that bar only by relying on
        // the client.
        var clients = Substitute.For<IHubClients>();
        var group   = Substitute.For<IClientProxy>();
        var hub     = Substitute.For<IHubContext<ChatHub>>();

        hub.Clients.Returns(clients);
        clients.Group(Arg.Any<string>()).Returns(group);

        var channelId = Guid.NewGuid();
        var sent      = AnEvent(channelId);

        var consumer = new MessageSentConsumer(hub);
        await consumer.Consume(ConsumeContextFor(sent));
        await consumer.Consume(ConsumeContextFor(sent));

        clients.Received(2).Group($"channel:{channelId}");
    }

    /// <summary>Builds a minimal consume context around one message.</summary>
    private static ConsumeContext<MessageSent> ConsumeContextFor(MessageSent message)
    {
        var context = Substitute.For<ConsumeContext<MessageSent>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }
}
