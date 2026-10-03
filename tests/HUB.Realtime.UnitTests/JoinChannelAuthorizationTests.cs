using System.Security.Claims;
using HUB.Realtime.WebApi.Channels;
using HUB.Realtime.WebApi.Hubs;
using HUB.Realtime.WebApi.Presence;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Shouldly;
using Xunit;

namespace HUB.Realtime.UnitTests;

/// <summary>
/// Covers the membership guard on <c>ChatHub.JoinChannel</c>.
///
/// Before this guard existed, knowing a channel id was enough: the hub added the connection to the group
/// unconditionally, and every message published to that channel was then pushed to the caller. No test of
/// the Chat service would catch it, because Chat's own queries did check membership — the hole was only in
/// the realtime path.
///
/// What these tests pin is that the connection is added **only** when access is granted, and that a refusal
/// is visible to the client as an error rather than a silent no-op. A silent refusal would be worse than no
/// guard at all: the client would believe it is subscribed and show an empty, never-updating channel.
/// </summary>
public sealed class JoinChannelAuthorizationTests
{
    private static readonly Guid UserId    = Guid.NewGuid();
    private static readonly Guid ChannelId = Guid.NewGuid();

    /// <summary>Builds a hub wired to substitutes, with the caller authenticated as <see cref="UserId"/>.</summary>
    private static (ChatHub Hub, IGroupManager Groups, IChannelAccessService Access) BuildHub(bool authenticated = true)
    {
        var access = Substitute.For<IChannelAccessService>();
        var groups = Substitute.For<IGroupManager>();

        var claims = authenticated ? new[] { new Claim("uid", UserId.ToString()) } : [];
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("conn-1");
        context.User.Returns(new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null)));

        var hub = new ChatHub(Substitute.For<IPresenceStore>(), access)
        {
            Context = context,
            Groups  = groups,
        };

        return (hub, groups, access);
    }

    [Fact]
    public async Task JoinChannel_AddsTheConnection_WhenAccessIsGranted()
    {
        var (hub, groups, access) = BuildHub();
        access.CanJoinAsync(ChannelId, UserId, Arg.Any<CancellationToken>()).Returns(true);

        await hub.JoinChannel(ChannelId);

        await groups.Received(1).AddToGroupAsync("conn-1", ChatHub.ChannelGroup(ChannelId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinChannel_DoesNotAddTheConnection_WhenAccessIsRefused()
    {
        var (hub, groups, access) = BuildHub();
        access.CanJoinAsync(ChannelId, UserId, Arg.Any<CancellationToken>()).Returns(false);

        await Should.ThrowAsync<HubException>(() => hub.JoinChannel(ChannelId));

        // The important half: refusing must not leave the connection subscribed anyway.
        await groups.DidNotReceive().AddToGroupAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinChannel_ChecksTheCallerIdentity_NotAnIdFromTheClient()
    {
        var (hub, _, access) = BuildHub();
        access.CanJoinAsync(ChannelId, UserId, Arg.Any<CancellationToken>()).Returns(true);

        await hub.JoinChannel(ChannelId);

        // The user id comes from the JWT claim, never from a hub argument — otherwise a client could ask
        // "may user X join?" and join as itself.
        await access.Received(1).CanJoinAsync(ChannelId, UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinChannel_RejectsAnUnauthenticatedCaller_WithoutAskingChatService()
    {
        var (hub, groups, access) = BuildHub(authenticated: false);

        await Should.ThrowAsync<HubException>(() => hub.JoinChannel(ChannelId));

        await access.DidNotReceive().CanJoinAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await groups.DidNotReceive().AddToGroupAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveChannel_StaysUnguarded_SoAClientCanAlwaysUnsubscribe()
    {
        var (hub, groups, access) = BuildHub();

        await hub.LeaveChannel(ChannelId);

        // Leaving needs no permission: a user who lost access must still be able to drop the group, and
        // gating it would let a revoked membership trap a connection in a group it may no longer read.
        await groups.Received(1).RemoveFromGroupAsync("conn-1", ChatHub.ChannelGroup(ChannelId), Arg.Any<CancellationToken>());
        await access.DidNotReceive().CanJoinAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
