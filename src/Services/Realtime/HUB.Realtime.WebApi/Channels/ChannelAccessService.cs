using System.Net;
using System.Text.Json;
using HUB.Shared.Auth;
using Microsoft.Extensions.Caching.Distributed;

namespace HUB.Realtime.WebApi.Channels;

/// <summary>
/// Asks chat-service whether a user may join a channel, caching the answer in Redis so the check is cheap
/// enough to run on every join.
/// </summary>
/// <remarks>
/// <para>
/// Caching is what makes this check practical: without it every <c>JoinChannel</c> would cost an HTTP hop
/// plus a Postgres query, which is why the check was deferred (the <c>P1</c> note on the hub) rather than
/// written. A client reconnecting re-joins every open channel at once, so the uncached cost arrives in
/// bursts.
/// </para>
/// <para>
/// Fails closed. If chat-service is unreachable the answer is "no": a realtime subscription is not worth
/// handing out on a guess, and the client can retry.
/// </para>
/// </remarks>
/// <param name="http">Typed client pointed at chat-service's <c>/internal</c> API.</param>
/// <param name="cache">Distributed (Redis) cache shared across realtime instances.</param>
/// <param name="logger">Logger for upstream failures, which are denials the user will notice.</param>
public sealed class ChannelAccessService(
    HttpClient http,
    IDistributedCache cache,
    ILogger<ChannelAccessService> logger) : IChannelAccessService
{
    /// <summary>
    /// How long a granted answer is trusted. Short on purpose: revoking channel membership should take
    /// effect without waiting, and the only thing a stale "allow" buys an ex-member is continued realtime
    /// push until it expires.
    /// </summary>
    private static readonly TimeSpan AllowTtl = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How long a denial is trusted — shorter than <see cref="AllowTtl"/>, because a user who was just
    /// added to a channel should not be locked out of it, and because a cached denial is what a joining
    /// client retries against.
    /// </summary>
    private static readonly TimeSpan DenyTtl = TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<bool> CanJoinAsync(Guid channelId, Guid userId, CancellationToken ct)
    {
        var key = $"hub:rt:canjoin:{channelId}:{userId}";

        var cached = await cache.GetStringAsync(key, ct);
        if (cached is not null) return cached == "1";

        bool allowed;
        try
        {
            var response = await http.GetAsync($"internal/channels/{channelId}/can-join/{userId}", ct);

            if (response.StatusCode is HttpStatusCode.NotFound)
            {
                // Channel is gone — a definite "no", worth caching like any other denial.
                allowed = false;
            }
            else
            {
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<CanJoinResponse>(JsonOptions, ct);
                allowed = body?.Allowed ?? false;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Not cached: a transient outage must not pin a denial for the whole DenyTtl, and the next
            // attempt should reach chat-service again.
            logger.LogWarning(ex,
                "Channel access check failed for channel {ChannelId}, user {UserId}; denying this join.",
                channelId, userId);
            return false;
        }

        await cache.SetStringAsync(
            key,
            allowed ? "1" : "0",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = allowed ? AllowTtl : DenyTtl },
            ct);

        return allowed;
    }

    /// <summary>Shape of chat-service's can-join response.</summary>
    /// <param name="Allowed">True when the channel is public or the user is a member.</param>
    private sealed record CanJoinResponse(bool Allowed);

    /// <summary>Configures the typed client against chat-service's internal API.</summary>
    /// <param name="client">The client to configure.</param>
    /// <param name="baseUrl">Chat-service base URL.</param>
    /// <param name="internalToken">Shared service token for <c>/internal</c> routes.</param>
    public static void Configure(HttpClient client, string baseUrl, string internalToken)
    {
        client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
        client.DefaultRequestHeaders.Add(ServiceTokenDefaults.Header, internalToken);
        // A join must not hang on a slow upstream — the hub method is awaited by the client.
        client.Timeout = TimeSpan.FromSeconds(5);
    }
}
