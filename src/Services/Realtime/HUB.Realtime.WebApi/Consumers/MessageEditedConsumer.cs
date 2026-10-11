using HUB.Realtime.WebApi.Hubs;
using HUB.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace HUB.Realtime.WebApi.Consumers;

/// <summary>Consumes <see cref="MessageEdited"/> and pushes the new body to the channel group.</summary>
/// <param name="hub">SignalR hub context (routes across servers via the Redis backplane).</param>
public sealed class MessageEditedConsumer(IHubContext<ChatHub> hub) : IConsumer<MessageEdited>
{
    /// <summary>Pushes <c>messageEdited</c> to every connection subscribed to the channel.</summary>
    /// <param name="context">The consume context carrying the event.</param>
    /// <returns>A task that completes when the push has been handed to SignalR.</returns>
    public Task Consume(ConsumeContext<MessageEdited> context)
    {
        var e = context.Message;
        return hub.Clients.Group(ChatHub.ChannelGroup(e.ChannelId)).SendAsync(
            "messageEdited",
            new { messageId = e.MessageId, channelId = e.ChannelId, newBody = e.Body, editedAt = e.EditedAt },
            context.CancellationToken);
    }
}
