# Hook: Post-Generation Review

Chạy checklist này SAU khi Claude generate code — trước khi copy vào project.

## Quick Prompt
```
Review the code you just generated against .claude/hooks/post-gen.md.
Fix all failures inline. Show final code only.
```

---

## Common (áp dụng cho mọi code)

**Design & SOLID**
- [ ] Một class/component = một responsibility
- [ ] Phụ thuộc vào interfaces, không phải concrete types
- [ ] Không `new ConcreteService()` trong business logic — luôn inject
- [ ] Không if/else chain trên type — dùng Strategy pattern

**Documentation**
- [ ] Mọi public method có doc comment (`/// <summary>` hoặc `/** */`)
- [ ] Mọi param được document kể cả `CancellationToken`
- [ ] Non-void methods có `<returns>` / `@returns` kể cả null cases

---

## Backend (.NET)

**Layer compliance**
- [ ] Không import EF Core trong Domain, không DbContext trong Application
- [ ] Namespace khớp folder structure

**Code quality**
- [ ] `async Task` + `CancellationToken ct` trên mọi I/O method
- [ ] `AsNoTracking()` trên read queries
- [ ] Không `.Result` / `.Wait()` blocking
- [ ] `using` / `await using` cho mọi `IDisposable`
- [ ] Không `new HttpClient()` — dùng `IHttpClientFactory`
- [ ] `DbContext` không inject vào Singleton services

**Security**
- [ ] Không hardcode credentials hay connection strings
- [ ] Không log sensitive data (password, token, PII)
- [ ] `[Authorize]` trên controller actions

**Tests** (xUnit + NSubstitute + Shouldly)
- [ ] Cover: happy path · not-found · validation failure · exception propagation
- [ ] `Verify()` confirm side effects (repo.Add, uow.Commit called/not called)
- [ ] Test data qua Bogus Faker / Builder — không magic strings
- [ ] `[Theory] + [InlineData]` cho multi-value validation cases

---

## Angular (v20+)

**Modern patterns**
- [ ] `standalone: true`, `imports[]` đầy đủ
- [ ] `inject()` — không constructor injection
- [ ] `@if` / `@for` / `@defer` — không `*ngIf` / `*ngFor`
- [ ] `track` expression trong mọi `@for`
- [ ] `OnPush` trên display components

**Signals & Memory**
- [ ] `signal()` / `computed()` / `effect()` cho state — không BehaviorSubject trong components
- [ ] Không bare `subscribe()` — dùng `takeUntilDestroyed()` hoặc `toSignal()`
- [ ] `Subject` / `BehaviorSubject` gọi `.complete()` trong `ngOnDestroy`

**TypeScript**
- [ ] Không implicit `any`
- [ ] Null checks với `?.` và `??` ở đúng chỗ
- [ ] Interface/type đúng cho API responses

**Tests** (Jasmine + TestBed)
- [ ] Services mock qua `jasmine.createSpyObj` với signal props
- [ ] `fixture.detectChanges()` sau mỗi signal update
- [ ] `httpMock.verify()` trong `afterEach`
- [ ] Cover: creation · loading/error/empty/loaded states · user interactions
