---
name: unit-testing-angular
description: Use when generating or reviewing Angular unit tests (Jasmine + TestBed + Signal testing) for components and services, including mocking with jasmine.createSpyObj.
---

# Skill: Angular Unit Testing

Unit testing patterns for Angular v19+ — Jasmine + TestBed + Signal testing.

## Usage
```
/unit-testing-angular [generate|review|mock|signal] [context]
```

## Stack
| Tool | Role |
|------|------|
| `Jasmine` | Test runner + `describe` / `it` / `expect` |
| `TestBed` | Angular DI container for tests |
| `jasmine.createSpyObj` | Mock services (spy on methods) |
| `HttpClientTestingModule` | Mock HTTP requests |
| `RouterTestingModule` | Mock Router + ActivatedRoute |

---

## Test File Structure

Each component ships its own spec file:
```
feature.component.ts
feature.component.spec.ts   ← always separate
```

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { OrderListComponent } from './order-list.component';
import { OrderService } from '../services/order.service';

describe('OrderListComponent', () => {
  let fixture: ComponentFixture<OrderListComponent>;
  let component: OrderListComponent;
  let orderServiceSpy: jasmine.SpyObj<OrderService>;

  beforeEach(async () => {
    // Create spy BEFORE TestBed — so you can reference it in providers
    orderServiceSpy = jasmine.createSpyObj('OrderService', ['loadOrders'], {
      // Mock signal properties as getters
      orders: signal([]),
      isLoading: signal(false),
      error: signal<string | null>(null),
    });

    await TestBed.configureTestingModule({
      imports: [OrderListComponent],           // standalone component → imports
      providers: [
        { provide: OrderService, useValue: orderServiceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(OrderListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();                  // triggers ngOnInit
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
```

---

## Mocking Services — jasmine.createSpyObj

### Basic method spy
```typescript
const serviceSpy = jasmine.createSpyObj('ProductService', [
  'loadProducts', 'createProduct', 'deleteProduct'
]);

// Make method return a resolved promise
serviceSpy.loadProducts.and.resolveTo(undefined);

// Make method return a value
serviceSpy.createProduct.and.resolveTo({ id: '1', name: 'Widget' });

// Make method throw
serviceSpy.deleteProduct.and.rejectWith(new Error('Not found'));
```

### Mocking signal properties (Angular v19+)
```typescript
// Use the third argument of createSpyObj for property descriptors
const orderServiceSpy = jasmine.createSpyObj('OrderService', ['loadOrders', 'reset'], {
  orders: signal<Order[]>([]),        // writable signal — test can update it
  isLoading: signal(false),
  error: signal<string | null>(null),
  totalOrders: signal(0),
});

// In a test — update the signal to simulate service state
(orderServiceSpy.orders as WritableSignal<Order[]>).set([
  { id: '1', customerName: 'Alice', total: 100 },
]);
fixture.detectChanges();              // re-render with new signal value
```

### Mocking Router
```typescript
import { Router } from '@angular/router';

const routerSpy = jasmine.createSpyObj('Router', ['navigate']);
routerSpy.navigate.and.resolveTo(true);

// In providers
{ provide: Router, useValue: routerSpy }

// Assert navigation
expect(routerSpy.navigate).toHaveBeenCalledWith(['/orders', '123']);
```

### Mocking ActivatedRoute (route params)
```typescript
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';

providers: [
  {
    provide: ActivatedRoute,
    useValue: {
      snapshot: { paramMap: { get: (key: string) => '123' } },
      params: of({ id: '123' }),
    },
  },
]
```

---

## HTTP Testing — HttpClientTestingModule

```typescript
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';

describe('OrderService', () => {
  let service: OrderService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [OrderService],
    });
    service = TestBed.inject(OrderService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());   // no pending requests left

  it('loadOrders — should call GET /api/orders and update signal', async () => {
    const mockOrders: Order[] = [
      { id: '1', customerName: 'Alice', total: 200, status: 'Pending' },
    ];

    const promise = service.loadOrders();

    const req = httpMock.expectOne('/api/orders');
    expect(req.request.method).toBe('GET');
    req.flush(mockOrders);              // simulate server response

    await promise;

    expect(service.orders()).toEqual(mockOrders);
    expect(service.isLoading()).toBeFalse();
    expect(service.error()).toBeNull();
  });

  it('loadOrders — on HTTP error, sets error signal', async () => {
    const promise = service.loadOrders();

    const req = httpMock.expectOne('/api/orders');
    req.flush('Server Error', { status: 500, statusText: 'Internal Server Error' });

    await promise;

    expect(service.error()).toBe('Failed to load orders');
    expect(service.orders()).toEqual([]);
  });
});
```

---

## Signal Testing

Signals are synchronous — just call the signal, no async needed.

```typescript
describe('Signal state', () => {
  it('isLoading — true while fetching, false after', async () => {
    const loadingStates: boolean[] = [];
    const sub = toObservable(service.isLoading).subscribe(v => loadingStates.push(v));

    const promise = service.loadOrders();
    const req = httpMock.expectOne('/api/orders');
    req.flush([]);
    await promise;

    expect(loadingStates).toEqual([false, true, false]);
    sub.unsubscribe();
  });

  it('computed totalOrders — reflects orders count', () => {
    (service.orders as WritableSignal<Order[]>).set([
      buildOrder(), buildOrder(), buildOrder(),
    ]);
    expect(service.totalOrders()).toBe(3);
  });
});
```

---

## Component Template Tests

Test what the user sees — rendered HTML.

```typescript
it('shows loading spinner while isLoading is true', () => {
  (orderServiceSpy.isLoading as WritableSignal<boolean>).set(true);
  fixture.detectChanges();

  const spinner = fixture.nativeElement.querySelector('app-spinner');
  expect(spinner).not.toBeNull();
});

it('shows order cards when orders are loaded', () => {
  (orderServiceSpy.orders as WritableSignal<Order[]>).set([
    buildOrder({ customerName: 'Alice' }),
    buildOrder({ customerName: 'Bob' }),
  ]);
  fixture.detectChanges();

  const cards = fixture.nativeElement.querySelectorAll('app-order-card');
  expect(cards.length).toBe(2);
});

it('shows empty state when orders list is empty', () => {
  (orderServiceSpy.orders as WritableSignal<Order[]>).set([]);
  fixture.detectChanges();

  const empty = fixture.nativeElement.querySelector('[data-testid="empty-state"]');
  expect(empty).not.toBeNull();
});

it('shows error message when error signal is set', () => {
  (orderServiceSpy.error as WritableSignal<string | null>).set('Failed to load orders');
  fixture.detectChanges();

  const errorEl = fixture.nativeElement.querySelector('app-error-message');
  expect(errorEl).not.toBeNull();
  expect(errorEl.getAttribute('ng-reflect-message')).toContain('Failed to load orders');
});
```

---

## Interaction Tests

Test what happens when the user clicks.

```typescript
it('calls loadOrders on init', () => {
  orderServiceSpy.loadOrders.and.resolveTo(undefined);
  fixture.detectChanges();             // triggers ngOnInit

  expect(orderServiceSpy.loadOrders).toHaveBeenCalledOnceWith();
});

it('navigates to order detail on row click', () => {
  (orderServiceSpy.orders as WritableSignal<Order[]>).set([buildOrder({ id: '42' })]);
  fixture.detectChanges();

  const row = fixture.nativeElement.querySelector('[data-testid="order-row"]');
  row.click();

  expect(routerSpy.navigate).toHaveBeenCalledWith(['/orders', '42']);
});
```

---

## Test Data Builders (TypeScript)

```typescript
// tests/builders/order.builder.ts
import type { Order } from '../models/order.model';

let counter = 0;

export function buildOrder(overrides: Partial<Order> = {}): Order {
  counter++;
  return {
    id: `order-${counter}`,
    customerName: `Customer ${counter}`,
    total: counter * 100,
    status: 'Pending',
    createdAt: new Date().toISOString(),
    ...overrides,
  };
}

export function buildOrderList(count: number, overrides: Partial<Order> = {}): Order[] {
  return Array.from({ length: count }, () => buildOrder(overrides));
}
```

Usage in tests:
```typescript
import { buildOrder, buildOrderList } from '../../tests/builders/order.builder';

// Single with defaults
const order = buildOrder();

// Single with overrides
const pendingOrder = buildOrder({ status: 'Pending', customerName: 'Alice' });

// List of 5
const orders = buildOrderList(5);
```

---

## Prompt Templates

### Generate Component Tests
```
Generate Jasmine unit tests for this Angular v19+ component:

**Component Code** (component.ts):
[Paste component class]

**Template** (component.html):
[Paste template — or describe what it renders]

**Service dependencies**:
- [List services injected — e.g., OrderService with signals: orders, isLoading, error]

**What to test**:
1. Component creation
2. ngOnInit — which service methods are called
3. Template: loading state renders spinner
4. Template: loaded state renders correct items count
5. Template: empty state shows empty message
6. Template: error state shows error message
7. User interaction: click/submit → correct service method called + correct argument
8. Signal updates → template re-renders

Use:
- jasmine.createSpyObj with signal property mocks (third arg)
- WritableSignal cast to update signals in tests
- fixture.detectChanges() after each signal update
- data-testid selectors for DOM queries
- buildXxx() builder functions for test data
```

### Generate Service Tests
```
Generate Jasmine unit tests for this Angular service:

**Service Code**:
[Paste service class]

**API endpoints used** (for HttpClientTestingModule mocking):
- [List: GET /api/orders, POST /api/orders, etc.]

**Signal state** (if applicable):
- [List signals: orders, isLoading, error]

Test:
1. Successful HTTP call → signals updated correctly
2. HTTP error → error signal set, data signal unchanged
3. isLoading: true during call, false after
4. Correct HTTP method and URL
5. Correct request body for POST/PUT
6. afterEach: httpMock.verify() — no pending requests
```

### Review Angular Tests
```
Review these Angular unit tests for quality:

**Test Code**:
[Paste spec file]

Check for:
1. Missing afterEach(httpMock.verify()) for HTTP tests
2. No fixture.detectChanges() after signal changes
3. Querying DOM before detectChanges (stale snapshot)
4. Signals not cast to WritableSignal before .set()
5. Tests depending on execution order (shared mutable state)
6. Missing negative cases (error state, empty state)
7. Assertions on implementation details, not visible behavior

Output: Issues + fixed code
```

---

## Quick Reference

| Task | Code |
|------|------|
| Create spy with methods | `jasmine.createSpyObj('S', ['method1', 'method2'])` |
| Create spy with signal props | `jasmine.createSpyObj('S', ['m'], { orders: signal([]) })` |
| Spy resolves async | `spy.method.and.resolveTo(value)` |
| Spy rejects async | `spy.method.and.rejectWith(new Error())` |
| Update signal in test | `(spy.orders as WritableSignal<T[]>).set([...])` |
| Re-render after change | `fixture.detectChanges()` |
| Query DOM element | `fixture.nativeElement.querySelector('selector')` |
| Query all elements | `fixture.nativeElement.querySelectorAll('selector')` |
| Assert called once | `expect(spy.method).toHaveBeenCalledOnceWith(args)` |
| Assert not called | `expect(spy.method).not.toHaveBeenCalled()` |
| Mock HTTP response | `req.flush(data)` |
| Mock HTTP error | `req.flush('err', { status: 500, statusText: '...' })` |
| Verify no pending HTTP | `httpMock.verify()` in afterEach |
