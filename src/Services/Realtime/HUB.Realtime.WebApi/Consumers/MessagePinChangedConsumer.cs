using HUB.Realtime.WebApi.Hubs;
using HUB.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace HUB.Realtime.WebApi.Consumers;

/// <summary>Consumes <see cref="MessagePinChanged"/> and pushes the new pin state to the channel group.</summary>
/// <param name="hub">SignalR hub context (routes across servers via the Redis backplane).</param>
public sealed class MessagePinChangedConsumer(IHubContext<ChatHub> hub) : IConsumer<MessagePinChanged>
{
    /// <summary>Pushes <c>messagePinChanged</c> to every connection subscribed to the channel.</summary>
    /// <param name="context">The consume context carrying the event.</param>
    /// <returns>A task that completes when the push has been handed to SignalR.</returns>
    public Task Consume(ConsumeContext<MessagePinChanged> context)
    {
        var e = context.Message;
        return hub.Clients.Group(ChatHub.ChannelGroup(e.ChannelId)).SendAsync(
            "messagePinChanged",
            new { messageId = e.MessageId, channelId = e.ChannelId, isPinned = e.IsPinned },
            context.CancellationToken);
    }
}
