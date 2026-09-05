using HUB.Chat.Application;
using HUB.Chat.Infrastructure;
using HUB.Chat.Infrastructure.Persistence;
using HUB.Chat.WebApi.Common;
using HUB.Shared.Auth;
using HUB.Shared.Observability;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHubObservability("hub-chat");
builder.Services.AddHubJwtAuth(builder.Configuration);

builder.Services.AddChatApplication();
builder.Services.AddChatInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Apply migrations on startup (dev/self-host convenience). Requires migrations to exist.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
// Must run before UseHubAuth so /internal/* is blocked without touching JWT.
app.UseMiddleware<HUB.Chat.WebApi.Middleware.InternalApiKeyMiddleware>();
app.MapHubHealthChecks();
app.UseHubAuth();
app.MapControllers();

app.Run();
