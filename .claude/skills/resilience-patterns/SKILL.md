---
name: resilience-patterns
description: Use when adding retry, circuit breaker, hedging, or timeout resilience to HttpClient calls or EF Core access in .NET using Polly v8 / Microsoft.Extensions.Http.Resilience.
---

# Skill: Resilience Patterns — Polly v8

Retry, circuit breaker, hedging, timeout cho HttpClient và EF Core.

## Usage
```
/resilience-patterns [http|efcore|pipeline|setup] [context]
```

## Stack
```xml
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="8.*" />
<PackageReference Include="Polly.Extensions"                     Version="8.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.*" />
<!-- EF Core built-in retry: không cần Polly riêng -->
```

---

## HttpClient — Standard Resilience (Recommended)

```csharp
// Program.cs — dùng built-in Microsoft.Extensions.Http.Resilience
builder.Services.AddHttpClient<IPaymentClient, PaymentClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["PaymentApi:BaseUrl"]!);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddStandardResilienceHandler(opt =>
    {
        // Retry: 3 lần, exponential backoff với jitter
        opt.Retry.MaxRetryAttempts = 3;
        opt.Retry.Delay = TimeSpan.FromSeconds(1);
        opt.Retry.UseJitter = true;
        opt.Retry.ShouldHandle = args => args.Outcome switch
        {
            { Exception: HttpRequestException }         => PredicateResult.True(),
            { Result.StatusCode: HttpStatusCode.TooManyRequests } => PredicateResult.True(),
            { Result.StatusCode: >= HttpStatusCode.InternalServerError } => PredicateResult.True(),
            _ => PredicateResult.False()
        };

        // Circuit Breaker
        opt.CircuitBreaker.SamplingDuration         = TimeSpan.FromSeconds(30);
        opt.CircuitBreaker.MinimumThroughput        = 10;
        opt.CircuitBreaker.FailureRatio             = 0.5;
        opt.CircuitBreaker.BreakDuration            = TimeSpan.FromSeconds(15);

        // Total timeout bao gồm tất cả retry
        opt.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
    });
```

---

## HttpClient — Custom Polly Pipeline

```csharp
// Khi cần fine-grained control hơn StandardResilienceHandler
builder.Services.AddResiliencePipeline<string, HttpResponseMessage>("payment-api", builder =>
{
    builder
        .AddTimeout(TimeSpan.FromSeconds(5))
        .AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(500),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .HandleResult(r => (int)r.StatusCode >= 500)
        })
        .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 5,
            FailureRatio = 0.6,
            BreakDuration = TimeSpan.FromSeconds(30),
            OnOpened = args =>
            {
                _logger.LogWarning("Circuit opened for payment-api: {Reason}", args.Outcome.Exception?.Message);
                return ValueTask.CompletedTask;
            }
        });
});

// Inject và dùng trong service
public class PaymentClient(ResiliencePipelineProvider<string> pipelines, HttpClient http)
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline =
        pipelines.GetPipeline<HttpResponseMessage>("payment-api");

    /// <summary>Charge với full retry + circuit breaker.</summary>
    public async Task<PaymentResult> ChargeAsync(PaymentRequest req, CancellationToken ct)
    {
        var response = await _pipeline.ExecuteAsync(
            async token => await http.PostAsJsonAsync("/charge", req, token), ct);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaymentResult>(ct)
               ?? throw new InvalidOperationException("Empty payment response");
    }
}
```

---

## EF Core — Built-in SQL Server Retry

```csharp
// Program.cs
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(
            maxRetryCount:       5,
            maxRetryDelay:       TimeSpan.FromSeconds(30),
            errorNumbersToAdd:   null);    // null = dùng default transient error codes
    }));
```

---

## Hedging — Chạy song song khi request chậm

```csharp
// Dùng khi latency quan trọng hơn load: gửi request thứ 2 nếu thứ 1 chậm quá N ms
builder.Services.AddResiliencePipeline<string, SearchResult>("search-hedge", builder =>
{
    builder.AddHedging(new HedgingStrategyOptions<SearchResult>
    {
        MaxHedgedAttempts = 2,
        Delay = TimeSpan.FromMilliseconds(200),   // sau 200ms, gửi thêm 1 request song song
        ShouldHandle = new PredicateBuilder<SearchResult>()
            .Handle<HttpRequestException>()
    });
});
```

---

## Timeout — Per-request và Total

```csharp
// Per-request timeout (mỗi attempt)
builder.AddTimeout(new TimeoutStrategyOptions
{
    Timeout = TimeSpan.FromSeconds(5),
    OnTimeout = args =>
    {
        _logger.LogWarning("Request timed out after {Timeout}", args.Timeout);
        return ValueTask.CompletedTask;
    }
});
```

---

## Rules
- Luôn dùng `UseJitter = true` để tránh thundering herd khi nhiều services retry cùng lúc
- `StandardResilienceHandler` đủ cho 90% cases — custom pipeline khi cần logging hoặc hedging
- EF Core retry: dùng built-in `EnableRetryOnFailure`, không bọc thêm Polly bên ngoài
- Circuit breaker mở: log `LogWarning`, không `LogError` — đây là behavior có chủ đích
- Không retry với `POST` mà không có idempotency key — risk tạo duplicate
- `CancellationToken` phải pass qua tất cả pipeline steps

## Prompt Template
```
Thêm resilience cho [HttpClient/EF Core/Service] trong [ProjectName].

Scenario:
- Gọi đến: [external API / database]
- SLA yêu cầu: [timeout Xms, retry Y lần]
- Idempotent: [có/không] — quan trọng để biết có retry POST không

Tạo:
1. Resilience pipeline registration trong Program.cs
2. Cập nhật [ClientClass] để dùng pipeline
3. Log khi circuit mở / retry xảy ra
```
