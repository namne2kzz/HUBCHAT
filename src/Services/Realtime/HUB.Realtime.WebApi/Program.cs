using MassTransit;
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

// SignalR with Redis backplane so message fan-out reaches clients on any server instance.
builder.Services.AddSignalR().AddStackExchangeRedis(redisConnection);

// Shared Redis multiplexer for presence tracking.
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddSingleton<IPresenceStore, RedisPresenceStore>();

// Consume MessageSent from RabbitMQ and push to SignalR groups.
builder.Services.AddHubMessaging(builder.Configuration, bus => bus.AddConsumer<MessageSentConsumer>());

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHubHealthChecks();
app.UseHubAuth();
app.MapHub<ChatHub>("/hubs/chat");
app.MapPresenceEndpoints();

app.Run();
