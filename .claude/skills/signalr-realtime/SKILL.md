---
name: signalr-realtime
description: Use when building realtime push in HUB — SignalR hubs, Redis backplane, JWT over WebSocket, fan-out from RabbitMQ consumers, typing/presence.
---

# Skill: SignalR Realtime (+ Redis backplane)

realtime-service là **edge** đẩy message/typing/presence tới client. Business (ghi DB, publish event) nằm ở chat-service; realtime **chỉ fan-out**.

## Backplane (scale nhiều instance)
```csharp
builder.Services.AddSignalR().AddStackExchangeRedis(redisConnection);
```
`Clients.Group(...)` sẽ tới client dù đang giữ connection ở pod nào.

## JWT qua WebSocket
Client truyền `?access_token=...`; `HUB.Shared.Auth` đã đọc token cho path `/hubs/*`:
```csharp
opt.Events = new JwtBearerEvents { OnMessageReceived = ctx => {
    var t = ctx.Request.Query["access_token"];
    if (!string.IsNullOrEmpty(t) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs")) ctx.Token = t;
    return Task.CompletedTask; } };
```

## Hub
```csharp
[Authorize]
public sealed class ChatHub : Hub
{
    public static string ChannelGroup(Guid id) => $"channel:{id}";
    public Task JoinChannel(Guid id)  => Groups.AddToGroupAsync(Context.ConnectionId, ChannelGroup(id));
    public Task StartTyping(Guid id)  => Clients.OthersInGroup(ChannelGroup(id)).SendAsync("typingStarted", new { id, userId = UserId() });
}
```

## Fan-out từ RabbitMQ
Consumer `IConsumer<MessageSent>` inject `IHubContext<ChatHub>` → `SendAsync("messageReceived", ...)`. Xem `masstransit-rabbitmq`.

## Đừng
- ❌ Business/DB write trong Hub. ❌ In-memory groups không backplane khi scale >1 instance.
- ⚠️ Kiểm tra membership trước `JoinChannel` trước GA (tránh nghe lén group kênh).
