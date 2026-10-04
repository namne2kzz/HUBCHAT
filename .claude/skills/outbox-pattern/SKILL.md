---
name: outbox-pattern
description: Use when a command must persist state AND publish integration events atomically — MassTransit EF Core transactional outbox in HUB services.
---

# Skill: Transactional Outbox (MassTransit + EF Core)

"Lưu DB + publish event" phải **atomic**. HUB dùng MassTransit EF outbox: publish qua `IPublishEndpoint` được lưu vào bảng outbox trong cùng transaction với `SaveChanges`, giao sau khi commit.

## Wiring (Infrastructure DI)
```csharp
// Luôn qua AddHubMessaging — KHÔNG tự gọi AddMassTransit (lệch retry/endpoint naming giữa service).
services.AddHubMessaging(configuration, bus =>
    bus.AddEntityFrameworkOutbox<ChatDbContext>(o => { o.UsePostgres(); o.UseBusOutbox(); }));
```

## DbContext
```csharp
protected override void OnModelCreating(ModelBuilder b)
{
    b.AddInboxStateEntity(); b.AddOutboxMessageEntity(); b.AddOutboxStateEntity();
    b.ApplyConfigurationsFromAssembly(typeof(ChatDbContext).Assembly);
}
```

## Handler — publish TRƯỚC SaveChanges
```csharp
db.Messages.Add(message);
await events.PublishAsync(new MessageSent(message.Id, channel.Id, ...), ct); // vào outbox
foreach (var u in message.Mentions)
    await events.PublishAsync(new UserMentioned(u, channel.Id, message.Id, message.AuthorId), ct);
await db.SaveChangesAsync(ct); // commit + outbox delivery
```
`IIntegrationEventPublisher` (Application) → implement bằng `IPublishEndpoint` (Infrastructure).

## Vì sao
- Không có outbox: crash giữa SaveChanges và publish → mất event (hoặc publish rồi rollback → event ma).
- Có outbox: event chỉ giao khi transaction commit; retry tự động; đi kèm inbox → consumer idempotent.

## Đừng
- ❌ Publish trực tiếp ra broker rồi mới SaveChanges. ❌ Publish ngoài scope DbContext của service.
