using HUB.Realtime.WebApi.Hubs;
using HUB.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace HUB.Realtime.WebApi.Consumers;

/// <summary>Consumes <see cref="MessageSent"/> from RabbitMQ and fans it out to the channel's SignalR group.</summary>
/// <param name="hub">SignalR hub context (routes across servers via the Redis backplane).</param>
public sealed class MessageSentConsumer(IHubContext<ChatHub> hub) : IConsumer<MessageSent>
{
    /// <summary>Pushes the new message to every connection subscribed to the channel.</summary>
    /// <param name="context">The consume context.</param>
    public async Task Consume(ConsumeContext<MessageSent> context)
    {
        var m = context.Message;
        await hub.Clients.Group(ChatHub.ChannelGroup(m.ChannelId)).SendAsync(
            "messageReceived",
            new
            {
                messageId = m.MessageId,
                channelId = m.ChannelId,
                authorId  = m.AuthorId,
                preview   = m.Preview,
                mentions  = m.MentionedUserIds,
                sentAt    = m.OccurredAtUtc,
            },
            context.CancellationToken);
    }
}
