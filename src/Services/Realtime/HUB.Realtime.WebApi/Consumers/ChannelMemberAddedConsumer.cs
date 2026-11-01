using HUB.Realtime.WebApi.Channels;
using HUB.Realtime.WebApi.Hubs;
using HUB.Realtime.WebApi.Presence;
using HUB.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace HUB.Realtime.WebApi.Consumers;

/// <summary>
/// Consumes <see cref="ChannelMemberAdded"/> and tells the user's open tabs that a new channel is theirs.
/// </summary>
/// <remarks>
/// <para>
/// The mirror of <see cref="ChannelMemberRemovedConsumer"/>. The sidebar is built from the channel list
/// fetched on load, so without this a user added to a channel — especially a private one they could not
/// previously see — had no way to learn of it until they reloaded the page.
/// </para>
/// <para>
/// The join-check answer is evicted first: a negative result may be cached from before the user was a
/// member, and it would otherwise keep them out of the group they have just been let into.
/// </para>
/// <para>
/// Only a notice is sent; the client fetches the channel itself. The group is joined when the client
/// opens the channel, which is also where the membership check belongs — adding connections to the
/// group here would subscribe tabs to a channel the user may never open.
/// </para>
/// <para>Idempotent: evicting a missing key and re-sending the notice are both harmless.</para>
/// </remarks>
/// <param name="hub">SignalR hub context.</param>
/// <param name="presence">Presence store — source of the user's connection ids.</param>
/// <param name="channelAccess">Join-check cache to evict.</param>
public sealed class ChannelMemberAddedConsumer(
    IHubContext<ChatHub> hub,
    IPresenceStore presence,
    IChannelAccessService channelAccess) : IConsumer<ChannelMemberAdded>
{
    /// <summary>Evicts the stale join answer and notifies the user's live connections.</summary>
    /// <param name="context">The consume context carrying the event.</param>
    /// <returns>A task that completes once the user's connections have been told.</returns>
    public async Task Consume(ConsumeContext<ChannelMemberAdded> context)
    {
        var (channelId, userId) = (context.Message.ChannelId, context.Message.UserId);
        var ct = context.CancellationToken;

        await channelAccess.InvalidateAsync(channelId, userId, ct);

        var connections = await presence.GetConnectionsAsync(userId, ct);
        if (connections.Count == 0) return; // offline — their next load fetches the channel anyway

        await hub.Clients.Clients(connections).SendAsync("channelAccessGranted", new { channelId }, ct);
    }
}
