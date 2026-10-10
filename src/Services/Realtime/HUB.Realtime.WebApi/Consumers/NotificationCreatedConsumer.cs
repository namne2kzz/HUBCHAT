using HUB.Realtime.WebApi.Hubs;
using HUB.Realtime.WebApi.Presence;
using HUB.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace HUB.Realtime.WebApi.Consumers;

/// <summary>Consumes <see cref="NotificationCreated"/> and pushes it to every open session of the recipient.</summary>
/// <remarks>
/// <para>
/// Addressed by connection id from the presence store — the same lookup the channel revoke uses — so it
/// reaches every tab and device on any realtime instance (via the backplane) without relying on SignalR's
/// user-id claim mapping.
/// </para>
/// <para>
/// Offline recipient: nothing to push; the notification is already stored and appears on the next list.
/// Not deduplicated here — the client ignores a notification id it already has.
/// </para>
/// </remarks>
/// <param name="hub">SignalR hub context.</param>
/// <param name="presence">Presence store — source of the recipient's connection ids.</param>
public sealed class NotificationCreatedConsumer(IHubContext<ChatHub> hub, IPresenceStore presence) : IConsumer<NotificationCreated>
{
    /// <summary>Pushes the notification (in the client's NotificationDto shape) to the recipient's connections.</summary>
    /// <param name="context">The consume context carrying the event.</param>
    /// <returns>A task that completes when the push has been handed to SignalR.</returns>
    public async Task Consume(ConsumeContext<NotificationCreated> context)
    {
        var e  = context.Message;
        var ct = context.CancellationToken;

        var connections = await presence.GetConnectionsAsync(e.UserId, ct);
        if (connections.Count == 0) return;

        await hub.Clients.Clients(connections).SendAsync(
            "notificationReceived",
            new
            {
                notification = new
                {
                    id        = e.NotificationId,
                    type      = e.Type,
                    sourceId  = e.SourceId,
                    channelId = e.ChannelId,
                    byUserId  = e.ByUserId,
                    preview   = e.Preview,
                    isRead    = false,
                    createdAt = e.CreatedAt,
                },
            },
            ct);
    }
}
