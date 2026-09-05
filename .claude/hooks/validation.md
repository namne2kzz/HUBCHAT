# Hook: Validation Checks

Input and output validation rules for code generation and review tasks.

## When to Apply
- When receiving requirements (validate inputs are complete)
- When reviewing generated code (validate outputs meet standards)
- When preparing PRs (validate PR is ready)

---

## Input Validation — Requirements Checklist

### Feature Request Validation
Before starting any feature, confirm:

```
Validate this feature request is ready for implementation:

**Feature**: [description]

Check:
1. User story complete? (As [role], I want [action] so that [value])
2. Acceptance criteria defined? (Given/When/Then or bullet list)
3. API contracts defined? (endpoint, request/response shape)
4. DB schema impact identified?
5. Auth/authorization requirements clear?
6. Performance requirements stated? (or "no special requirements")
7. Error cases documented?
8. Dependencies on other teams/services identified?

If any are missing, list what's needed before coding can begin.
```

### Bug Report Validation
```
Validate this bug report has enough info to fix:

1. Reproduction steps provided? (exact steps, not "sometimes fails")
2. Expected vs actual behavior stated?
3. Environment specified? (dev/staging/prod, version)
4. Error messages / stack traces included?
5. Impact assessed? (how many users affected, data corrupted?)
6. Recent changes that could be related? (last deploy date)

Missing info: [list]
```

---

## Output Validation — Generated Code Standards

### Clean Architecture Validation
```
Validate this generated code meets Clean Architecture rules:

[Paste generated code]

Check each class:
1. What layer does it belong to? (Domain/Application/Infrastructure/WebApi)
2. What does it import/depend on?
3. Are those dependencies allowed for this layer?
   - Domain: no external dependencies
   - Application: Domain interfaces only
   - Infrastructure: Application + Domain
   - WebApi: Application only (no Infrastructure direct)

Report any violations.
```

### Performance Validation
```
Validate this generated code for performance issues:

[Paste code]

Check:
- EF Core: AsNoTracking missing on reads?
- EF Core: N+1 risk (loop + DB call inside)?
- EF Core: Loading full entities when projection would do?
- Async: Any .Result / .Wait() blocking?
- Angular: Missing OnPush on display component?
- Angular: Missing track in @for?
- Redis: Cache set without TTL?

Report issues with severity.
```

### Security Validation
```
Validate this generated code for security issues:

[Paste code — especially API controllers and Angular forms]

Backend checks:
- Authorization attribute present?
- Input validated via FluentValidation?
- No raw string SQL injection risk?
- No sensitive data in logs?
- No hardcoded secrets?

Frontend checks:
- No [innerHTML] with unescaped user input?
- No token stored in localStorage?
- HTTP interceptor handles 401 correctly?
- No sensitive data in URL query params?

Report each finding with OWASP category if applicable.
```

---

## PR Readiness Validation
```
Validate this PR is ready to merge:

**PR Description**: [paste or describe]
**Files changed**: [list]

Check:
1. [ ] Tests present and meaningful
2. [ ] No TODOs or FIXMEs left uncommitted
3. [ ] No console.log / Debug.WriteLine left in code
4. [ ] Migration has Down() implemented
5. [ ] Breaking changes documented
6. [ ] Config changes added to Docker secrets / pipeline variables
7. [ ] Performance impact assessed
8. [ ] CHANGELOG.md updated (if user-facing change)

Status: [Ready | Needs work — list items]
```
