---
name: unit-testing
description: Use when generating or reviewing .NET unit tests (xUnit + Moq + FluentAssertions + Bogus) for command/query handlers, domain entities, validators, or services in this Clean Architecture project.
---

# Skill: .NET Unit Testing

Unit testing patterns for Clean Architecture .NET 10 — xUnit + Moq + FluentAssertions.

## Usage
```
/unit-testing [generate|review|mock|data] [context]
```

## Stack
| Package | Role |
|---------|------|
| `xUnit` | Test runner + `[Fact]` / `[Theory]` / `[InlineData]` |
| `Moq` | Mocking interfaces — `Mock<T>`, `Setup`, `Verify` |
| `FluentAssertions` | Readable assertions — `.Should().Be()`, `.ThrowAsync()` |
| `Bogus` | Fake data generation — `Faker<T>`, `faker.Name`, `faker.Internet` |
| `AutoFixture` | Auto-generate complex objects with sensible defaults |

```xml
<!-- tests/Unit/Unit.csproj -->
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
<PackageReference Include="Moq" Version="4.*" />
<PackageReference Include="FluentAssertions" Version="6.*" />
<PackageReference Include="Bogus" Version="34.*" />
```

---

## Test Structure — AAA Pattern

Always: **Arrange → Act → Assert**. One logical assertion group per test. Name tests: `Method_Condition_ExpectedResult`.

```csharp
[Fact]
public async Task Handle_ValidCommand_ReturnsSuccessWithId()
{
    // Arrange
    var order = OrderBuilder.Build();
    _orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(order);

    // Act
    var result = await _sut.Handle(new ConfirmOrderCommand(order.Id), CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().Be(order.Id);
}
```

---

## Moq Patterns

### Setup & Returns
```csharp
// Return value
_repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
         .ReturnsAsync(entity);

// Return null (not found)
_repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync((Order?)null);

// Throw exception
_repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
         .ThrowsAsync(new InvalidOperationException("DB offline"));

// Return Task.CompletedTask (void async)
_uowMock.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

// Conditional match
_repoMock.Setup(r => r.GetByIdAsync(It.Is<Guid>(id => id != Guid.Empty), It.IsAny<CancellationToken>()))
         .ReturnsAsync(entity);
```

### Verify — Assert interactions
```csharp
// Called exactly once
_uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

// Never called
_uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);

// Called with specific argument
_repoMock.Verify(r => r.Add(It.Is<Order>(o => o.CustomerId == customerId)), Times.Once);

// Verify all setups were used (strict mocking)
_repoMock.VerifyAll();
```

### Capture — Inspect what was passed
```csharp
Order? capturedOrder = null;
_repoMock
    .Setup(r => r.Add(It.IsAny<Order>()))
    .Callback<Order>(order => capturedOrder = order);

await _sut.Handle(command, CancellationToken.None);

capturedOrder.Should().NotBeNull();
capturedOrder!.Status.Should().Be(OrderStatus.Confirmed);
capturedOrder.CustomerId.Should().Be(command.CustomerId);
```

### Sequence — Different returns on repeated calls
```csharp
_repoMock.SetupSequence(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
         .ReturnsAsync(null)       // first call → not found
         .ReturnsAsync(entity);   // second call → found
```

---

## Test Classes — Structure

```csharp
public class CreateOrderCommandHandlerTests
{
    // ── Dependencies ───────────────────────────────────────────────────────
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<ICustomerRepository> _customerRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly CreateOrderCommandHandler _sut;

    public CreateOrderCommandHandlerTests()
    {
        _sut = new CreateOrderCommandHandler(
            _orderRepoMock.Object,
            _customerRepoMock.Object,
            _uowMock.Object
        );
    }

    // ── Happy Path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidCommand_CreatesOrderAndReturnsId()
    {
        var customer = CustomerBuilder.Build();
        var command = CommandFaker.CreateOrder(customer.Id);

        _customerRepoMock.Setup(r => r.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(customer);
        _uowMock.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _orderRepoMock.Verify(r => r.Add(It.IsAny<Order>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Not Found ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CustomerNotFound_ReturnsFailure()
    {
        var command = CommandFaker.CreateOrder(Guid.NewGuid());

        _customerRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync((Customer?)null);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
        _orderRepoMock.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Validation ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task Handle_InvalidNote_ValidationFails(string? note)
    {
        var validator = new CreateOrderCommandValidator();
        var command = new CreateOrderCommand(Guid.NewGuid(), note!, []);

        var result = await validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Note));
    }

    // ── Exception Propagation ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        var command = CommandFaker.CreateOrder(Guid.NewGuid());

        _customerRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        Func<Task> act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DB error");
    }
}
```

---

## Fake Data — Bogus

### Faker<T> — Entity fakers
```csharp
// tests/Unit/Builders/Fakers.cs
using Bogus;

public static class Fakers
{
    // Seeded — same data every run for reproducibility
    private static readonly int Seed = 42;

    public static Faker<Order> OrderFaker() => new Faker<Order>()
        .CustomInstantiator(f => Order.Create(
            customerId: f.Random.Guid(),
            note: f.Lorem.Sentence()
        ));

    public static Faker<Customer> CustomerFaker() => new Faker<Customer>()
        .CustomInstantiator(f => Customer.Create(
            name: f.Name.FullName(),
            email: f.Internet.Email(),
            phone: f.Phone.PhoneNumber("###-###-####")
        ));

    public static Faker<Product> ProductFaker() => new Faker<Product>()
        .CustomInstantiator(f => Product.Create(
            name: f.Commerce.ProductName(),
            price: f.Finance.Amount(1, 1000),
            sku: f.Commerce.Ean13()
        ));
}
```

### Builder — Fluent test object construction
```csharp
// tests/Unit/Builders/OrderBuilder.cs
public static class OrderBuilder
{
    private static readonly Faker<Order> _faker = Fakers.OrderFaker();

    /// <summary>Build a valid Order with defaults or overrides.</summary>
    public static Order Build(
        Guid? customerId = null,
        string? note = null)
    {
        var order = _faker.Generate();

        // Apply overrides via reflection or re-create if needed
        return order;
    }

    /// <summary>Build a list of N valid orders.</summary>
    public static List<Order> BuildList(int count = 3)
        => _faker.Generate(count);
}
```

### CommandFaker — Generate valid commands
```csharp
// tests/Unit/Builders/CommandFaker.cs
public static class CommandFaker
{
    private static readonly Faker _f = new();

    /// <summary>Generate a valid CreateOrderCommand.</summary>
    public static CreateOrderCommand CreateOrder(
        Guid? customerId = null,
        string? note = null) =>
        new(
            CustomerId: customerId ?? _f.Random.Guid(),
            Note: note ?? _f.Lorem.Sentence(),
            Items: [new OrderItemDto(_f.Random.Guid(), _f.Random.Int(1, 10))]
        );
}
```

---

## Domain Entity Tests

Test domain logic directly — no mocks needed.

```csharp
public class OrderTests
{
    [Fact]
    public void Confirm_PendingOrder_StatusChangesToConfirmed()
    {
        var order = OrderBuilder.Build();
        order.Confirm();
        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void Confirm_AlreadyConfirmed_ThrowsDomainException()
    {
        var order = OrderBuilder.Build();
        order.Confirm();

        var act = () => order.Confirm();

        act.Should().Throw<DomainException>()
           .WithMessage("*already confirmed*");
    }

    [Fact]
    public void AddItem_ExceedsLimit_ThrowsDomainException()
    {
        var order = OrderBuilder.Build();
        for (var i = 0; i < 100; i++)
            order.AddItem(OrderItem.Create(Guid.NewGuid(), 1));

        var act = () => order.AddItem(OrderItem.Create(Guid.NewGuid(), 1));

        act.Should().Throw<DomainException>()
           .WithMessage("*cannot exceed 100*");
    }

    [Fact]
    public void Confirm_RaisesDomainEvent()
    {
        var order = OrderBuilder.Build();
        order.Confirm();
        order.DomainEvents.Should().ContainSingle(e => e is OrderConfirmedEvent);
    }
}
```

---

## Query Handler Tests

Queries mock the read-side only — no UoW needed.

```csharp
public class GetOrderByIdQueryHandlerTests
{
    private readonly Mock<IOrderRepository> _repoMock = new();
    private readonly GetOrderByIdQueryHandler _sut;

    public GetOrderByIdQueryHandlerTests()
        => _sut = new GetOrderByIdQueryHandler(_repoMock.Object);

    [Fact]
    public async Task Handle_ExistingOrder_ReturnsDto()
    {
        var order = OrderBuilder.Build();
        _repoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(order);

        var result = await _sut.Handle(new GetOrderByIdQuery(order.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task Handle_MissingOrder_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Order?)null);

        var result = await _sut.Handle(new GetOrderByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }
}
```

---

## Validator Tests

```csharp
public class CreateOrderCommandValidatorTests
{
    private readonly CreateOrderCommandValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Validate_EmptyNote_IsInvalid(string? note)
    {
        var cmd = CommandFaker.CreateOrder(note: note);
        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.Note));
    }

    [Fact]
    public async Task Validate_EmptyItems_IsInvalid()
    {
        var cmd = new CreateOrderCommand(Guid.NewGuid(), "Note", []);
        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.Items));
    }

    [Fact]
    public async Task Validate_ValidCommand_IsValid()
    {
        var cmd = CommandFaker.CreateOrder();
        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }
}
```

---

## Service / Infrastructure Tests

Mock external dependencies (HTTP, cache, email).

```csharp
public class NotificationServiceTests
{
    private readonly Mock<IEmailClient> _emailMock = new();
    private readonly Mock<ISmsClient> _smsMock = new();
    private readonly NotificationService _sut;

    public NotificationServiceTests()
        => _sut = new NotificationService(_emailMock.Object, _smsMock.Object);

    [Fact]
    public async Task Send_EmailChannel_CallsEmailClientOnce()
    {
        _emailMock.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        await _sut.SendAsync(new Notification("user@test.com", "Subject", "Body"), Channel.Email, CancellationToken.None);

        _emailMock.Verify(e => e.SendAsync(
            It.Is<EmailMessage>(m => m.To == "user@test.com"),
            It.IsAny<CancellationToken>()), Times.Once);

        _smsMock.Verify(s => s.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

---

## Prompt Templates

### Generate Unit Tests
```
Generate xUnit unit tests for this .NET 10 handler:

**Handler Code**:
[Paste command/query handler + domain entity code]

**Dependencies to mock** (interfaces injected):
- [List interface names]

**Test cases to cover**:
1. Happy path — valid input, all dependencies succeed
2. Not found — entity missing from repo
3. Validation — [list invalid field values]
4. Exception propagation — repo throws
5. [Any domain rule that can fail]

Use:
- xUnit [Fact] / [Theory] + [InlineData]
- Moq for all interface dependencies
- FluentAssertions for all assertions
- Bogus Builder pattern for test data (static BuildXxx() helpers)
- Moq Callback to capture arguments when needed
- AAA structure with region comments (// Arrange / // Act / // Assert)

Generate:
1. Test class with constructor-initialized mocks
2. All test cases
3. Static Builder helpers at the bottom
```

### Review Unit Tests
```
Review these unit tests for quality and completeness:

**Test Code**:
[Paste test class]

Check for:
1. Missing test cases (not-found, validation failures, exception paths)
2. Weak assertions (only checking IsSuccess, not the value)
3. Missing Verify() calls — was the side effect actually called?
4. Setup leaking between tests (shared state mutation)
5. Real implementations in tests (should be mocked)
6. Magic values without explanation (use named variables or Bogus)
7. Tests that can pass even when handler is broken (false positives)

Output:
- Issues with severity (Critical / Warning / Suggestion)
- Fixed or supplemented test code
```

### Add Edge Cases
```
This handler test only has a happy path. Add edge cases for:

**Current Tests**:
[Paste existing test class]

**Business Rules** (from domain/validator):
[Describe the rules — e.g., "order cannot have more than 100 items", "customer must be active"]

Add tests for:
- Each business rule violation → DomainException with correct message
- Each validation rule → FluentValidation errors on correct field
- Concurrent modification (optional — if optimistic concurrency used)
- CancellationToken cancellation mid-operation
```

---

## Quick Reference

| Scenario | Pattern |
|----------|---------|
| Async method returns entity | `.ReturnsAsync(entity)` |
| Async method returns null | `.ReturnsAsync((Order?)null)` |
| Async void method | `.Returns(Task.CompletedTask)` |
| Throws async | `.ThrowsAsync(new Exception())` |
| Called once | `Verify(..., Times.Once)` |
| Never called | `Verify(..., Times.Never)` |
| Capture argument | `.Callback<T>(arg => captured = arg)` |
| Different on each call | `.SetupSequence(...)` |
| Assert throws | `await act.Should().ThrowAsync<ExType>()` |
| Assert collection | `.Should().ContainSingle()`, `.HaveCount(n)` |
| Fake string | `new Faker().Lorem.Sentence()` |
| Fake email | `new Faker().Internet.Email()` |
| Fake Guid | `new Faker().Random.Guid()` |
| Fake decimal | `new Faker().Finance.Amount(1, 1000)` |
