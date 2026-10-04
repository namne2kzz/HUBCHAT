# Established Patterns

## Memory Management

### Angular — Always Use takeUntilDestroyed or toSignal
```typescript
// ✅ BEST — toSignal auto-unsubscribes
readonly products = toSignal(this.http.get<Product[]>('/api/products'), { initialValue: [] });

// ✅ OK — manual subscribe with auto-cleanup
private readonly destroyRef = inject(DestroyRef);
ngOnInit(): void {
  this.router.events.pipe(
    filter(e => e instanceof NavigationEnd),
    takeUntilDestroyed(this.destroyRef)
  ).subscribe(e => this.trackPage(e));
}

// ✅ Complete Subject on destroy
private readonly destroy$ = new Subject<void>();
ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }
```

### .NET — IDisposable & Resource Cleanup
```csharp
// ✅ Always await using for IAsyncDisposable
await using var db = await _factory.CreateDbContextAsync(ct);

// ✅ HttpClient via factory
services.AddHttpClient<IExternalApiClient, ExternalApiClient>(client => {
    client.BaseAddress = new Uri(config["ExternalApi:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ✅ CancellationToken threaded through
public async Task<Result<T>> ExecuteAsync(CancellationToken ct)
{
    await _repo.SaveAsync(entity, ct);      // passes ct down
    await _bus.PublishAsync(evt, ct);       // passes ct down
}
```

## Generic & Extensible Patterns

### Generic Repository Base (.NET)
```csharp
public abstract class RepositoryBase<T>(AppDbContext db) : IRepository<T>
    where T : Entity
{
    protected readonly AppDbContext Db = db;

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await Db.Set<T>().FindAsync([id], ct);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await Db.Set<T>().AsNoTracking().ToListAsync(ct);

    public void Add(T entity) => Db.Set<T>().Add(entity);
    public void Remove(T entity) => Db.Set<T>().Remove(entity);
}

// Extend for entity-specific queries
public class OrderRepository(AppDbContext db) : RepositoryBase<Order>(db), IOrderRepository
{
    public async Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId, CancellationToken ct)
        => await Db.Orders.Where(o => o.CustomerId == customerId).AsNoTracking().ToListAsync(ct);
}
```

### Generic Base Service (Angular)
```typescript
@Injectable()
abstract class BaseEntityService<T extends { id: string }> {
  protected abstract readonly http: HttpClient;
  protected abstract readonly apiUrl: string;

  protected readonly _items = signal<T[]>([]);
  protected readonly _isLoading = signal(false);
  protected readonly _error = signal<string | null>(null);

  readonly items = this._items.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly count = computed(() => this._items().length);

  /**
   * Loads all entities from the API and updates internal state.
   * @returns Promise resolving when load completes.
   */
  async loadAll(): Promise<void> {
    this._isLoading.set(true);
    this._error.set(null);
    try {
      const data = await firstValueFrom(this.http.get<T[]>(this.apiUrl));
      this._items.set(data);
    } catch {
      this._error.set('Failed to load data');
    } finally {
      this._isLoading.set(false);
    }
  }

  reset(): void { this._items.set([]); this._error.set(null); }
}
```

### Strategy Pattern (.NET)
```csharp
// Define strategy interface
public interface INotificationSender
{
    Task SendAsync(Notification notification, CancellationToken ct = default);
}

// Implementations
public class EmailSender(IEmailClient client) : INotificationSender { ... }
public class SmsSender(ISmsClient client) : INotificationSender { ... }
public class PushSender(IPushClient client) : INotificationSender { ... }

// Register all — resolve by key
services.AddKeyedScoped<INotificationSender, EmailSender>("email");
services.AddKeyedScoped<INotificationSender, SmsSender>("sms");

// Use — open for new channels, closed for modification
public class NotificationService([FromKeyedServices("email")] INotificationSender sender) { ... }
```



## Backend (.NET)

### Repository Pattern
```csharp
// Interface in Domain layer
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default);
    void Add(Order order);
    void Update(Order order);
    void Remove(Order order);
}

// Implementation in Infrastructure
public class OrderRepository(AppDbContext db) : IOrderRepository
{
    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);
}
```

### Command/Query Handler
```csharp
// Command
public record CreateOrderCommand(Guid CustomerId, List<OrderItemDto> Items) : IRequest<Result<Guid>>;

public class CreateOrderCommandHandler(
    IOrderRepository orderRepo,
    ICustomerRepository customerRepo,
    IUnitOfWork uow) : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        var customer = await customerRepo.GetByIdAsync(cmd.CustomerId, ct);
        if (customer is null) return Result.Failure<Guid>("Customer not found");

        var order = Order.Create(customer, cmd.Items.Select(i => OrderItem.Create(i.ProductId, i.Quantity)));
        orderRepo.Add(order);
        await uow.CommitAsync(ct);
        return Result.Success(order.Id);
    }
}
```

### Validator Pattern
```csharp
public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty().WithMessage("Order must have at least one item");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}
```

### Domain Entity Base
```csharp
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    protected void RaiseDomainEvent(IDomainEvent e) => _domainEvents.Add(e);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

---

## Frontend (Angular v20+)

### Feature Service with Signals
```typescript
@Injectable({ providedIn: 'root' })
export class OrderService {
  private readonly http = inject(HttpClient);

  readonly orders = signal<Order[]>([]);
  readonly isLoading = signal(false);
  readonly error = signal<string | null>(null);

  readonly totalOrders = computed(() => this.orders().length);

  async loadOrders(): Promise<void> {
    this.isLoading.set(true);
    this.error.set(null);
    try {
      const data = await firstValueFrom(this.http.get<Order[]>('/api/orders'));
      this.orders.set(data);
    } catch (e) {
      this.error.set('Failed to load orders');
    } finally {
      this.isLoading.set(false);
    }
  }
}
```

### Smart Component Pattern
```typescript
@Component({
  selector: 'app-order-list',
  standalone: true,
  imports: [OrderCardComponent, AsyncPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (service.isLoading()) {
      <app-spinner />
    } @else if (service.error()) {
      <app-error-message [message]="service.error()!" />
    } @else {
      @for (order of service.orders(); track order.id) {
        <app-order-card [order]="order" />
      } @empty {
        <p>No orders found</p>
      }
    }
  `
})
export class OrderListComponent {
  protected readonly service = inject(OrderService);
  ngOnInit() { this.service.loadOrders(); }
}
```

---

## Database

### EF Core Configuration
```csharp
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(50);
        builder.OwnsMany(o => o.Items, items =>
        {
            items.WithOwner().HasForeignKey("OrderId");
            items.HasKey("Id");
        });
        builder.HasIndex(o => o.CustomerId);
        builder.HasIndex(o => new { o.CustomerId, o.Status });
    }
}
```

### Redis Cache Key Convention
```
{service}:{entity}:{id}        → orders:order:abc-123
{service}:{entity}:list        → orders:order:list
{service}:{entity}:user:{uid}  → orders:order:user:xyz-456
```

---

## HUB — Microservices patterns (bổ sung)

### Transactional Outbox (save + publish atomic)
```csharp
db.Messages.Add(message);
await events.PublishAsync(new MessageSent(...), ct); // IPublishEndpoint → outbox
await db.SaveChangesAsync(ct);                        // commit + delivery
```
MassTransit EF outbox (`AddEntityFrameworkOutbox<TDbContext>` + `UseBusOutbox`) → không mất/không ma event. Xem skill `outbox-pattern`.

### Idempotent consumer
Dedupe theo `IntegrationEvent.EventId` (Inbox của MassTransit). Consume phải chịu được double-delivery (at-least-once).

### Find-or-create / idempotent command (chống race)
Check-then-insert **không đủ** (khe race giữa 2 lệnh). Unique index ở DB là đảm bảo; check trước chỉ là đường nhanh.
```csharp
if (await FindAsync(request, ct) is { } existing) return existing.ToDto();
db.Channels.Add(channel);
try { await db.SaveChangesAsync(ct); }
catch (UniqueConstraintViolationException)          // ChatDbContext dịch Postgres 23505 → exception Application
{
    db.DiscardChanges();                             // bỏ insert thua (+ child + outbox rows) khỏi tracker
    var winner = await FindAsync(request, ct);
    if (winner is null) throw;                       // clash khác (vd slug) — không phải của mình
    return winner.ToDto();
}
```
- Index unique **filtered** (`HasFilter("\"Col\" IS NOT NULL")`) khi cột nullable / key tuỳ chọn.
- Idempotency key client (vd `ClientMessageId`) scope **theo user**, replay **không publish event lần 2**.
- Test race ở **IntegrationTests (Postgres thật)**, bắn N request song song; SQLite unit test không có 23505.
- Ví dụ: `PostMessageHandler`, `OpenLinkedThreadHandler`, `FindOrCreateSprintChannelHandler`, `OpenDirectMessageHandler`.

### Keyset (seek) pagination
```csharp
if (MessageCursor.TryDecode(cursor) is { } c) query = query.Where(m => m.CreatedAt < c.CreatedAt);
var page = await query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Take(limit).ToListAsync(ct);
```
Index `(channel_id, created_at)`. Không dùng OFFSET cho list dài.

### Realtime fan-out tách business
chat-service ghi DB + publish; realtime-service consume → `IHubContext<ChatHub>.Clients.Group(...)`. Redis backplane để scale nhiều instance. Xem `signalr-realtime`.

### Cross-service data
Không JOIN chéo DB. Lấy user/context DASHBOARD qua `dashboard-gateway` (REST pull + Redis cache, Polly). Tham chiếu service khác bằng id + integration event.

### Aggregate encapsulation (EF)
Backing field cho collection (`_members`, `_reactions`) + `PropertyAccessMode.Field`; primitive collection (`_mentions` List<Guid>) map jsonb. Query pure-projection không đọc được computed property → materialize rồi map DTO.
