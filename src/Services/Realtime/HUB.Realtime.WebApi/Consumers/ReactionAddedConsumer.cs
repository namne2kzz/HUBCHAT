using HUB.Realtime.WebApi.Hubs;
using HUB.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace HUB.Realtime.WebApi.Consumers;

/// <summary>Consumes <see cref="ReactionAdded"/> and pushes it to the message's channel group.</summary>
/// <remarks>
/// Not deduplicated server-side, like <see cref="MessageSentConsumer"/>: a redelivery pushes twice, and
/// the client ignores a reaction it already shows (same user + emoji).
/// </remarks>
/// <param name="hub">SignalR hub context (routes across servers via the Redis backplane).</param>
public sealed class ReactionAddedConsumer(IHubContext<ChatHub> hub) : IConsumer<ReactionAdded>
{
    /// <summary>Pushes the reaction to every connection subscribed to the channel.</summary>
    /// <param name="context">The consume context carrying the event.</param>
    /// <returns>A task that completes when the push has been handed to SignalR.</returns>
    public Task Consume(ConsumeContext<ReactionAdded> context)
    {
        var e = context.Message;
        return hub.Clients.Group(ChatHub.ChannelGroup(e.ChannelId)).SendAsync(
            "reactionAdded",
            new { messageId = e.MessageId, channelId = e.ChannelId, userId = e.UserId, emoji = e.Emoji },
            context.CancellationToken);
    }
}
