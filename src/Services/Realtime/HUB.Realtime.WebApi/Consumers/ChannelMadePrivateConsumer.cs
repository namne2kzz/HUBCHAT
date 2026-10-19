using HUB.Realtime.WebApi.Channels;
using HUB.Realtime.WebApi.Hubs;
using HUB.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace HUB.Realtime.WebApi.Consumers;

/// <summary>
/// Consumes <see cref="ChannelMadePrivate"/> and drops every live subscriber who is not a member, so a
/// channel locked down to private stops pushing to the non-members who were watching it while it was public.
/// </summary>
/// <remarks>
/// A public channel admits anyone, and SignalR cannot enumerate a group, so the subscription index is the
/// only record of who is watching. For each non-member connection we: evict its cached join answer (so a
/// re-join is checked fresh against the now-private channel), remove it from the group, forget it in the
/// index, and tell it — the same revocation path as removing a member.
/// </remarks>
/// <param name="hub">SignalR hub context (group ops route across instances via the Redis backplane).</param>
/// <param name="subscriptions">Index of connections per channel.</param>
/// <param name="channelAccess">Join-check cache to evict per revoked user.</param>
/// <param name="logger">Logger for connections that could not be removed.</param>
public sealed class ChannelMadePrivateConsumer(
    IHubContext<ChatHub> hub,
    IChannelSubscriptions subscriptions,
    IChannelAccessService channelAccess,
    ILogger<ChannelMadePrivateConsumer> logger) : IConsumer<ChannelMadePrivate>
{
    private static readonly TimeSpan RemoveTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Revokes every subscriber of the channel who is not in the member list.</summary>
    /// <param name="context">The consume context carrying the event.</param>
    /// <returns>A task that completes when non-members have been revoked.</returns>
    public async Task Consume(ConsumeContext<ChannelMadePrivate> context)
    {
        var channelId = context.Message.ChannelId;
        var members   = context.Message.MemberIds.ToHashSet();
        var ct        = context.CancellationToken;

        var subscribers = await subscriptions.GetSubscribersAsync(channelId, ct);
        var group       = ChatHub.ChannelGroup(channelId);

        foreach (var (connectionId, userId) in subscribers)
        {
            if (members.Contains(userId)) continue; // a member keeps watching

            await channelAccess.InvalidateAsync(channelId, userId, ct);
            await subscriptions.RemoveAsync(channelId, connectionId, ct);
            await RemoveAsync(connectionId, group, ct);
            // nowPrivate tells the client this was a lock-down (not a plain removal), so it closes the view
            // rather than offering a "join" bar for a channel it can no longer see.
            await hub.Clients.Client(connectionId).SendAsync("channelAccessRevoked", new { channelId, nowPrivate = true }, ct);
        }
    }

    /// <summary>Removes one connection, never letting a stale id fail the others.</summary>
    private async Task RemoveAsync(string connectionId, string group, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RemoveTimeout);
        try
        {
            await hub.Groups.RemoveFromGroupAsync(connectionId, group, timeout.Token);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not remove connection {ConnectionId} from {Group}; likely stale.", connectionId, group);
        }
    }
}
