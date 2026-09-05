---
name: api-versioning
description: Use when adding or reviewing API versioning (URL segment, header, query string) with Asp.Versioning in a .NET WebApi — setup, versioned controllers, deprecation headers, Swagger per-version docs.
---

# Skill: API Versioning — Asp.Versioning

URL segment, header, và query string versioning với proper deprecation.

## Usage
```
/api-versioning [setup|controller|deprecate|swagger] [context]
```

## Stack
```xml
<PackageReference Include="Asp.Versioning.Mvc"               Version="8.*" />
<PackageReference Include="Asp.Versioning.Mvc.ApiExplorer"   Version="8.*" />
```

---

## Setup — Program.cs

```csharp
builder.Services.AddApiVersioning(opt =>
{
    // Trả về version headers trong response
    opt.ReportApiVersions = true;

    // Default version khi client không chỉ định
    opt.DefaultApiVersion = new ApiVersion(1, 0);
    opt.AssumeDefaultVersionWhenUnspecified = true;

    // Đọc version từ: URL segment + header + query string (ưu tiên theo thứ tự)
    opt.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),                       // /api/v1/orders
        new HeaderApiVersionReader("X-Api-Version"),           // X-Api-Version: 1.0
        new QueryStringApiVersionReader("api-version"));       // ?api-version=1.0
})
.AddApiExplorer(opt =>
{
    // Format version trong route template
    opt.GroupNameFormat           = "'v'VVV";     // "v1", "v1.1", "v2"
    opt.SubstituteApiVersionInUrl = true;
});
```

---

## Controller — Versioned Endpoints

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/orders")]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
public class OrdersController(ISender mediator) : ControllerBase
{
    /// <summary>Get all orders — v1 returns basic list.</summary>
    [HttpGet]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryDto>>> GetOrders(CancellationToken ct)
    {
        var result = await mediator.Send(new GetOrdersQuery(), ct);
        return Ok(result);
    }

    /// <summary>Get all orders — v2 adds pagination and filters.</summary>
    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<PagedResult<OrderSummaryDto>>> GetOrdersPaged(
        [FromQuery] GetOrdersPagedQuery query, CancellationToken ct)
    {
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>Create order — shared across v1 and v2.</summary>
    [HttpPost]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<Guid>> Create(
        CreateOrderRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(request.ToCommand(), ct);
        return CreatedAtAction(nameof(GetById), new { id, version = "1" }, id);
    }
}
```

---

## Deprecation — Mark cũ và thông báo

```csharp
[ApiVersion("1.0", Deprecated = true)]   // v1 vẫn work nhưng báo deprecated
[ApiVersion("2.0")]
public class OrdersController : ControllerBase { }
```

Response headers khi gọi v1:
```
api-deprecated-versions: 1.0
api-supported-versions: 2.0
Sunset: Sat, 01 Jan 2027 00:00:00 GMT   // optional sunset date
```

```csharp
// Thêm Sunset header vào deprecated versions
app.Use(async (ctx, next) =>
{
    await next();
    var feature = ctx.Features.Get<IApiVersioningFeature>();
    if (feature?.RequestedApiVersion == new ApiVersion(1, 0))
        ctx.Response.Headers["Sunset"] = "Sat, 01 Jan 2027 00:00:00 GMT";
});
```

---

## Swagger — Separate Docs per Version

```csharp
// Program.cs
builder.Services.AddSwaggerGen(opt =>
{
    // Tự động lấy danh sách versions từ ApiExplorer
    var provider = builder.Services.BuildServiceProvider()
        .GetRequiredService<IApiVersionDescriptionProvider>();

    foreach (var desc in provider.ApiVersionDescriptions)
    {
        opt.SwaggerDoc(desc.GroupName, new OpenApiInfo
        {
            Title   = $"YourApp API {desc.GroupName}",
            Version = desc.GroupName,
            Description = desc.IsDeprecated
                ? "**DEPRECATED** — Migrate to latest version."
                : string.Empty
        });
    }
});

// Middleware
app.UseSwagger();
app.UseSwaggerUI(opt =>
{
    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
    foreach (var desc in provider.ApiVersionDescriptions.Reverse())
        opt.SwaggerEndpoint($"/swagger/{desc.GroupName}/swagger.json",
            $"v{desc.ApiVersion}{(desc.IsDeprecated ? " [DEPRECATED]" : "")}");
});
```

---

## Breaking vs Non-Breaking Changes

| Thay đổi | Cần version mới? |
|----------|-----------------|
| Thêm field vào response | Không |
| Xóa field khỏi response | **Có** |
| Đổi kiểu dữ liệu của field | **Có** |
| Thêm optional request param | Không |
| Xóa / đổi tên request param | **Có** |
| Thay đổi business logic / behavior | **Có** |
| Thêm endpoint mới | Không |

---

## Rules
- Versioning trong URL segment (`/v1/`) — dễ cache, dễ route, dễ log
- Header versioning chỉ dùng thêm khi client không control URL (e.g. webhooks)
- `Deprecated = true` ít nhất 3 tháng trước khi remove hoàn toàn
- Không xóa version cũ mà không có migration guide cho clients
- `ReportApiVersions = true` để clients biết version nào supported
- Minor changes (1.0 → 1.1) không breaking; major changes (1.x → 2.0) breaking

## Prompt Template
```
Thêm API versioning cho [ProjectName].

Versioning strategy: [URL segment | header | query string]
Versions hiện tại cần support: [v1, v2]
Endpoints cần tách theo version:
- [EndpointName]: v1 [mô tả], v2 [mô tả thay đổi]

Tạo:
1. Program.cs setup với ApiVersionReader
2. Controller với [MapToApiVersion] attributes
3. Swagger config với separate docs per version
4. Deprecated marking cho version cũ (nếu có)
```
