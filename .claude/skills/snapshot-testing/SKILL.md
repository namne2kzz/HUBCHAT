---
name: snapshot-testing
description: Use when writing or reviewing snapshot tests (Verify.Xunit) for complex DTOs, API responses, or email templates in .NET — setup, scrubbing unstable fields, and CI approval flow.
---

# Skill: Snapshot Testing — Verify Library

Assert complex objects, API responses, emails bằng cách so sánh với approved snapshots.

## Usage
```
/snapshot-testing [setup|test|update|ci] [context]
```

## Stack
```xml
<PackageReference Include="Verify.Xunit"           Version="26.*" />
<PackageReference Include="Verify.Http"            Version="26.*" />    <!-- HTTP responses -->
<PackageReference Include="Verify.EntityFramework" Version="26.*" />    <!-- EF query output -->
```

---

## Setup — ModuleInitializer

```csharp
// tests/Unit/ModuleInitializer.cs
// Chạy một lần khi test assembly load — configure Verify globally
public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifyDiff.Initialize();      // show diff in test output
        VerifierSettings.ScrubMember("id");         // scrub unstable fields globally
        VerifierSettings.ScrubMember("createdAt");
    }
}
```

---

## Basic Snapshot Test

```csharp
[UsesVerify]
public class OrderDtoMapperTests
{
    [Fact]
    public async Task Map_OrderEntity_MatchesSnapshot()
    {
        // Arrange
        var order = Order.Create(
            customerId: Guid.Parse("11111111-1111-1111-1111-111111111111"),  // fixed value cho stable snapshot
            note: "Snapshot test order");
        order.AddItem(OrderItem.Create(
            productId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            quantity: 3));

        // Act
        var dto = OrderMapper.ToDto(order);

        // Assert — first run: creates .verified.txt; subsequent runs: compares
        await Verify(dto);
    }
}
```

Lần đầu chạy tạo file: `OrderDtoMapperTests.Map_OrderEntity_MatchesSnapshot.verified.txt`
```json
{
  "customerId": "11111111-1111-1111-1111-111111111111",
  "note": "Snapshot test order",
  "status": "Pending",
  "items": [
    {
      "productId": "22222222-2222-2222-2222-222222222222",
      "quantity": 3
    }
  ]
}
```

---

## HTTP Response Snapshot

```csharp
[UsesVerify]
public class OrdersControllerSnapshotTests(IntegrationWebFactory factory)
    : IClassFixture<IntegrationWebFactory>
{
    [Fact]
    public async Task GET_Order_ResponseMatchesSnapshot()
    {
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/orders/{KnownOrderId}");

        // Assert — snapshot cả status code + headers + body
        await VerifyJson(response);
    }
}
```

---

## Scrubbing — Loại bỏ giá trị không ổn định

```csharp
[Fact]
public async Task CreateOrder_ResponseMatchesSnapshot()
{
    var dto = await _handler.Handle(command, CancellationToken.None);

    await Verify(dto)
        .ScrubMember("id")            // Guid mới mỗi lần → scrub
        .ScrubMember("createdAt")     // timestamp → scrub
        .ScrubMember("updatedAt");
}
```

Kết quả trong `.verified.txt`:
```json
{
  "id": "Guid_1",         ← replaced with deterministic placeholder
  "createdAt": "DateTime_1",
  "note": "Test order",
  "status": "Confirmed"
}
```

---

## Update Snapshots

```bash
# Chạy tests và tự động accept tất cả thay đổi
dotnet test -- --verify-accept-all

# Hoặc set env var trước khi chạy
$env:VerifyTests_AcceptAll = "true"; dotnet test

# Chỉ update 1 test cụ thể: thêm attribute
[Fact(Skip = "update snapshot")]
```

---

## CI/CD — Fail nếu snapshot chưa approved

```yaml
# github-actions.yml
- name: Run tests
  run: dotnet test
  env:
    CI: true   # Verify tự động fail nếu có .received file chưa approved trên CI
```

```bash
# Verify tất cả .received files đã được committed
git diff --name-only | grep ".verified."
# Nếu có file → developer quên commit snapshot update
```

---

## Khi nào dùng Snapshot vs FluentAssertions

| Dùng Snapshot (Verify) | Dùng FluentAssertions |
|------------------------|----------------------|
| DTO mapping phức tạp nhiều fields | Logic assertions đơn giản |
| API response body đầy đủ | `.IsSuccess.Should().BeTrue()` |
| Email template rendering | Numeric / boolean checks |
| Complex domain object state | Exception type + message |
| Serialized output (JSON, XML) | Collection count / containment |

---

## Rules
- Commit `.verified.txt` files vào git — chúng là expected output, không phải generated
- Không commit `.received.txt` files — đây là actual output chờ approval
- Dùng fixed Guid/DateTime trong test data để snapshot ổn định, hoặc Scrub
- Một test = một snapshot — không assert nhiều objects trong 1 Verify call
- Đặt snapshot files cùng folder với test file hoặc folder `Snapshots/` riêng

## Prompt Template
```
Thêm snapshot tests cho [ClassName / API endpoint].

Objects cần snapshot:
1. [DTO name] — mapping từ [Entity]
2. [API response] từ [GET|POST] /api/[route]

Unstable fields cần scrub: [id, createdAt, updatedAt, ...]

Tạo:
1. ModuleInitializer.cs với global Verify config
2. Snapshot test methods
3. Hướng dẫn approve snapshots lần đầu
```
