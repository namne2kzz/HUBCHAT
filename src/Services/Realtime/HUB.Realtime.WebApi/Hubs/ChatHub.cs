using System.Security.Claims;
using HUB.Realtime.WebApi.Channels;
using HUB.Realtime.WebApi.Presence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HUB.Realtime.WebApi.Hubs;

/// <summary>SignalR hub for realtime chat: channel groups, typing, and presence. Backed by a Redis backplane.</summary>
/// <param name="presence">Redis presence store (online/away tracking).</param>
/// <param name="channelAccess">Cached membership check guarding channel subscriptions.</param>
[Authorize]
public sealed class ChatHub(IPresenceStore presence, IChannelAccessService channelAccess) : Hub
{
    /// <summary>Group name for a channel's subscribers.</summary>
    public static string ChannelGroup(Guid channelId) => $"channel:{channelId}";

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        if (CurrentUserId() is { } uid)
        {
            var status = await presence.AddConnectionAsync(uid, Context.ConnectionId, Context.ConnectionAborted);
            if (status is { } s)
                await Clients.All.SendAsync("presenceChanged", new { userId = uid, status = s.ToString() });
        }
        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (CurrentUserId() is { } uid)
        {
            var becameOffline = await presence.RemoveConnectionAsync(uid, Context.ConnectionId, CancellationToken.None);
            if (becameOffline)
                await Clients.All.SendAsync("presenceChanged", new { userId = uid, status = "Offline" });
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Subscribes the caller's connection to a channel's realtime stream.</summary>
    /// <param name="channelId">Channel to join.</param>
    /// <remarks>
    /// Membership is verified against chat-service (cached) before the connection joins the group. Without
    /// this, knowing a channel id was enough to receive every message pushed to it. Throws
    /// <see cref="HubException"/> on refusal so the client sees the rejection instead of silently joining
    /// nothing.
    /// </remarks>
    /// <exception cref="HubException">Thrown when the caller is unauthenticated or not allowed to join.</exception>
    public async Task JoinChannel(Guid channelId)
    {
        if (CurrentUserId() is not { } userId)
            throw new HubException("Not authenticated.");

        if (!await channelAccess.CanJoinAsync(channelId, userId, Context.ConnectionAborted))
            throw new HubException("You are not allowed to join this channel.");

        await Groups.AddToGroupAsync(Context.ConnectionId, ChannelGroup(channelId));
    }

    /// <summary>Unsubscribes the caller's connection from a channel.</summary>
    /// <param name="channelId">Channel to leave.</param>
    public Task LeaveChannel(Guid channelId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ChannelGroup(channelId));

    /// <summary>Broadcasts that the caller started typing in a channel.</summary>
    /// <param name="channelId">Channel id.</param>
    public Task StartTyping(Guid channelId) =>
        Clients.OthersInGroup(ChannelGroup(channelId)).SendAsync("typingStarted", new { channelId, userId = CurrentUserId() });

    /// <summary>Broadcasts that the caller stopped typing in a channel.</summary>
    /// <param name="channelId">Channel id.</param>
    public Task StopTyping(Guid channelId) =>
        Clients.OthersInGroup(ChannelGroup(channelId)).SendAsync("typingStopped", new { channelId, userId = CurrentUserId() });

    /// <summary>Refreshes the caller's presence TTL. Client should call periodically (e.g. every 30s).</summary>
    public Task Heartbeat() =>
        CurrentUserId() is { } uid ? presence.HeartbeatAsync(uid, Context.ConnectionAborted) : Task.CompletedTask;

    /// <summary>Sets the caller's manual presence status and broadcasts it to everyone.</summary>
    /// <param name="status">Desired status (1 = Active/auto, 2 = Away, 3 = Do Not Disturb). Anything else is treated as Active.</param>
    public async Task SetStatus(int status)
    {
        if (CurrentUserId() is not { } uid) return;
        var desired   = Enum.IsDefined(typeof(PresenceStatus), status) && status != (int)PresenceStatus.Offline
            ? (PresenceStatus)status
            : PresenceStatus.Online;
        var effective = await presence.SetManualStatusAsync(uid, desired, Context.ConnectionAborted);
        await Clients.All.SendAsync("presenceChanged", new { userId = uid, status = effective.ToString() });
    }

    private Guid? CurrentUserId()
    {
        var raw = Context.User?.FindFirstValue("uid") ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
