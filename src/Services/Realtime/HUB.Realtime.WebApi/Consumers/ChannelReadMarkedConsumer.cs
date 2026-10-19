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
    /// <summary>
    /// Pushes <c>channelRead</c> to the reader's own connections (badge sync across their devices) and, for a
    /// DM, a <c>readReceipt</c> to the other participant so their client can show "Seen" (CF-15).
    /// </summary>
    /// <param name="context">The consume context carrying the event.</param>
    /// <returns>A task that completes when the pushes have been handed to SignalR.</returns>
    public async Task Consume(ConsumeContext<ChannelReadMarked> context)
    {
        var e  = context.Message;
        var ct = context.CancellationToken;

        var connections = await presence.GetConnectionsAsync(e.UserId, ct);
        if (connections.Count > 0)
            await hub.Clients.Clients(connections).SendAsync(
                "channelRead", new { channelId = e.ChannelId, readAt = e.ReadAt }, ct);

        // Read receipts are a DM courtesy; broadcasting them in a busy channel would be noise, so only DMs.
        // OthersInGroup excludes the reader's own connections — the receipt is for the people they are
        // reading, not themselves.
        if (!e.IsDirect) return;

        var group = ChatHub.ChannelGroup(e.ChannelId);
        var exclude = connections.ToArray();
        await hub.Clients.GroupExcept(group, exclude).SendAsync(
            "readReceipt", new { channelId = e.ChannelId, userId = e.UserId, readAt = e.ReadAt }, ct);
    }
}
