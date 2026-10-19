using System.Security.Claims;
using HUB.Realtime.WebApi.Channels;
using HUB.Realtime.WebApi.Presence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HUB.Realtime.WebApi.Hubs;

/// <summary>SignalR hub for realtime chat: channel groups, typing, and presence. Backed by a Redis backplane.</summary>
/// <param name="presence">Redis presence store (online/away tracking).</param>
/// <param name="channelAccess">Cached membership check guarding channel subscriptions.</param>
/// <param name="subscriptions">Index of who is in each channel group, so non-members can be revoked on a visibility change.</param>
[Authorize]
public sealed class ChatHub(
    IPresenceStore presence,
    IChannelAccessService channelAccess,
    IChannelSubscriptions subscriptions) : Hub
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
        await subscriptions.RemoveConnectionAsync(Context.ConnectionId, CancellationToken.None);
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
        await subscriptions.AddAsync(channelId, Context.ConnectionId, userId, Context.ConnectionAborted);
    }

    /// <summary>Unsubscribes the caller's connection from a channel.</summary>
    /// <param name="channelId">Channel to leave.</param>
    public async Task LeaveChannel(Guid channelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ChannelGroup(channelId));
        await subscriptions.RemoveAsync(channelId, Context.ConnectionId, Context.ConnectionAborted);
    }

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

    /// <summary>Maximum length of a custom status note; longer text is truncated.</summary>
    private const int CustomStatusMaxLen = 100;

    /// <summary>
    /// Sets (or clears) the caller's custom status and broadcasts it to everyone (CF-19).
    /// Empty emoji+text clears. An expiry in the past also clears.
    /// </summary>
    /// <param name="emoji">A single emoji, or empty.</param>
    /// <param name="text">The status note; truncated to 100 chars.</param>
    /// <param name="expiresAtUnixMs">Auto-clear time in Unix ms, or null for "don't clear".</param>
    public async Task SetCustomStatus(string? emoji, string? text, long? expiresAtUnixMs)
    {
        if (CurrentUserId() is not { } uid) return;

        var trimmedText = (text ?? string.Empty).Trim();
        if (trimmedText.Length > CustomStatusMaxLen) trimmedText = trimmedText[..CustomStatusMaxLen];
        var expires = expiresAtUnixMs is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : (DateTimeOffset?)null;

        var stored = await presence.SetCustomStatusAsync(
            uid, new CustomStatus((emoji ?? string.Empty).Trim(), trimmedText, expires), Context.ConnectionAborted);

        await Clients.All.SendAsync("customStatusChanged", new
        {
            userId = uid,
            emoji = stored?.Emoji ?? string.Empty,
            text = stored?.Text ?? string.Empty,
            expiresAt = stored?.ExpiresAt?.ToString("o"),
        });
    }

    private Guid? CurrentUserId()
    {
        var raw = Context.User?.FindFirstValue("uid") ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
