using HUB.Notification.Application;
using HUB.Notification.Infrastructure;
using HUB.Notification.Infrastructure.Persistence;
using HUB.Notification.WebApi.Common;
using HUB.Shared.Auth;
using HUB.Shared.Observability;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHubObservability("hub-notification");
builder.Services.AddHubJwtAuth(builder.Configuration);
builder.Services.AddNotificationApplication();
builder.Services.AddNotificationInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.MigrateAsync();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapHubHealthChecks();
app.UseHubAuth();
app.MapControllers();
app.Run();
