---
name: opentelemetry
description: Use when setting up OpenTelemetry tracing/metrics/structured logging in .NET (Serilog, custom ActivitySource/metrics, OTLP or Azure Application Insights export) or deciding log levels.
---

# Skill: OpenTelemetry — Observability cho .NET

Traces, metrics, structured logs — export sang Jaeger, Grafana, hoặc Azure Application Insights.

## Usage
```
/opentelemetry [setup|traces|metrics|logs|azure] [context]
```

## Stack
```xml
<PackageReference Include="OpenTelemetry.Extensions.Hosting"        Version="1.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http"       Version="1.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.SqlClient"  Version="1.*" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.*" />
<!-- Azure Application Insights -->
<PackageReference Include="Azure.Monitor.OpenTelemetry.AspNetCore"   Version="1.*" />
<!-- Structured logging -->
<PackageReference Include="Serilog.AspNetCore"                       Version="8.*" />
<PackageReference Include="Serilog.Enrichers.Environment"            Version="2.*" />
<PackageReference Include="Serilog.Enrichers.Thread"                 Version="3.*" />
```

---

## Setup — WebApi/Program.cs

```csharp
// Traces
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: builder.Configuration["Otel:ServiceName"] ?? "YourApp.Api",
        serviceVersion: Assembly.GetExecutingAssembly().GetName().Version?.ToString()))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(opt =>
        {
            opt.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
            opt.RecordException = true;
        })
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation(opt => opt.SetDbStatementForText = true)
        .AddEntityFrameworkCoreInstrumentation(opt => opt.SetDbStatementForText = true)
        .AddSource("YourApp.*")             // custom ActivitySource
        .AddOtlpExporter())                 // Jaeger / Grafana Tempo / Aspire dashboard
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("YourApp.*")
        .AddOtlpExporter())
    .WithLogging(logs => logs
        .AddOtlpExporter());

// Azure Application Insights (thay OTLP nếu dùng Azure)
// builder.Services.AddOpenTelemetry().UseAzureMonitor();
```

---

## Structured Logging — Serilog

```csharp
// Program.cs
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(new JsonFormatter())       // JSON để log aggregator parse
    .WriteTo.OpenTelemetry());                  // forward logs qua OTel

// appsettings.json
// {
//   "Serilog": {
//     "MinimumLevel": {
//       "Default": "Information",
//       "Override": {
//         "Microsoft.EntityFrameworkCore": "Warning",
//         "Microsoft.AspNetCore": "Warning"
//       }
//     }
//   }
// }
```

---

## Custom ActivitySource — Traces trong Business Logic

```csharp
/// <summary>ActivitySource cho domain operations — tạo spans trong business code.</summary>
public static class AppActivity
{
    public static readonly ActivitySource Source = new("YourApp.Application");

    /// <summary>Bắt đầu 1 span cho operation, tự kết thúc khi dispose.</summary>
    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        => Source.StartActivity(name, kind);
}

// Trong handler
public async Task<Result<Guid>> Handle(CreateOrderCommand cmd, CancellationToken ct)
{
    using var activity = AppActivity.StartActivity("CreateOrder");
    activity?.SetTag("order.customerId", cmd.CustomerId);

    try
    {
        var order = Order.Create(cmd.CustomerId, cmd.Note);
        _repo.Add(order);
        await _uow.CommitAsync(ct);

        activity?.SetTag("order.id", order.Id);
        return Result.Success(order.Id);
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.RecordException(ex);
        throw;
    }
}
```

---

## Custom Metrics

```csharp
/// <summary>Application-level metrics — counters, histograms, gauges.</summary>
public sealed class AppMetrics
{
    private readonly Counter<long> _ordersCreated;
    private readonly Histogram<double> _orderProcessingMs;

    public AppMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("YourApp.Application");
        _ordersCreated     = meter.CreateCounter<long>("orders.created.total");
        _orderProcessingMs = meter.CreateHistogram<double>("orders.processing.ms");
    }

    /// <summary>Increment order created counter.</summary>
    public void IncrementOrdersCreated(string status)
        => _ordersCreated.Add(1, new TagList { { "status", status } });

    /// <summary>Record order processing duration in milliseconds.</summary>
    public void RecordProcessingTime(double ms, string operation)
        => _orderProcessingMs.Record(ms, new TagList { { "operation", operation } });
}

// Register: services.AddSingleton<AppMetrics>();
```

---

## Log Level Strategy

| Level | Khi nào dùng |
|-------|-------------|
| `LogError` | Exceptions ảnh hưởng user hoặc data |
| `LogWarning` | Recoverable issues: retry, fallback, slow query |
| `LogInformation` | Business events: order created, payment processed |
| `LogDebug` | Dev-only detail: SQL params, cache hits — tắt trên prod |
| `LogTrace` | Framework-level noise — không bật trên bất kỳ env nào |

**Không log:** password, token, PII, credit card, sensitive headers.

---

## Rules
- Luôn `RecordException = true` trên AspNetCore instrumentation
- Filter out `/health` và `/alive` khỏi traces — chúng không có giá trị
- Dùng `ActivitySource` tên theo namespace (`YourApp.Application`) để dễ filter
- Log JSON format trên production — human-readable chỉ cho local dev
- `SetDbStatementForText = true` chỉ trên non-production — tránh SQL với PII vào traces
- `AddMeter("YourApp.*")` wildcard để tự động pick up metrics từ sub-services

## Prompt Template
```
Setup OpenTelemetry cho [ProjectName] với:
- Export target: [Jaeger local | Azure Application Insights | Grafana OTLP]
- Custom spans cần trace: [list operations]
- Custom metrics cần track: [list events/durations]
- Structured logging: Serilog với [Console JSON | Application Insights]

Tạo:
1. Program.cs OTel setup (traces + metrics + logs)
2. AppActivity static class với ActivitySource
3. AppMetrics class với relevant counters/histograms
4. Log level config trong appsettings.json
```
