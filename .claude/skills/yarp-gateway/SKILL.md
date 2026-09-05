---
name: yarp-gateway
description: Use when configuring HUB's YARP reverse proxy — route/cluster config, JWT auth policy, rate limiting, WebSocket (SignalR) passthrough.
---

# Skill: YARP Gateway (HUB entry point)

1 entry point duy nhất: validate JWT, route theo path, rate-limit, proxy WebSocket cho SignalR.

## Route/Cluster (appsettings)
```jsonc
"ReverseProxy": {
  "Routes": {
    "chat-messages": { "ClusterId": "chat", "AuthorizationPolicy": "authenticated",
                        "Match": { "Path": "/api/v1/messages/{**catch-all}" } },
    "realtime-hub":  { "ClusterId": "realtime", "AuthorizationPolicy": "authenticated",
                        "Match": { "Path": "/hubs/{**catch-all}" } }
  },
  "Clusters": {
    "chat":     { "Destinations": { "primary": { "Address": "http://chat:8080/" } } },
    "realtime": { "Destinations": { "primary": { "Address": "http://realtime:8080/" } } }
  }
}
```

## Program
```csharp
builder.Services.AddHubJwtAuth(builder.Configuration);
builder.Services.AddAuthorizationBuilder().AddPolicy("authenticated", p => p.RequireAuthenticatedUser());
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
...
app.UseHubAuth(); app.UseRateLimiter(); app.MapReverseProxy();
```

## Rule
- Client chỉ biết gateway; không gọi thẳng service.
- WebSocket: YARP proxy tự động; đảm bảo JWT đọc từ `?access_token` (path `/hubs`).
- Rate-limit per-user (claim `uid`) / per-IP ở gateway.
- KHÔNG route `/internal/*` của DASHBOARD ra public (service-to-service only).

## Đừng
- ❌ Business logic trong gateway. ❌ Thiếu `AuthorizationPolicy` trên route nhạy cảm.
