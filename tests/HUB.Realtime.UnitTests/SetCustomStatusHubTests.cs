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
/// Covers the hub side of custom status (CF-19): it hands the store a trimmed, length-capped note and
/// announces the stored result to everyone — so a client that didn't make the change still sees it.
/// </summary>
public sealed class SetCustomStatusHubTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static (ChatHub Hub, IPresenceStore Store, IClientProxy All) BuildHub(bool authenticated = true)
    {
        var store  = Substitute.For<IPresenceStore>();
        var all    = Substitute.For<IClientProxy>();
        var clients = Substitute.For<IHubCallerClients>();
        clients.All.Returns(all);

        var claims  = authenticated ? new[] { new Claim("uid", UserId.ToString()) } : [];
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("conn-1");
        context.User.Returns(new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null)));

        var hub = new ChatHub(store, Substitute.For<IChannelAccessService>(), Substitute.For<IChannelSubscriptions>())
        {
            Context = context,
            Clients = clients,
        };

        return (hub, store, all);
    }

    [Fact]
    public async Task ItTruncatesALongNoteBeforeStoring()
    {
        var (hub, store, _) = BuildHub();
        store.SetCustomStatusAsync(Arg.Any<Guid>(), Arg.Any<CustomStatus>(), Arg.Any<CancellationToken>())
            .Returns(ci => (CustomStatus?)ci.Arg<CustomStatus>());

        await hub.SetCustomStatus("📅", new string('x', 250), null);

        await store.Received(1).SetCustomStatusAsync(
            UserId,
            Arg.Is<CustomStatus>(s => s.Text.Length == 100 && s.Emoji == "📅"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItBroadcastsTheStoredStatusToEveryone()
    {
        var (hub, store, all) = BuildHub();
        store.SetCustomStatusAsync(Arg.Any<Guid>(), Arg.Any<CustomStatus>(), Arg.Any<CancellationToken>())
            .Returns(new CustomStatus("🍔", "Lunch", null));

        await hub.SetCustomStatus("🍔", "Lunch", null);

        await all.Received(1).SendCoreAsync("customStatusChanged", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItDoesNothingForAnUnauthenticatedCaller()
    {
        var (hub, store, _) = BuildHub(authenticated: false);

        await hub.SetCustomStatus("🍔", "Lunch", null);

        await store.DidNotReceive().SetCustomStatusAsync(
            Arg.Any<Guid>(), Arg.Any<CustomStatus>(), Arg.Any<CancellationToken>());
    }
}
