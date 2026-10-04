using HUB.Realtime.WebApi.Channels;
using HUB.Realtime.WebApi.Hubs;
using HUB.Realtime.WebApi.Presence;
using HUB.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace HUB.Realtime.WebApi.Consumers;

/// <summary>
/// Consumes <see cref="ChannelMemberRemoved"/> and revokes the user's live subscription to the channel.
/// </summary>
/// <remarks>
/// <para>
/// The membership check runs only when a connection joins the group. Without this consumer, a user
/// removed from a private channel kept receiving its messages until they reconnected — and the cached
/// "allow" let them back in for up to a minute even then.
/// </para>
/// <para>Order matters:</para>
/// <list type="number">
/// <item>Evict the cached join answer first, so a client that tries to re-join right away is checked fresh.</item>
/// <item>Remove every connection of the user from the group — the enforcement, works across instances via the backplane.</item>
/// <item>Tell those connections, so the UI can close the channel instead of silently going quiet.</item>
/// </list>
/// <para>Idempotent: evicting a missing key, removing a non-member connection and re-sending the notice are all harmless.</para>
/// </remarks>
/// <param name="hub">SignalR hub context (group operations route across instances via the Redis backplane).</param>
/// <param name="presence">Presence store — source of the user's connection ids.</param>
/// <param name="channelAccess">Join-check cache to evict.</param>
/// <param name="logger">Logger for connections that could not be removed.</param>
public sealed class ChannelMemberRemovedConsumer(
    IHubContext<ChatHub> hub,
    IPresenceStore presence,
    IChannelAccessService channelAccess,
    ILogger<ChannelMemberRemovedConsumer> logger) : IConsumer<ChannelMemberRemoved>
{
    /// <summary>
    /// Upper bound for one group removal. A connection id that died without a clean disconnect stays in the
    /// presence set until its TTL; removing it waits for an ack no instance will send.
    /// </summary>
    private static readonly TimeSpan RemoveTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Evicts the join cache, removes the user's connections from the channel group, and notifies them.</summary>
    /// <param name="context">The consume context carrying the event.</param>
    /// <returns>A task that completes when the subscription has been revoked.</returns>
    public async Task Consume(ConsumeContext<ChannelMemberRemoved> context)
    {
        var (channelId, userId) = (context.Message.ChannelId, context.Message.UserId);
        var ct = context.CancellationToken;

        await channelAccess.InvalidateAsync(channelId, userId, ct);

        var connections = await presence.GetConnectionsAsync(userId, ct);
        if (connections.Count == 0) return; // offline — the next join is checked fresh anyway

        var group = ChatHub.ChannelGroup(channelId);
        await Task.WhenAll(connections.Select(c => RemoveAsync(c, group, ct)));

        await hub.Clients.Clients(connections).SendAsync("channelAccessRevoked", new { channelId }, ct);
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
            logger.LogWarning(ex, "Could not remove connection {ConnectionId} from {Group}; likely a stale connection.", connectionId, group);
        }
    }
}
