---
description: Run the pre-commit health-check gate (build, tests, format, EF migrations, dependency vulnerabilities) and produce a pass/fail summary.
argument-hint: [scope: full | build | test | format | migration]
---

# Workflow: Health Check

Pre-commit gate — verify build, tests, format, migrations, và code quality đều xanh.

> Lưu ý: HUB.VIEW (Angular) chưa scaffold — bỏ qua các bước `ng ...` cho tới khi có frontend. Solution build bằng `dotnet build HUB.slnx`.

## Usage
```
/health-check [scope: full | build | test | format | migration]
```

---

## Bước 1 — Build Status

### .NET
```bash
cd src

# Build tất cả projects
dotnet build --configuration Release --no-restore 2>&1

# Check build warnings nghiêm trọng
dotnet build --configuration Release --no-restore 2>&1 |
    Select-String "warning|error" |
    Where-Object { $_ -notmatch "^Build succeeded" }
```

### Angular
```bash
cd frontend

# Production build (strict mode)
ng build --configuration production 2>&1

# Type check without build
npx tsc --noEmit 2>&1
```

**Pass criteria:** Exit code 0, không có `error` output.

---

## Bước 2 — Test Suite

### .NET Unit Tests
```bash
dotnet test tests/Unit/ `
    --configuration Release `
    --no-build `
    --logger "console;verbosity=normal" `
    --collect:"XPlat Code Coverage" `
    2>&1 | Tee-Object -Variable testOutput

$testOutput | Select-String "Failed|Passed|Skipped|Error"
```

### .NET Integration Tests (cần Docker)
```bash
dotnet test tests/Integration/ `
    --configuration Release `
    --no-build `
    --logger "console;verbosity=minimal" `
    2>&1
```

### Angular Tests
```bash
ng test --watch=false --browsers=ChromeHeadless --code-coverage 2>&1 |
    Select-String "FAILED|SUCCESS|ERROR"
```

**Pass criteria:** 0 failed tests. Skipped tests phải có documented reason.

---

## Bước 3 — Code Format

### .NET — EditorConfig
```bash
# Check only (không thay đổi file)
dotnet format --verify-no-changes --severity error 2>&1

# Nếu fail → auto-fix và list files thay đổi
dotnet format
git diff --name-only
```

### Angular — Prettier + ESLint
```bash
# Prettier check
npx prettier --check "src/**/*.{ts,html,scss}" 2>&1

# ESLint
ng lint 2>&1 | Select-String "error|warning"
```

**Pass criteria:** `dotnet format --verify-no-changes` exit 0. `ng lint` 0 errors.

---

## Bước 4 — Migration Status

```bash
# Kiểm tra có pending migrations chưa được apply
dotnet ef migrations list --project src/Services/Chat/HUB.Chat.Infrastructure --startup-project src/Services/Chat/HUB.Chat.WebApi

# Verify migration scripts syntax (dry run)
dotnet ef database update --no-connect --project src/Services/Chat/HUB.Chat.Infrastructure `
    --startup-project src/Services/Chat/HUB.Chat.WebApi 2>&1

# Kiểm tra model changes chưa có migration
dotnet ef migrations has-pending-model-changes `
    --project src/Services/Chat/HUB.Chat.Infrastructure `
    --startup-project src/Services/Chat/HUB.Chat.WebApi 2>&1
```

**Pass criteria:** Không có pending model changes. Tất cả migrations đã được apply trên dev DB.

---

## Bước 5 — Dependency Check

```bash
# .NET — outdated / vulnerable packages
dotnet list package --vulnerable 2>&1 | Select-String "has the following vulnerable"

# Angular
npm audit --audit-level=high 2>&1
```

**Pass criteria:** Không có `High` hoặc `Critical` vulnerabilities.

---

## Bước 6 — Summary Report

```
## Health Check — [timestamp]

| Check              | Status | Notes                          |
|--------------------|--------|--------------------------------|
| .NET Build         | ✅ PASS |                                |
| Angular Build      | ✅ PASS |                                |
| Unit Tests (47)    | ✅ PASS | 0 failed, 2 skipped (see #123) |
| Integration Tests  | ✅ PASS | Testcontainers — PostgreSQL OK |
| Angular Tests      | ✅ PASS |                                |
| dotnet format      | ✅ PASS | No changes needed              |
| ng lint            | ⚠️ WARN | 2 warnings — non-blocking      |
| EF Migrations      | ✅ PASS | All applied                    |
| NuGet Vulnerabilities | ✅ PASS |                             |
| npm audit          | ✅ PASS |                                |

Overall: ✅ READY TO MERGE
```

---

## Quick Health Check (30 seconds)

```bash
# One-liner cho fast feedback trước commit
dotnet build --no-restore -q && dotnet test --no-build -q && dotnet format --verify-no-changes -q
echo "All good!" 
```

---

## Prompt Template
```
/health-check

Scope: [full | build only | tests only]
Changed files: [list hoặc "all"]
Branch: [branch name]

Chạy tất cả checks và output summary table.
Nếu có failure: chỉ rõ file:line và suggest fix.
```
