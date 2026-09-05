using HUB.Media.Application;
using HUB.Media.Infrastructure;
using HUB.Media.Infrastructure.Persistence;
using HUB.Media.WebApi.Common;
using HUB.Shared.Auth;
using HUB.Shared.Observability;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHubObservability("hub-media");
builder.Services.AddHubJwtAuth(builder.Configuration);
builder.Services.AddMediaApplication();
builder.Services.AddMediaInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<MediaDbContext>().Database.MigrateAsync();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapHubHealthChecks();
app.UseHubAuth();
app.MapControllers();
app.Run();
