using HUB.Realtime.WebApi.Hubs;
using HUB.Realtime.WebApi.Presence;
using HUB.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace HUB.Realtime.WebApi.Consumers;

/// <summary>
/// Consumes <see cref="ChannelReadMarked"/> and tells the reader's own sessions, so reading a channel on one
/// device clears its unread badge on the others.
/// </summary>
/// <param name="hub">SignalR hub context.</param>
/// <param name="presence">Presence store — source of the reader's connection ids.</param>
public sealed class ChannelReadMarkedConsumer(IHubContext<ChatHub> hub, IPresenceStore presence) : IConsumer<ChannelReadMarked>
{
    /// <summary>Pushes <c>channelRead</c> to every connection of the reader.</summary>
    /// <param name="context">The consume context carrying the event.</param>
    /// <returns>A task that completes when the push has been handed to SignalR.</returns>
    public async Task Consume(ConsumeContext<ChannelReadMarked> context)
    {
        var e = context.Message;
        var connections = await presence.GetConnectionsAsync(e.UserId, context.CancellationToken);
        if (connections.Count == 0) return;

        await hub.Clients.Clients(connections).SendAsync(
            "channelRead", new { channelId = e.ChannelId, readAt = e.ReadAt }, context.CancellationToken);
    }
}
