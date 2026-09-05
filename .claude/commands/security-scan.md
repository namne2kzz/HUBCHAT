---
description: Run a full security scan (dependency CVEs, secrets detection, OWASP Top 10, auth/authz audit) before merge or deploy.
argument-hint: [scope: full | deps | secrets | code | auth]
---

# Workflow: Security Scan

Quét toàn diện vulnerabilities trước khi merge hoặc deploy — dependencies, secrets, OWASP, auth.

## Usage
```
/security-scan [scope: full | deps | secrets | code | auth]
```

---

## Bước 1 — Dependency Vulnerabilities

### .NET
```bash
# Quét CVE trong NuGet packages
dotnet list package --vulnerable --include-transitive

# Xem outdated packages
dotnet tool install -g dotnet-outdated-tool   # một lần
dotnet outdated

# Fix: upgrade theo semantic versioning
dotnet add package [PackageName] --version [SafeVersion]
```

**Kết quả cần đạt:** Không có severity `High` hoặc `Critical`. `Medium` phải có documented reason nếu chưa fix được.

### Angular / Node
```bash
npm audit
npm audit --json | ConvertFrom-Json | Select-Object -ExpandProperty vulnerabilities

# Auto-fix minor/patch
npm audit fix

# Fix breaking changes — review trước
npm audit fix --force
```

---

## Bước 2 — Secrets Detection

```bash
# Scan toàn bộ code cho hardcoded secrets
git grep -rn -E "(password|pwd|secret|apikey|api_key|connectionstring)\s*=\s*['\"][^'\"]{4,}" `
    --include="*.cs" --include="*.ts" --include="*.json" --include="*.yaml" --include="*.yml"

# Scan git history (cẩn thận — secrets cũ đã commit vẫn nguy hiểm)
git log --all --oneline -50
git grep -rn "password" $(git rev-list --all) 2>$null | Select-Object -First 20

# Check appsettings files
Get-Content src/**/appsettings*.json | Select-String -Pattern "password|secret|key" -CaseSensitive:$false
```

**Red flags:**
- `"Password": "actual_password"` trong `appsettings.json`
- `ConnectionString` với credentials
- `ApiKey` hardcoded trong `*.ts` hoặc `*.cs`
- `.env` file committed

---

## Bước 3 — OWASP Top 10 Code Review

### A01 — Broken Access Control (IDOR)
```bash
# Tìm endpoints không có [Authorize]
grep -rn "public.*ActionResult\|public.*IActionResult" src/ --include="*.cs" |
    grep -v "\[Authorize\]\|\[AllowAnonymous\]"

# Tìm queries không filter theo current user
grep -rn "\.Where(" src/ --include="*.cs" | grep -v "UserId\|CustomerId\|OwnerId"
```

### A02 — Cryptographic Failures
```bash
# Tìm weak hashing
grep -rn "MD5\|SHA1\b\|SHA256.*password" src/ --include="*.cs"

# Tìm HTTP (non-HTTPS) hardcoded
grep -rn '"http://' src/ --include="*.cs" --include="*.ts" --include="*.json"
```

### A03 — Injection
```bash
# Raw SQL với string interpolation
grep -rn 'FromSqlRaw\|ExecuteSqlRaw\|ExecuteSqlCommand' src/ --include="*.cs"

# Angular innerHTML binding
grep -rn "\[innerHTML\]\|bypassSecurityTrust" src/ --include="*.ts" --include="*.html"
```

### A05 — Security Misconfiguration
```bash
# CORS wildcard
grep -rn 'AllowAnyOrigin\|WithOrigins\("\*"\)' src/ --include="*.cs"

# Stack trace in responses
grep -rn 'UseDeveloperExceptionPage\|exception\.StackTrace' src/ --include="*.cs"
```

### A07 — Identification & Authentication
```bash
# JWT config
grep -A 10 "AddJwtBearer\|JwtBearerOptions" src/ --include="*.cs"
# Verify: ValidateIssuer, ValidateAudience, ValidateLifetime = true

# Password policy
grep -rn "PasswordOptions\|RequireDigit\|RequiredLength" src/ --include="*.cs"
```

---

## Bước 4 — Auth & Authorization Audit

```bash
# Controllers không có [Authorize]
grep -rn "\[ApiController\]" src/ --include="*.cs" -l |
    ForEach-Object { 
        if (-not (Select-String -Path $_ -Pattern "\[Authorize")) { Write-Host "NO AUTH: $_" }
    }

# Angular routes không có guards
grep -rn "path:" src/ --include="*.ts" | grep -v "canActivate\|canActivateChild\|login\|register\|public"
```

---

## Bước 5 — Report & Remediation

Output format:

```
## Security Scan Report — [Date]

### Critical (fix before merge)
- [ ] CVE-2024-XXXX in PackageName v1.0 — upgrade to v1.1
- [ ] Hardcoded API key in src/Services/PaymentService.cs:42
- [ ] SQL injection risk: raw query in OrderRepository.cs:89

### High
- [ ] Missing [Authorize] on AdminController.cs
- [ ] CORS allows all origins in Program.cs

### Medium
- [ ] npm: lodash < 4.17.21 — prototype pollution (low exploitability in this context)

### Passed
- [x] No secrets in git history (last 100 commits)
- [x] JWT validation fully configured
- [x] All Angular routes protected with AuthGuard
```

---

## Prompt Template
```
/security-scan

Scope: [full | chỉ deps | chỉ secrets | chỉ code]
Branch/PR: [branch name hoặc PR link]
Recent changes: [mô tả những gì vừa thay đổi]

Chạy:
1. dotnet list package --vulnerable
2. npm audit
3. Secrets grep trong changed files
4. OWASP review cho các endpoints mới
5. Auth coverage check

Output: Security report với severity levels và fix recommendations.
```
