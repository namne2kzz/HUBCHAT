using HUB.DashboardGateway.Dashboard;
using HUB.DashboardGateway.Directory;
using HUB.DashboardGateway.Endpoints;
using HUB.Shared.Auth;
using HUB.Shared.Messaging;
using HUB.Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

// ── Observability + health ────────────────────────────────────────────────────
builder.Services.AddHubObservability("hub-dashboard-gateway");

// ── Auth (same shared DASHBOARD JWT secret) ───────────────────────────────────
builder.Services.AddHubJwtAuth(builder.Configuration);

// ── Options ───────────────────────────────────────────────────────────────────
var dashboardOptions = builder.Configuration.GetSection(DashboardOptions.SectionName).Get<DashboardOptions>()
                       ?? throw new InvalidOperationException("Missing 'Dashboard' configuration section.");
var internalToken = builder.Configuration.GetSection(InternalApiOptions.SectionName).Get<InternalApiOptions>()?.Token
                    ?? throw new InvalidOperationException("Missing 'InternalApi:Token'.");

// ── Typed client → DASHBOARD /internal (service-token + resilience) ───────────
builder.Services.AddHttpClient<IDashboardClient, DashboardClient>(client =>
    {
        client.BaseAddress = new Uri(dashboardOptions.InternalBaseUrl);
        client.DefaultRequestHeaders.Add(ServiceTokenDefaults.Header, internalToken);
    })
    .AddStandardResilienceHandler(); // Polly: retry + circuit breaker + timeout

// ── Redis read-through cache ──────────────────────────────────────────────────
var redisConnection = builder.Configuration.GetValue<string>("Redis:Connection")
                      ?? throw new InvalidOperationException("Missing 'Redis:Connection'.");
builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConnection);
builder.Services.AddScoped<IDirectoryService, DirectoryService>();

// ── Messaging (RabbitMQ) — publish-only for now, ready for future consumers ────
builder.Services.AddHubMessaging(builder.Configuration);

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddRedis(redisConnection, name: "redis", tags: ["ready"]);

var app = builder.Build();

app.MapHubHealthChecks();
app.UseHubAuth();
app.MapDirectoryEndpoints();

app.Run();

/// <summary>Exposed so integration tests can reference the entry-point assembly.</summary>
public partial class Program;
