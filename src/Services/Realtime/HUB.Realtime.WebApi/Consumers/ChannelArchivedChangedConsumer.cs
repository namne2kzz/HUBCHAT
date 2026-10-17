using HUB.Realtime.WebApi.Hubs;
using HUB.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace HUB.Realtime.WebApi.Consumers;

/// <summary>Consumes <see cref="ChannelArchivedChanged"/> and pushes the new state to the channel group.</summary>
/// <param name="hub">SignalR hub context (routes across servers via the Redis backplane).</param>
public sealed class ChannelArchivedChangedConsumer(IHubContext<ChatHub> hub) : IConsumer<ChannelArchivedChanged>
{
    /// <summary>Pushes <c>channelArchivedChanged</c> to the channel's subscribers.</summary>
    /// <param name="context">The consume context carrying the event.</param>
    /// <returns>A task that completes when the push has been handed to SignalR.</returns>
    public Task Consume(ConsumeContext<ChannelArchivedChanged> context)
    {
        var e = context.Message;
        return hub.Clients.Group(ChatHub.ChannelGroup(e.ChannelId)).SendAsync(
            "channelArchivedChanged",
            new { channelId = e.ChannelId, isArchived = e.IsArchived },
            context.CancellationToken);
    }
}
