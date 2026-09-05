# Common Mistakes to Avoid

## Backend (.NET)

### ❌ Sync-over-async deadlocks
```csharp
// WRONG — blocks thread pool, causes deadlocks in ASP.NET
var result = someAsyncMethod().Result;
someAsyncMethod().Wait();

// CORRECT
var result = await someAsyncMethod();
```

### ❌ DbContext in singleton services
```csharp
// WRONG — DbContext is scoped, not singleton-safe
public class MySingletonService(AppDbContext db) { }  // DbContext injected into singleton!

// CORRECT — use IDbContextFactory
public class MySingletonService(IDbContextFactory<AppDbContext> factory)
{
    public async Task DoWork()
    {
        await using var db = await factory.CreateDbContextAsync();
        // use db here
    }
}
```

### ❌ Loading full entities for read operations
```csharp
// WRONG — loads all columns, all navigation props
var orders = await db.Orders.Include(o => o.Items).Include(o => o.Customer).ToListAsync();

// CORRECT — project to DTO
var orders = await db.Orders
    .Where(o => o.Status == OrderStatus.Active)
    .Select(o => new OrderSummaryDto(o.Id, o.Customer.Name, o.Items.Count))
    .AsNoTracking()
    .ToListAsync();
```

### ❌ Domain logic in Infrastructure/WebApi layers
```csharp
// WRONG — business rule in controller
[HttpPost]
public async Task<IActionResult> CreateOrder(CreateOrderRequest req)
{
    if (req.Items.Count > 100) return BadRequest("Too many items"); // ← domain logic here!
    ...
}

// CORRECT — domain rule in Domain entity, validation in Application
public class Order : Entity
{
    private const int MaxItems = 100;
    public void AddItem(OrderItem item)
    {
        if (_items.Count >= MaxItems) throw new DomainException("Order cannot exceed 100 items");
        _items.Add(item);
    }
}
```

### ❌ Swallowing exceptions
```csharp
// WRONG
try { await DoWork(); } catch { }  // silent failure

// CORRECT
try { await DoWork(); }
catch (Exception ex) { _logger.LogError(ex, "Failed to do work for {Context}", context); throw; }
```

---

## Memory & Resource Leaks

### ❌ Bare subscribe() — Angular memory leak
```typescript
// WRONG — subscription never cleaned up
ngOnInit(): void {
  this.productService.products$.subscribe(p => this.products = p);
}

// CORRECT — auto-cleanup via toSignal
readonly products = toSignal(this.productService.products$, { initialValue: [] });

// CORRECT — manual with takeUntilDestroyed
private readonly destroyRef = inject(DestroyRef);
ngOnInit(): void {
  this.productService.products$
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe(p => this.products.set(p));
}
```

### ❌ new HttpClient() — socket exhaustion in .NET
```csharp
// WRONG — bypasses connection pool, causes socket exhaustion under load
public class MyService {
  public async Task CallApiAsync() {
    using var client = new HttpClient();  // creates new connections each time!
    await client.GetAsync("...");
  }
}

// CORRECT — injected via IHttpClientFactory
public class MyService(HttpClient http) { ... }
// + services.AddHttpClient<MyService>();
```

### ❌ DbContext in Singleton — ObjectDisposedException
```csharp
// WRONG — DbContext is Scoped; Singleton outlives it → ObjectDisposedException
public class MySingleton(AppDbContext db) { }  // scoped in singleton!

// CORRECT — factory pattern
public class MySingleton(IDbContextFactory<AppDbContext> factory)
{
  public async Task DoWork(CancellationToken ct)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    // db is properly scoped to this operation
  }
}
```

### ❌ Violating SOLID — Concrete dependencies
```csharp
// WRONG — depends on concrete, impossible to test or swap
public class OrderService {
  private readonly SqlOrderRepository _repo = new SqlOrderRepository(); // hardcoded!
}

// CORRECT — depends on abstraction, injectable
public class OrderService(IOrderRepository repo) { }
```

## Frontend (Angular v20+)

### ❌ Using `*ngIf` / `*ngFor` (old syntax)
```html
<!-- WRONG — old structural directive syntax -->
<div *ngIf="isLoading">Loading...</div>
<li *ngFor="let item of items">{{ item.name }}</li>

<!-- CORRECT — new control flow -->
@if (isLoading()) { <div>Loading...</div> }
@for (item of items(); track item.id) { <li>{{ item.name }}</li> }
```

### ❌ Mutating signal values directly
```typescript
// WRONG — signals are immutable, mutation doesn't trigger reactivity
const items = signal<Item[]>([]);
items().push(newItem);  // ← no reactivity!

// CORRECT
items.update(prev => [...prev, newItem]);
```

### ❌ HTTP calls inside components
```typescript
// WRONG — violates SRP, hard to test
@Component({...})
export class OrderComponent {
  private http = inject(HttpClient);
  ngOnInit() { this.http.get('/api/orders').subscribe(...) }  // ← HTTP in component
}

// CORRECT — HTTP in service
@Component({...})
export class OrderComponent {
  private orderService = inject(OrderService);
  ngOnInit() { this.orderService.loadOrders(); }
}
```

### ❌ Missing `track` in `@for`
```html
<!-- WRONG — poor performance, full DOM re-render on any change -->
@for (item of items()) { <app-item [item]="item" /> }

<!-- CORRECT -->
@for (item of items(); track item.id) { <app-item [item]="item" /> }
```

### ❌ Storing tokens in localStorage
```typescript
// WRONG — vulnerable to XSS
localStorage.setItem('token', token);

// CORRECT — use in-memory or httpOnly cookie
// Store in service memory, use refresh token flow with httpOnly cookies
```

---

## Database

### ❌ N+1 queries
```csharp
// WRONG — 1 query for orders + N queries for each customer
var orders = await db.Orders.ToListAsync();
foreach (var o in orders)
{
    var customer = await db.Customers.FindAsync(o.CustomerId); // N+1!
}

// CORRECT — eager load or projection
var orders = await db.Orders
    .Select(o => new { o.Id, CustomerName = o.Customer.Name })
    .ToListAsync();
```

### ❌ Forgetting migration rollback (Down method)
```csharp
// WRONG — no rollback path
protected override void Down(MigrationBuilder migrationBuilder) { }

// CORRECT — always implement Down
protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(column: "NewColumn", table: "Orders");
}
```

### ❌ Redis key without TTL
```csharp
// WRONG — cache grows indefinitely
await cache.SetStringAsync(key, value);

// CORRECT — always set TTL
await cache.SetStringAsync(key, value, new DistributedCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
});
```

---

## Unit Testing

### ❌ No Verify() — side effect never confirmed
```csharp
// WRONG — test passes even if uow.CommitAsync was never called
result.IsSuccess.Should().BeTrue();

// CORRECT — verify the commit actually happened
result.IsSuccess.Should().BeTrue();
_uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
```

### ❌ Mocking concrete classes instead of interfaces
```csharp
// WRONG — Moq can only mock virtual members on concrete classes; brittle
var mock = new Mock<OrderRepository>(db);

// CORRECT — always mock the interface
var mock = new Mock<IOrderRepository>();
```

### ❌ Magic values in test data — hard to tell what's important
```csharp
// WRONG — is "abc@test.com" significant? Hard to tell
var command = new CreateCustomerCommand("abc@test.com", "John");

// CORRECT — use Bogus or named variables that signal intent
var validEmail = new Faker().Internet.Email();
var command = new CreateCustomerCommand(validEmail, "John Doe");

// CORRECT for edge-case intent — make the value meaningful
var invalidEmail = "not-an-email";
var command = new CreateCustomerCommand(invalidEmail, "John");
```

### ❌ Setup leaking between tests (shared mutable state)
```csharp
// WRONG — mock set up in class field; prior test's Setup may interfere
private readonly Mock<IOrderRepository> _repo = new();  // shared instance

// CORRECT — fresh mock per test, initialized in constructor or [TestInitialize]
public CreateOrderCommandHandlerTests()
{
    _repo = new Mock<IOrderRepository>();    // reset each test run
    _sut = new CreateOrderCommandHandler(_repo.Object, _uow.Object);
}
```

### ❌ Angular: no fixture.detectChanges() after signal change
```typescript
// WRONG — DOM still shows old value
(serviceSpy.orders as WritableSignal<Order[]>).set([buildOrder()]);
const cards = fixture.nativeElement.querySelectorAll('app-order-card');
expect(cards.length).toBe(1);  // ← fails, DOM not updated

// CORRECT
(serviceSpy.orders as WritableSignal<Order[]>).set([buildOrder()]);
fixture.detectChanges();        // ← triggers change detection
const cards = fixture.nativeElement.querySelectorAll('app-order-card');
expect(cards.length).toBe(1);
```

### ❌ Angular: missing httpMock.verify() — pending requests silently ignored
```typescript
// WRONG — test passes but an HTTP call was never asserted
afterEach(() => { /* nothing */ });

// CORRECT — catch dangling requests
afterEach(() => httpMock.verify());
```

### ❌ Testing only the happy path — false confidence
```csharp
// WRONG — only tests success; domain rules and error paths untested
[Fact]
public async Task Handle_ReturnsSuccess() { ... }

// CORRECT — always add:
// - not-found case
// - each validation rule failure
// - exception propagation
// - domain rule violation (if applicable)
```

---

## HUB — Microservices pitfalls (bổ sung)

### ❌ Publish event ngoài outbox
```csharp
await _bus.Publish(evt);       // rồi mới SaveChanges → crash giữa chừng = event ma / mất event
await db.SaveChangesAsync(ct);
```
✅ Publish qua `IPublishEndpoint` với EF outbox bật; delivery gắn với commit.

### ❌ JOIN chéo database giữa service
Mỗi service 1 DB (`hub_chat`, `hub_directory`...). Không query DB service khác. ✅ pull/cache qua dashboard-gateway hoặc integration event.

### ❌ Đọc DB DASHBOARD hoặc nghe broker DASHBOARD
HUB tách rời: chỉ gọi `/internal/v1/*` (service-token) + cache. Không coupling DB/broker chéo.

### ❌ Nhét membership/role vào JWT
Membership là danh sách động (per-Repository) → token phình + stale. ✅ pull `/internal/v1/me`, cache Redis TTL ngắn.

### ❌ SignalR không backplane khi scale
`Clients.Group` chỉ tới connection trên cùng pod. ✅ `AddStackExchangeRedis` (Redis backplane) khi >1 instance.

### ❌ Business logic / DB write trong SignalR Hub
Hub chỉ là edge (join group, typing, fan-out). Ghi DB + publish event ở chat-service.

### ❌ OFFSET pagination cho message list
Chậm dần theo độ sâu. ✅ keyset theo `(created_at)` + index `(channel_id, created_at)`.

### ❌ Consumer không idempotent
At-least-once ⇒ double-delivery bình thường. ✅ dedupe theo `EventId`.

### ⚠️ SignalR JoinChannel chưa check membership (P1 TODO)
Trước GA phải verify user là member kênh (gọi chat-service / membership-cache) trước khi `AddToGroupAsync`.
