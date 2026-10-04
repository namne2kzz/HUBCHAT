using MassTransit;
using HUB.Realtime.WebApi.Channels;
using HUB.Realtime.WebApi.Consumers;
using HUB.Realtime.WebApi.Hubs;
using HUB.Realtime.WebApi.Presence;
using HUB.Shared.Auth;
using HUB.Shared.Messaging;
using HUB.Shared.Observability;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHubObservability("hub-realtime");
builder.Services.AddHubJwtAuth(builder.Configuration);

var redisConnection = builder.Configuration.GetValue<string>("Redis:Connection")
                      ?? throw new InvalidOperationException("Missing 'Redis:Connection'.");

// AbortOnConnectFail=false: Redis not being up yet at boot must not crash the service. Every connection
// built from these options keeps reconnecting in the background; /health/ready reports Unhealthy until then.
var redisOptions = ConfigurationOptions.Parse(redisConnection);
redisOptions.AbortOnConnectFail = false;

// Redis pub/sub channels are server-wide — database numbers do not separate them — so two environments
// running this hub on one Redis would receive each other's pushes (Clients.All presence included).
// The prefix must therefore be unique per environment, which is why it comes from config, not code.
// All instances of one environment must share it: changing it mid rolling-deploy splits the backplane.
var backplanePrefix = builder.Configuration.GetValue<string>("Redis:BackplanePrefix");
if (string.IsNullOrWhiteSpace(backplanePrefix))
    throw new InvalidOperationException("Missing 'Redis:BackplanePrefix'.");

// SignalR with Redis backplane so message fan-out reaches clients on any server instance.
builder.Services.AddSignalR().AddStackExchangeRedis(o =>
{
    o.Configuration = redisOptions.Clone();
    // Redis prepends the prefix verbatim; the separator keeps channel names readable in PUBSUB CHANNELS.
    o.Configuration.ChannelPrefix = RedisChannel.Literal(backplanePrefix.EndsWith(':') ? backplanePrefix : backplanePrefix + ":");
});

// Shared Redis multiplexer for presence tracking.
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));
builder.Services.AddSingleton<IPresenceStore, RedisPresenceStore>();

// ── Channel access check (chat-service + Redis cache) ─────────────────────────
// Guards JoinChannel: without it, knowing a channel id was enough to receive its realtime stream. The
// cache is what keeps the check affordable — a reconnecting client re-joins every open channel at once.
var chatBaseUrl = builder.Configuration.GetValue<string>("Chat:InternalBaseUrl")
                  ?? throw new InvalidOperationException("Missing 'Chat:InternalBaseUrl'.");
var internalToken = builder.Configuration.GetValue<string>("InternalApi:Token")
                    ?? throw new InvalidOperationException("Missing 'InternalApi:Token'.");

builder.Services.AddStackExchangeRedisCache(o => o.ConfigurationOptions = redisOptions.Clone());
builder.Services
    .AddHttpClient<IChannelAccessService, ChannelAccessService>(c =>
        ChannelAccessService.Configure(c, chatBaseUrl, internalToken))
    .AddStandardResilienceHandler(ChannelAccessService.ConfigureResilience); // Polly: retry + circuit breaker + timeout

// Consume Chat integration events from RabbitMQ and push them to SignalR groups / connections.
builder.Services.AddHubMessaging(builder.Configuration, bus =>
{
    bus.AddConsumer<MessageSentConsumer>();
    bus.AddConsumer<ReactionAddedConsumer>();
    // Revokes a removed member's live subscription (group + join cache) — see the consumer for why.
    bus.AddConsumer<ChannelMemberRemovedConsumer>();
});

// Reuses the presence multiplexer: a failing ping means presence and the backplane are both down.
builder.Services.AddHealthChecks()
    .AddRedis(sp => sp.GetRequiredService<IConnectionMultiplexer>(), name: "redis", tags: ["ready"]);

var app = builder.Build();

app.MapHubHealthChecks();
app.UseHubAuth();
app.MapHub<ChatHub>("/hubs/chat");
app.MapPresenceEndpoints();

app.Run();
