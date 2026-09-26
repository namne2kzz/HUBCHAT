---
name: generate-angular
description: Use when generating production-ready Angular code (standalone component, service, page, form, or interceptor) using Signals and this project's modern Angular conventions.
---

# Skill: Generate Angular v19 Code

Generate production-ready Angular v19 code using standalone components, Signals, and modern patterns.

## Usage
```
/generate-angular [type] [name] [context]
```

### Types
- `component` — Standalone component with Signals
- `service` — Injectable service with Signals state
- `page` — Route-level smart component (feature page)
- `form` — Reactive form component
- `interceptor` — HTTP interceptor
- `guard` — Route guard (functional)
- `pipe` — Custom pipe
- `directive` — Custom attribute directive
- `store` — Signal-based feature store

---

## Prompt Template

```
Generate Angular v19 TypeScript code:

**Type**: [component|service|page|form|interceptor|guard|pipe|directive|store]
**Name**: [ComponentName / ServiceName]
**Feature**: [Feature module this belongs to]

**Requirements**:
- [UI/behavior requirement 1]
- [UI/behavior requirement 2]

**Data/API**:
- Endpoint: [GET|POST|PUT|DELETE /api/resource]
- Request type: [DTO interface]
- Response type: [DTO interface]

**State needed**:
- [list of signals/state needed]

**Parent/child components**:
- Uses: [list of child components]
- Used by: [parent component or route]

Generate following Angular v19 standards:
- Standalone: true on all components
- Signals for all reactive state (signal, computed, effect)
- inject() instead of constructor injection
- @if/@for/@defer control flow (no *ngIf/*ngFor)
- OnPush change detection for display components
- TypeScript strict mode compliant
- ALWAYS separate files: templateUrl + styleUrl (never inline template strings)
- Generate ALL 4 files: .component.ts, .component.html, .component.scss, .component.spec.ts
- ALWAYS put interfaces/types in a dedicated models/ file, never inside service/store/component files
- JSDoc on every public method: description line, `@param` for each argument, `@returns` (omit for void)
```

---

## Examples

### Component
```
Generate Angular v19 component:
Type: component
Name: UserProfileCard
Feature: users

Requirements:
- Display user avatar, name, email, role badge
- Show edit button if canEdit input is true
- Emit editClicked output event
- Loading skeleton while data loads

Data: Input [user: UserDto], [canEdit: boolean], Output [editClicked: EventEmitter<void>]
```

### Service
```
Generate Angular v19 service:
Type: service
Name: ProductService
Feature: products

Requirements:
- Load paginated product list with filters (category, search, page)
- Create/update/delete product (admin only)
- Cache product list in signal, invalidate on mutation
- Expose loading and error signals

API:
- GET /api/products?category=&search=&page=
- POST /api/products
- PUT /api/products/:id
- DELETE /api/products/:id

State: products signal, isLoading signal, error signal, pagination signal
```

### Form
```
Generate Angular v19 reactive form:
Type: form
Name: CreateProductForm
Feature: products

Requirements:
- Fields: name (required, min 3), price (required, >0), category (select from enum), description (optional, max 500)
- Submit calls ProductService.create()
- Show validation errors inline on blur
- Disable submit while loading
- Reset form on success

Related service: ProductService
```

---

## Angular v19 Key Patterns

### Signals
```typescript
count = signal(0);                           // writable signal
doubled = computed(() => count() * 2);      // computed (read-only)
effect(() => console.log(count()));         // side effect on change
count.set(5);                               // set value
count.update(v => v + 1);                  // update based on current
```

### Control Flow
```html
@if (isLoggedIn()) { <app-dashboard /> }
@for (item of items(); track item.id) { <li>{{ item.name }}</li> }
@switch (status()) {
  @case ('active') { <span class="green">Active</span> }
  @case ('inactive') { <span class="red">Inactive</span> }
  @default { <span>Unknown</span> }
}
@defer (on viewport; prefetch on idle) { <app-heavy /> } @placeholder { <div>...</div> }
```

### Functional Guards
```typescript
export const authGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated() ? true : router.createUrlTree(['/login']);
};
```
