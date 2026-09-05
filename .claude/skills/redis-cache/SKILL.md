---
name: redis-cache
description: Use when designing or reviewing a Redis caching strategy in .NET (StackExchange.Redis / IDistributedCache) — TTL, key design, invalidation patterns.
---

# Skill: Redis Caching (StackExchange.Redis)

Redis caching patterns with StackExchange.Redis and IDistributedCache in .NET.

## Usage
```
/redis-cache [design|review|pattern|invalidation] [context]
```

---

## Prompt Templates

### Design Cache Strategy
```
Design a Redis caching strategy for:

**Feature/Service**: [Name]
**Data to cache**: [Description of data]

**Data Characteristics**:
- Size per entry: [KB/MB]
- Total entries: [count estimate]
- Update frequency: [how often data changes]
- Consistency requirement: [Strong / Eventual (tolerate stale)]

**Access Pattern**:
- Read frequency: [times/sec]
- Write frequency: [times/sec]
- Access distribution: [uniform / hot keys]

**Current Performance**:
- DB query time: [ms]
- Target API response time: [ms]

Provide:
1. Cache key naming convention
2. TTL recommendation with reasoning
3. Cache pattern (cache-aside, write-through, write-behind)
4. Cache invalidation strategy
5. Cache warming approach (if needed)
6. .NET implementation code
7. Redis memory estimate
```

### Review Cache Implementation
```
Review this Redis caching implementation:

**Code**:
[Paste caching code]

Check for:
1. Missing TTL (cache grows indefinitely)
2. Cache stampede risk (multiple concurrent misses hitting DB)
3. Non-atomic read-modify-write (race condition)
4. Large values (serialize and check size)
5. Key naming inconsistency
6. Missing serialization null checks
7. No circuit breaker (what if Redis is down?)
8. Fire-and-forget without error logging

Provide:
- Issues found with severity
- Fixed implementation
```

### Cache Invalidation Design
```
Design cache invalidation for:

**Cached Data**: [What's cached]
**Mutation Events**: [List of operations that change the data]

**Invalidation Approach**:
- [ ] TTL only (accept stale within TTL window)
- [ ] Event-driven (invalidate on mutation)
- [ ] Write-through (update cache on write)
- [ ] Cache version bump (increment key version)

**Distributed Concerns**:
- Multiple instances: [Yes/No]
- Need pub/sub for invalidation: [Yes/No]

Provide:
1. Invalidation code for each mutation event
2. Redis pub/sub setup if multi-instance
3. Fallback behavior when cache is unavailable
```

---

## Implementation Patterns

### Service Setup
```csharp
// Registration
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "myapp_";  // key prefix
});

// Or with ConnectionMultiplexer for advanced ops
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
```

### Generic Cache Service
```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);
}

public class RedisCacheService(IDistributedCache cache, IConnectionMultiplexer mux, ILogger<RedisCacheService> logger)
    : ICacheService
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var bytes = await cache.GetAsync(key, ct);
            return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes, _json);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Cache GET failed for key {Key}", key); return default; }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, _json);
            await cache.SetAsync(key, bytes,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Cache SET failed for key {Key}", key); }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl, CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null) return cached;

        var value = await factory();
        await SetAsync(key, value, ttl, ct);
        return value;
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try { await cache.RemoveAsync(key, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Cache REMOVE failed for key {Key}", key); }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
    {
        // Use SCAN — never use KEYS in production!
        var server = mux.GetServers().First();
        var keys = server.KeysAsync(pattern: pattern);
        var db = mux.GetDatabase();
        await foreach (var key in keys)
            await db.KeyDeleteAsync(key);
    }
}
```

### Key Convention
```csharp
public static class CacheKeys
{
    public static string Order(Guid id)          => $"orders:order:{id}";
    public static string OrdersByUser(Guid uid)  => $"orders:order:user:{uid}";
    public static string ProductList()           => "catalog:product:list";
    public static string Product(Guid id)        => $"catalog:product:{id}";
}

// TTLs as constants
public static class CacheTtl
{
    public static readonly TimeSpan ReferenceData = TimeSpan.FromHours(1);
    public static readonly TimeSpan UserData      = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan SearchResults = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan Session       = TimeSpan.FromMinutes(30);
}
```
