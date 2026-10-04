---
name: masstransit-rabbitmq
description: Use when publishing or consuming integration events over HUB's RabbitMQ broker via MassTransit — bus setup, consumers, retry/DLQ, message contracts, idempotency.
---

# Skill: MassTransit + RabbitMQ (HUB internal bus)

Event async **giữa các service HUB** qua RabbitMQ (broker riêng của HUB). Contract đặt ở `HUB.Shared.Contracts/Events` (versioned), publish qua outbox (xem `outbox-pattern`).

## Nguyên tắc
- Event kế thừa `IntegrationEvent` (có `EventId` + `OccurredAtUtc`) → dùng `EventId` làm **idempotency key**.
- `record` bất biến, chỉ chứa id + dữ liệu tối thiểu (không nhét cả entity).
- Publish bằng generic `Publish<TConcrete>` để giữ đúng message type.
- Consumer **idempotent**: dedupe theo `EventId` (Inbox của MassTransit lo việc này khi bật EF outbox/inbox).

## Bus setup (publish-only service)
```csharp
services.AddHubMessaging(configuration); // HUB.Shared.Messaging: RabbitMq host + retry + delayed redelivery
```

## Bus setup (service có consumer)
```csharp
services.AddHubMessaging(configuration, bus => bus.AddConsumer<MessageSentConsumer>());
```

## Consumer mẫu
```csharp
public sealed class MessageSentConsumer(IHubContext<ChatHub> hub) : IConsumer<MessageSent>
{
    public async Task Consume(ConsumeContext<MessageSent> ctx)
    {
        var m = ctx.Message;
        await hub.Clients.Group(ChatHub.ChannelGroup(m.ChannelId))
            .SendAsync("messageReceived", new { m.MessageId, m.ChannelId, m.Preview }, ctx.CancellationToken);
    }
}
```

## Retry / DLQ (đã cấu hình trong HUB.Shared.Messaging — `AddHubRetryPolicy`)
- Per-endpoint (qua `AddConfigureEndpointsCallback`), **exponential in-memory** 5 lần (~0.2s → 7.7s), rồi fault → `_error`. Inspect qua RabbitMQ UI :15672.
- **Không dùng `UseDelayedRedelivery`**: broker dùng chung với DASHBOARD (`rabbitmq:3-management`) không có plugin `rabbitmq_delayed_message_exchange`.
- Exception nghiệp vụ (không transient) → ignore để fault ngay, không retry vô ích:
```csharp
services.AddHubMessaging(configuration,
    bus => bus.AddConsumer<FileUploadedConsumer>(),
    configureRetry: r => r.Ignore<DomainException>());
```
- Test consumer với đúng policy production: `AddMassTransitTestHarness(bus => { bus.AddHubRetryPolicy(); bus.AddConsumer<...>(); })`.

## Đừng
- ❌ Publish base `IntegrationEvent` (mất concrete type). ❌ Gọi broker của DASHBOARD.
- ❌ Consumer có side-effect không idempotent (double-delivery là bình thường ở at-least-once).
