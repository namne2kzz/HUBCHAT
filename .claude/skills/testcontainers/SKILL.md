---
name: testcontainers
description: Use when writing .NET integration tests against real SQL Server/PostgreSQL/Redis containers via Testcontainers, including WebApplicationFactory setup with multi-container fixtures.
---

# Skill: Testcontainers — Integration Testing with Real Containers

Integration testing với SQL Server, PostgreSQL, Redis thực — không mock DB.

## Usage
```
/testcontainers [setup|fixture|test|ci] [context]
```

## Stack
```xml
<!-- tests/Integration/Integration.csproj -->
<PackageReference Include="Testcontainers.MsSql"      Version="3.*" />
<PackageReference Include="Testcontainers.PostgreSql"  Version="3.*" />
<PackageReference Include="Testcontainers.Redis"       Version="3.*" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
<PackageReference Include="xunit"                      Version="2.*" />
<PackageReference Include="FluentAssertions"           Version="6.*" />
```

---

## Database Fixture — SQL Server

```csharp
/// <summary>Spins up a SQL Server container once per test collection, runs migrations, then tears down.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Strong_Pwd_123!")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    /// <summary>Start container and apply EF Core migrations.</summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
    }

    /// <summary>Stop and remove the container.</summary>
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition("SqlServer")]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture> { }
```

---

## Repository Integration Test

```csharp
[Collection("SqlServer")]
public class OrderRepositoryTests(SqlServerFixture fixture)
{
    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options);

    [Fact]
    public async Task AddAsync_ShouldPersistAndRetrieveOrder()
    {
        // Arrange
        await using var ctx = CreateContext();
        var repo = new OrderRepository(ctx);
        var order = Order.Create(Guid.NewGuid(), "Integration test order");

        // Act
        repo.Add(order);
        await ctx.SaveChangesAsync();

        // Assert
        await using var readCtx = CreateContext();
        var saved = await readCtx.Orders.FindAsync(order.Id);
        saved.Should().NotBeNull();
        saved!.Note.Should().Be("Integration test order");
    }
}
```

---

## WebApplicationFactory + Multi-Container

```csharp
/// <summary>Full integration factory replacing real DB and Redis with containers.</summary>
public sealed class IntegrationWebFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _db   = new MsSqlBuilder().Build();
    private readonly RedisContainer _redis = new RedisBuilder().Build();

    public async Task InitializeAsync()
        => await Task.WhenAll(_db.StartAsync(), _redis.StartAsync());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseSqlServer(_db.GetConnectionString()));

            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(_redis.GetConnectionString()));
        });
    }

    public async Task DisposeAsync()
        => await Task.WhenAll(_db.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
}

// API Integration Test
[Collection("Integration")]
public class OrdersApiTests(IntegrationWebFactory factory) : IClassFixture<IntegrationWebFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task POST_Orders_Returns201WithId()
    {
        var request = new { CustomerId = Guid.NewGuid(), Note = "Test order" };
        var response = await _client.PostAsJsonAsync("/api/orders", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBeEmpty();
    }
}
```

---

## Rules
- Share 1 container per collection (`ICollectionFixture`) — không tạo container per test
- Luôn `MigrateAsync()` sau khi container start — không dùng `EnsureCreated()`
- Dùng context riêng biệt cho write và read assertions để tránh first-level cache che lỗi
- CI pipeline cần Docker daemon: thêm `services: - docker` hoặc `- name: Set up Docker` step
- Đặt integration tests trong project riêng `tests/Integration/` — không trộn với unit tests
- Cleanup giữa tests: dùng `DELETE FROM` trong `IAsyncLifetime.InitializeAsync` hoặc transaction rollback

## Prompt Template
```
Viết integration tests cho [RepositoryName / ApiEndpoint] dùng Testcontainers.

Database: [SQL Server | PostgreSQL | Redis]
Scenarios cần test:
1. [happy path]
2. [not found / empty]
3. [concurrent write / constraint violation]

Dùng:
- IClassFixture + ICollectionFixture (share container)
- EF Core MigrateAsync() trong InitializeAsync
- FluentAssertions
- Separate DbContext instances cho write và read
```
