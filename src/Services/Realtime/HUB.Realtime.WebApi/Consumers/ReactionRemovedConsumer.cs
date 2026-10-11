using HUB.Realtime.WebApi.Hubs;
using HUB.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace HUB.Realtime.WebApi.Consumers;

/// <summary>Consumes <see cref="ReactionRemoved"/> and pushes it to the message's channel group.</summary>
/// <remarks>Idempotent on the client: removing a reaction it no longer shows is a no-op.</remarks>
/// <param name="hub">SignalR hub context (routes across servers via the Redis backplane).</param>
public sealed class ReactionRemovedConsumer(IHubContext<ChatHub> hub) : IConsumer<ReactionRemoved>
{
    /// <summary>Pushes the removal to every connection subscribed to the channel.</summary>
    /// <param name="context">The consume context carrying the event.</param>
    /// <returns>A task that completes when the push has been handed to SignalR.</returns>
    public Task Consume(ConsumeContext<ReactionRemoved> context)
    {
        var e = context.Message;
        return hub.Clients.Group(ChatHub.ChannelGroup(e.ChannelId)).SendAsync(
            "reactionRemoved",
            new { messageId = e.MessageId, channelId = e.ChannelId, userId = e.UserId, emoji = e.Emoji },
            context.CancellationToken);
    }
}
