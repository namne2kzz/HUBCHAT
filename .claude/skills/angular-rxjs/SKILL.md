---
name: angular-rxjs
description: Use when reviewing or writing RxJS code in Angular for HTTP/WebSocket/event streams — when to use RxJS vs Signals, and bridging via toSignal()/toObservable().
---

# Skill: Angular RxJS Patterns

RxJS usage in Angular v19+ — focused on HTTP, routing, and stream operations.

## Usage
```
/angular-rxjs [review|optimize|pattern] [context]
```

## Philosophy in Angular v19+

> Use RxJS for **streams** (HTTP, WebSocket, events, routing), use **Signals** for **state**.
> Bridge them with `toSignal()` and `toObservable()`.

---

## HTTP Patterns

### Basic GET with error handling
```typescript
@Injectable({ providedIn: 'root' })
export class ProductService {
  private readonly http = inject(HttpClient);

  getProducts(filters: ProductFilters): Observable<Product[]> {
    return this.http.get<Product[]>('/api/products', { params: { ...filters } }).pipe(
      retry({ count: 2, delay: 1000 }),
      catchError(err => {
        console.error('Failed to load products', err);
        return throwError(() => new Error('Failed to load products'));
      })
    );
  }
}
```

### POST with optimistic update
```typescript
createProduct(dto: CreateProductDto): Observable<Product> {
  return this.http.post<Product>('/api/products', dto).pipe(
    tap(created => this._products.update(list => [...list, created])),
    catchError(err => {
      // rollback optimistic update if needed
      return throwError(() => err);
    })
  );
}
```

### Pagination + Search (debounced)
```typescript
readonly searchTerm = signal('');
readonly currentPage = signal(1);

readonly products$ = toObservable(this.searchTerm).pipe(
  debounceTime(300),
  distinctUntilChanged(),
  switchMap(term =>
    this.http.get<PagedResult<Product>>('/api/products', {
      params: { search: term, page: this.currentPage().toString() }
    })
  )
);

readonly products = toSignal(this.products$, { initialValue: null });
```

---

## Prompt Templates

### Review RxJS Usage
```
Review this Angular code for RxJS best practices:

**Code**:
[Paste component or service code]

Check for:
1. Memory leaks (missing takeUntilDestroyed / unsubscribe)
2. switchMap vs mergeMap vs concatMap — correct operator for use case
3. Nested subscribes (should use higher-order operators)
4. Missing error handling (no catchError)
5. Using Subject where Signal would be better
6. Missing distinctUntilChanged/debounceTime on user input streams
7. shareReplay without refCount (memory leak risk)

Provide:
- Issues found with severity
- Fixed code
- Explanation of each fix
```

### Optimize HTTP Stream
```
Optimize this Angular HTTP + RxJS code:

**Current Code**:
[Paste code]

**Performance Goal**: [e.g., "reduce API calls on search", "avoid race conditions"]

**Current Issues**:
- [Describe current problem]

Optimize using:
- switchMap for cancellable streams (search, navigation)
- concatMap for ordered sequential operations
- mergeMap for parallel independent calls
- debounceTime + distinctUntilChanged for user input
- shareReplay(1) for multicasting (be careful with memory)
- retry / retryWhen for transient failures

Provide optimized code with explanation.
```

### Pattern Selection
```
Help me choose the right RxJS pattern for:

**Scenario**: [Describe what you're trying to do]

**Data flow**:
- Source: [User input / Timer / HTTP / WebSocket / Event]
- Processing: [What needs to happen]
- Output: [Where result goes]

**Constraints**:
- Cancel previous if new arrives: [Yes/No]
- Must be sequential: [Yes/No]
- Multiple concurrent: [Yes/No]
- Must retry on failure: [Yes/No]

Recommend the correct operator(s) with code example.
```

---

## Operator Quick Reference

| Scenario | Operator | Why |
|----------|----------|-----|
| Cancel prev on new (search) | `switchMap` | Cancels in-flight request |
| Sequential (form submit) | `concatMap` | Queues, doesn't cancel |
| Parallel independent | `mergeMap` | All run concurrently |
| First only (init data) | `exhaustMap` | Ignores new while active |
| Combine latest values | `combineLatest` | Re-emits on any source change |
| Merge multiple streams | `merge` | Emits from any source |
| Transform + flatten | `mergeMap(obs$)` | Map to observable |
| Retry with delay | `retry({ count: 3, delay: 1000 })` | Transient error recovery |
| Timeout | `timeout(5000)` | Fail-fast on slow API |
| Share multicasted | `shareReplay(1)` | Cache last value for late subscribers |
| Cleanup on destroy | `takeUntilDestroyed()` | Angular 16+ preferred over takeUntil |

## Memory Leak Prevention

**Priority order — choose the highest applicable:**

1. **`toSignal()`** — best, fully automatic cleanup, integrates with Signals
2. **`takeUntilDestroyed(destroyRef)`** — for cases where you must subscribe manually
3. **`Subject` + `takeUntil`** — legacy pattern, avoid in new code

```typescript
@Component({ standalone: true, ... })
export class MyComponent {
  private readonly destroyRef = inject(DestroyRef);

  // ✅ Option 1: toSignal — zero manual cleanup
  readonly products = toSignal(this.productService.getProducts(), { initialValue: [] });

  // ✅ Option 2: takeUntilDestroyed — when toSignal is not applicable
  ngOnInit(): void {
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(event => this.onNavigate(event));
  }
}

// ✅ Service with Subject — always complete on destroy
@Injectable()
export class PollingService implements OnDestroy {
  private readonly stop$ = new Subject<void>();

  startPolling(): Observable<Data> {
    return interval(5000).pipe(
      switchMap(() => this.http.get<Data>('/api/data')),
      takeUntil(this.stop$)
    );
  }

  ngOnDestroy(): void {
    this.stop$.next();
    this.stop$.complete();  // ← must complete, not just next()
  }
}
```

### Memory-Safe Long-Lived Store Pattern
```typescript
@Injectable({ providedIn: 'root' })
export class AppStore {
  private readonly _data = signal<Data[]>([]);

  // Called on logout — clears memory in long-lived singleton
  reset(): void {
    this._data.set([]);
  }
}
```
