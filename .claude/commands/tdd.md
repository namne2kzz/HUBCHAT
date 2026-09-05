---
description: Run the Red-Green-Refactor TDD loop for a .NET handler, domain logic, or Angular service.
argument-hint: [mô tả tính năng cần implement]
---

# Workflow: TDD — Test-Driven Development

Red → Green → Refactor loop cho .NET handlers, domain logic, và Angular services.

## Usage
```
/tdd [mô tả tính năng cần implement]
```

---

## Bước 1 — Clarify Requirements (RED setup)

Trước khi viết test, xác định:
- **Input**: Command / Query / Method parameters là gì?
- **Output**: Return type, success/failure cases?
- **Business rules**: Validation nào? Domain invariants nào?
- **Side effects**: Repository calls, events, cache invalidation?

---

## Bước 2 — Write Failing Tests (RED)

Viết test trước khi có implementation. Tests sẽ fail — đó là mục tiêu.

### .NET — Command Handler
```csharp
// Viết test class TRƯỚC, để compile fail là bình thường ở bước này
public class CreateProductCommandHandlerTests
{
    private readonly Mock<IProductRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private CreateProductCommandHandler _sut;   // chưa có class này → compile error OK

    public CreateProductCommandHandlerTests()
    {
        _uowMock.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        _sut = new CreateProductCommandHandler(_repoMock.Object, _uowMock.Object);
    }

    // Test 1: happy path
    [Fact]
    public async Task Handle_ValidCommand_CreatesProductAndReturnsId()
    {
        var command = new CreateProductCommand("Laptop", 999.99m, "SKU-001");
        var result = await _sut.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _repoMock.Verify(r => r.Add(It.Is<Product>(p => p.Name == "Laptop")), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // Test 2: validation failure
    [Theory]
    [InlineData("", 10, "SKU")]
    [InlineData("Name", -1, "SKU")]
    [InlineData("Name", 10, "")]
    public async Task Handle_InvalidInput_ReturnsFailure(string name, decimal price, string sku)
    {
        var validator = new CreateProductCommandValidator();
        var cmd = new CreateProductCommand(name, price, sku);
        var result = await validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
    }

    // Test 3: duplicate SKU
    [Fact]
    public async Task Handle_DuplicateSku_ReturnsFailure()
    {
        _repoMock.Setup(r => r.ExistsBySkuAsync("SKU-DUP", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        var command = new CreateProductCommand("Product", 100m, "SKU-DUP");
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("SKU");
        _repoMock.Verify(r => r.Add(It.IsAny<Product>()), Times.Never);
    }
}
```

### Angular — Service
```typescript
describe('ProductService', () => {
  let service: ProductService;            // chưa có class này → compile error OK
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [ProductService]
    });
    service = TestBed.inject(ProductService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should create product and return id', () => {
    const expectedId = '123e4567-e89b-12d3-a456-426614174000';
    service.createProduct({ name: 'Laptop', price: 999.99, sku: 'SKU-001' }).subscribe(id => {
      expect(id).toBe(expectedId);
    });
    const req = httpMock.expectOne('/api/products');
    expect(req.request.method).toBe('POST');
    req.flush(expectedId);
  });
});
```

---

## Bước 3 — Run Tests → Confirm RED

```bash
# .NET
dotnet test --filter "CreateProductCommandHandlerTests" -- xunit.diagnostics=true

# Angular
ng test --include="**/product.service.spec.ts" --watch=false
```

Tests fail → đúng rồi, tiếp tục.

---

## Bước 4 — Write Minimum Implementation (GREEN)

Viết đủ code để tests pass — không hơn.

```csharp
// Command record
public record CreateProductCommand(string Name, decimal Price, string Sku) : IRequest<Result<Guid>>;

// Validator
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
    }
}

// Handler — minimum implementation để tests pass
public class CreateProductCommandHandler(IProductRepository repo, IUnitOfWork uow)
    : IRequestHandler<CreateProductCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateProductCommand cmd, CancellationToken ct)
    {
        if (await repo.ExistsBySkuAsync(cmd.Sku, ct))
            return Result.Failure<Guid>($"SKU '{cmd.Sku}' already exists");

        var product = Product.Create(cmd.Name, cmd.Price, cmd.Sku);
        repo.Add(product);
        await uow.CommitAsync(ct);
        return Result.Success(product.Id);
    }
}
```

---

## Bước 5 — Run Tests → Confirm GREEN

```bash
dotnet test --filter "CreateProductCommandHandlerTests"
# All tests pass → GREEN
```

---

## Bước 6 — Refactor (REFACTOR)

Code pass rồi — giờ clean up mà không phá tests.

- Extract constants / magic values thành named variables
- Rename cho rõ nghĩa
- Extract helper methods nếu handler quá dài
- Verify tests vẫn pass sau refactor

```bash
dotnet test  # chạy toàn bộ test suite sau refactor
```

---

## Checklist mỗi vòng lặp

- [ ] Test viết trước implementation
- [ ] Tests fail đúng lý do (không fail vì typo)
- [ ] Implementation minimal — không viết code không có test cover
- [ ] Tất cả tests pass sau implementation
- [ ] Refactor xong → tests vẫn pass
- [ ] Không có `// TODO` hay partial implementation

---

## Prompt Template
```
/tdd [FeatureName]

Business rules:
1. [rule 1]
2. [rule 2]

Test cases cần cover:
- Happy path: [mô tả]
- Not found: [mô tả]
- Validation: [field, rule]
- Business rule violation: [mô tả]

Stack: [.NET handler | Angular service | Domain entity]
Dependencies cần mock: [list interfaces]
```
