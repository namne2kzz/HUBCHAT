---
name: angular-signals
description: Use when designing, reviewing, or converting Angular state to Signals (signal/computed/effect, signal-based stores) for reactive state management.
---

# Skill: Angular Signals — Reactive State

Master Angular Signals for reactive state management in Angular v19+.

## Usage
```
/angular-signals [review|design|convert|store] [context]
```

---

## Signal Primitives

### Core APIs
```typescript
// Writable signal
const count = signal(0);
count();           // read — returns 0
count.set(5);      // set — now 5
count.update(v => v + 1);  // update — now 6

// Computed (lazy, cached, re-evaluates when deps change)
const doubled = computed(() => count() * 2);

// Effect (side effect, runs when signals it reads change)
effect(() => {
  console.log('count changed:', count());
  // Auto-cleanup on component destroy when created in injection context
});
```

### Signal-based Component Store
```typescript
@Injectable()
export class CartStore {
  // State
  private readonly _items = signal<CartItem[]>([]);
  private readonly _isLoading = signal(false);
  private readonly _error = signal<string | null>(null);

  // Derived
  readonly items = this._items.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly totalPrice = computed(() =>
    this._items().reduce((sum, item) => sum + item.price * item.quantity, 0)
  );
  readonly itemCount = computed(() =>
    this._items().reduce((sum, item) => sum + item.quantity, 0)
  );

  // Actions
  addItem(item: CartItem): void {
    this._items.update(items => {
      const existing = items.find(i => i.id === item.id);
      if (existing) {
        return items.map(i => i.id === item.id
          ? { ...i, quantity: i.quantity + 1 } : i);
      }
      return [...items, { ...item, quantity: 1 }];
    });
  }

  removeItem(id: string): void {
    this._items.update(items => items.filter(i => i.id !== id));
  }

  clear(): void {
    this._items.set([]);
  }
}
```

---

## Prompt Templates

### Review Signal Usage
```
Review this Angular component/service for proper Signals usage:

**Code**:
[Paste component or service code]

Check for:
1. BehaviorSubject that could be replaced with signal
2. Manual subscription management (missing takeUntilDestroyed)
3. Missing computed for derived state (recalculating in template)
4. Missing effect() for side effects triggered by state changes
5. Mutating signal values directly (push/pop instead of update)
6. Signal read in non-reactive context (outside template/computed/effect)

Provide:
- Issues found
- Refactored code using proper signals
- Performance implications
```

### Convert BehaviorSubject → Signals
```
Convert this BehaviorSubject-based state to Angular Signals:

**Current Code** (BehaviorSubject version):
[Paste code with BehaviorSubject]

Convert to:
- signal() for state
- computed() for derived values
- effect() for side effects
- Keep RxJS only for HTTP calls (use toSignal() to bridge)
- Remove all subscribe()/unsubscribe() manual management

Maintain: same public API shape so consumers don't break
```

### Design Feature Store
```
Design a Signal-based feature store for:

**Feature**: [Feature name]
**State needed**:
- [List of state items and their types]

**Derived state** (computed):
- [Computed value 1] = [derivation formula]
- [Computed value 2] = [derivation formula]

**Actions** (state mutations):
- [Action 1]: [how it changes state]
- [Action 2]: [how it changes state]

**Side effects** (effect):
- When [state X changes]: [do Y]

**HTTP operations**:
- [Method]: [endpoint] → updates [state]

Provide:
1. Injectable store class with full Signal implementation
2. HTTP integration using toSignal() or async/await
3. Error and loading state handling
4. Usage example in a component
```

---

## toSignal — Bridge RxJS → Signals

```typescript
@Injectable({ providedIn: 'root' })
export class RouteParamService {
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);

  // Convert Observable to Signal
  readonly productId = toSignal(
    this.route.paramMap.pipe(map(p => p.get('id'))),
    { initialValue: null }
  );
}

// In component — HTTP result as signal
readonly products = toSignal(
  this.http.get<Product[]>('/api/products'),
  { initialValue: [] }
);
```

## Signal Inputs (Angular 17+)
```typescript
@Component({...})
export class ProductCardComponent {
  // Signal inputs — reactive by default
  product = input.required<Product>();
  showActions = input(true);  // with default

  // Computed from input signal
  displayName = computed(() => this.product().name.toUpperCase());
}
```

## Model Signals (Two-way binding, Angular 17+)
```typescript
@Component({...})
export class ToggleComponent {
  value = model(false);  // supports [(value)]="someSignal"
}
```
