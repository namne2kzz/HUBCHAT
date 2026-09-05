using HUB.Shared.Auth;
using HUB.Shared.Observability;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Observability ────────────────────────────────────────────────────────────
builder.Services.AddHubObservability("hub-gateway");
builder.Services.AddHealthChecks();

// ── Auth (validate DASHBOARD-issued JWT with the shared secret) ───────────────
builder.Services.AddHubJwtAuth(builder.Configuration);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser());

// ── Per-user rate limiting (spam protection at the edge) ──────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var key = context.User.FindFirst("uid")?.Value
                  ?? context.Connection.RemoteIpAddress?.ToString()
                  ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window      = TimeSpan.FromSeconds(10),
            QueueLimit  = 0,
        });
    });
});

// ── YARP reverse proxy (routes/clusters from config) ──────────────────────────
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapHubHealthChecks();
app.UseHubAuth();
app.UseRateLimiter();

// All proxied routes require an authenticated user (set via route AuthorizationPolicy = "authenticated").
app.MapReverseProxy();

app.Run();
