using System.Diagnostics;
using System.Net;
using System.Text;
using HUB.Realtime.WebApi.Channels;
using HUB.TestKit.Fakes;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace HUB.Realtime.UnitTests;

/// <summary>
/// Covers <c>ChannelAccessService</c> — the cached membership check that makes guarding
/// <c>JoinChannel</c> affordable.
///
/// Caching is the reason this check exists at all: a reconnecting client re-joins every open channel at
/// once, so an uncached check would mean a burst of HTTP hops and Postgres queries on every reconnect.
/// That cost is why the guard was deferred rather than written.
///
/// Two behaviours carry the risk. The answer must come from cache on repeat joins, or the guard is too
/// expensive to keep. And an unreachable chat-service must deny rather than allow — a realtime
/// subscription handed out on a guess is the exact hole the guard was added to close.
/// </summary>
public sealed class ChannelAccessServiceTests
{
    private static readonly Guid ChannelId = Guid.NewGuid();
    private static readonly Guid UserId    = Guid.NewGuid();
    private static readonly CancellationToken Ct = CancellationToken.None;

    /// <summary>Counts requests so a test can assert whether an answer came from cache.</summary>
    private sealed class CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(bool allowed) => new(HttpStatusCode.OK)
    {
        Content = new StringContent($"{{\"allowed\":{(allowed ? "true" : "false")}}}", Encoding.UTF8, "application/json"),
    };

    private static (ChannelAccessService Service, CountingHandler Handler) Build(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new CountingHandler(respond);
        var client  = new HttpClient(handler) { BaseAddress = new Uri("http://chat/") };
        var service = new ChannelAccessService(
            client, new FakeDistributedCache(), NullLogger<ChannelAccessService>.Instance);
        return (service, handler);
    }

    [Fact]
    public async Task CanJoin_ReturnsTheUpstreamAnswer()
    {
        var (service, _) = Build(_ => Json(allowed: true));

        (await service.CanJoinAsync(ChannelId, UserId, Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task CanJoin_AsksChatServiceOnce_ThenServesFromCache()
    {
        var (service, handler) = Build(_ => Json(allowed: true));

        await service.CanJoinAsync(ChannelId, UserId, Ct);
        await service.CanJoinAsync(ChannelId, UserId, Ct);

        handler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task CanJoin_CachesADenialToo_SoARetryLoopCannotHammerChatService()
    {
        var (service, handler) = Build(_ => Json(allowed: false));

        (await service.CanJoinAsync(ChannelId, UserId, Ct)).ShouldBeFalse();
        (await service.CanJoinAsync(ChannelId, UserId, Ct)).ShouldBeFalse();

        handler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task CanJoin_KeysPerChannelAndUser()
    {
        var (service, handler) = Build(_ => Json(allowed: true));

        await service.CanJoinAsync(ChannelId, UserId, Ct);
        await service.CanJoinAsync(ChannelId, Guid.NewGuid(), Ct);   // different user, same channel
        await service.CanJoinAsync(Guid.NewGuid(), UserId, Ct);      // same user, different channel

        // A key missing either half would let one user's grant leak to another, or one channel's to the next.
        handler.Calls.ShouldBe(3);
    }

    [Fact]
    public async Task CanJoin_DeniesAMissingChannel()
    {
        var (service, _) = Build(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        (await service.CanJoinAsync(ChannelId, UserId, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task CanJoin_FailsClosed_WhenChatServiceIsUnreachable()
    {
        var (service, _) = Build(_ => throw new HttpRequestException("connection refused"));

        // Allowing on failure would hand out a realtime subscription on a guess — the hole this guard
        // closes. Denying is recoverable; the client retries.
        (await service.CanJoinAsync(ChannelId, UserId, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task CanJoin_DoesNotCacheAnUpstreamFailure()
    {
        var failing = true;
        var (service, handler) = Build(_ => failing
            ? throw new HttpRequestException("connection refused")
            : Json(allowed: true));

        (await service.CanJoinAsync(ChannelId, UserId, Ct)).ShouldBeFalse();

        failing = false;
        (await service.CanJoinAsync(ChannelId, UserId, Ct)).ShouldBeTrue();

        // A transient outage must not pin a denial for the whole deny-TTL, so the second attempt has to
        // reach chat-service again rather than read a cached "no".
        handler.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task CanJoin_DeniesOnAnUnexpectedUpstreamStatus()
    {
        var (service, _) = Build(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        (await service.CanJoinAsync(ChannelId, UserId, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task CanJoin_CallsTheExpectedUpstreamRoute()
    {
        string? path = null;
        var (service, _) = Build(req =>
        {
            path = req.RequestUri!.AbsolutePath;
            return Json(allowed: true);
        });

        await service.CanJoinAsync(ChannelId, UserId, Ct);

        path.ShouldBe($"/internal/channels/{ChannelId}/can-join/{UserId}");
    }

    /// <summary>Upstream that never answers until the request is cancelled.</summary>
    private sealed class HangingHandler : HttpMessageHandler
    {
        public int Calls;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            await Task.Delay(Timeout.Infinite, ct);
            throw new UnreachableException();
        }
    }

    [Fact]
    public async Task CanJoin_DeniesWithinTheTimeoutBudget_WhenChatServiceHangs()
    {
        // Goes through the production resilience pipeline (ConfigureResilience), not a bare HttpClient: the
        // timeout is Polly's now, and it surfaces as TimeoutRejectedException rather than
        // TaskCanceledException — which the old catch did not cover, so a hang threw out of the hub method.
        var hanging = new HangingHandler();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IDistributedCache, FakeDistributedCache>();
        services
            .AddHttpClient<IChannelAccessService, ChannelAccessService>(c => ChannelAccessService.Configure(c, "http://chat/", "token"))
            .ConfigurePrimaryHttpMessageHandler(() => hanging)
            .AddStandardResilienceHandler(ChannelAccessService.ConfigureResilience);

        // Resolving also runs the resilience options validation — a mis-tuned ConfigureResilience fails here.
        await using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IChannelAccessService>();

        var clock   = Stopwatch.StartNew();
        var allowed = await service.CanJoinAsync(ChannelId, UserId, Ct);
        clock.Stop();

        allowed.ShouldBeFalse();
        hanging.Calls.ShouldBe(2); // first attempt timed out, one retry
        clock.Elapsed.ShouldBeLessThan(ChannelAccessService.TotalTimeout + TimeSpan.FromSeconds(1));
    }
}
