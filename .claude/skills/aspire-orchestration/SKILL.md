---
name: aspire-orchestration
description: Use when setting up or extending .NET Aspire local orchestration (AppHost, ServiceDefaults, resource references to SQL Server/PostgreSQL/Redis, OTel + health checks) or writing Aspire-based integration tests.
---

# Skill: .NET Aspire — Local Orchestration

Local dev orchestration cho .NET microservices — thay thế docker-compose, bật OTel + health checks tự động.

## Usage
```
/aspire-orchestration [setup|resource|service|test] [context]
```

## Stack
```xml
<!-- AppHost.csproj -->
<PackageReference Include="Aspire.Hosting.AppHost"    Version="9.*" />
<PackageReference Include="Aspire.Hosting.SqlServer"  Version="9.*" />
<PackageReference Include="Aspire.Hosting.Redis"      Version="9.*" />
<PackageReference Include="Aspire.Hosting.PostgreSQL" Version="9.*" />

<!-- WebApi.csproj — ServiceDefaults -->
<PackageReference Include="Aspire.Microsoft.EntityFrameworkCore.SqlServer" Version="9.*" />
<PackageReference Include="Aspire.StackExchange.Redis"                     Version="9.*" />
```

---

## AppHost Setup

```csharp
// src/YourApp.AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure resources
var sqlServer = builder.AddSqlServer("sql")
    .WithDataVolume("sql-data")        // persist across restarts
    .WithLifetime(ContainerLifetime.Persistent);

var appDb = sqlServer.AddDatabase("AppDb");

var redis = builder.AddRedis("redis")
    .WithRedisInsight()                // Redis Insight UI
    .WithLifetime(ContainerLifetime.Persistent);

// Services
builder.AddProject<Projects.YourApp_WebApi>("api")
    .WithReference(appDb)
    .WithReference(redis)
    .WaitFor(appDb)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
```

---

## ServiceDefaults Extension

```csharp
// src/YourApp.ServiceDefaults/Extensions.cs

/// <summary>Registers Aspire service defaults: OTel, health checks, resilience, service discovery.</summary>
public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
{
    builder.ConfigureOpenTelemetry();

    builder.AddDefaultHealthChecks();

    builder.Services.AddServiceDiscovery();

    builder.Services.ConfigureHttpClientDefaults(http =>
    {
        http.AddStandardResilienceHandler();
        http.AddServiceDiscovery();
    });

    return builder;
}

/// <summary>Maps /health (deep) and /alive (shallow) endpoints.</summary>
public static WebApplication MapDefaultEndpoints(this WebApplication app)
{
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
    app.MapHealthChecks("/alive", new HealthCheckOptions
    {
        Predicate = r => r.Tags.Contains("live")
    });
    return app;
}
```

```csharp
// src/YourApp.WebApi/Program.cs
builder.AddServiceDefaults();           // OTel + health checks + resilience
builder.AddSqlServerDbContext<AppDbContext>("AppDb");   // from Aspire connection string
builder.AddRedisClient("redis");        // from Aspire connection string

// ...
app.MapDefaultEndpoints();             // /health + /alive
```

---

## Integration Testing với DistributedApplicationFixture

```csharp
/// <summary>Starts the full Aspire AppHost for end-to-end integration tests.</summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.YourApp_AppHost>();

        appHost.Services.ConfigureHttpClientDefaults(c => c.AddStandardResilienceHandler());

        _app = await appHost.BuildAsync();
        await _app.StartAsync();
    }

    /// <summary>Get a typed HTTP client for the named Aspire resource.</summary>
    public HttpClient CreateHttpClient(string resourceName)
        => _app!.CreateHttpClient(resourceName);

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }
}

[Collection("AppHost")]
public class OrdersEndpointTests(AppHostFixture fixture) : IClassFixture<AppHostFixture>
{
    [Fact]
    public async Task GetOrders_ReturnsOk()
    {
        var client = fixture.CreateHttpClient("api");
        var response = await client.GetAsync("/api/orders");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

---

## Rules
- AppHost là entry point local dev: `dotnet run --project src/YourApp.AppHost`
- Không hardcode connection strings — Aspire inject tự động qua named resources
- `WithDataVolume` + `Persistent` lifetime để data không mất khi restart container
- ServiceDefaults bật tự động: OTel traces/metrics, health checks, Polly resilience
- CI/CD: dùng Testcontainers thay AppHost — Aspire không phù hợp cho CI pipeline
- Dashboard: mở `http://localhost:18888` để xem traces, logs, resources

## Prompt Template
```
Setup .NET Aspire cho project [ProjectName].

Resources cần:
- [SQL Server / PostgreSQL] database tên [DbName]
- [Redis cache]
- [thêm services nếu có]

Services:
- WebApi project: [YourApp.WebApi]
- [Worker service nếu có]

Tạo:
1. AppHost/Program.cs với tất cả resources và references
2. ServiceDefaults extension với OTel + health checks
3. WebApi/Program.cs cập nhật dùng Aspire connection strings
```
