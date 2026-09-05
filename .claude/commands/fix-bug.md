---
description: Run the systematic bug investigation workflow (evidence → root cause → fix → regression test) for a full-stack bug.
argument-hint: [bug-description]
---

# Workflow: Fix Bug

Systematic bug investigation and fix workflow for full-stack issues.

## Usage
```
/fix-bug [bug-description]
```

---

## Workflow Steps

### Step 1 — Gather Evidence
```
Diagnose this bug:

**Symptoms**:
[Describe what's happening — error messages, wrong behavior]

**Expected behavior**:
[Describe what should happen]

**Reproduction steps**:
1. [Step 1]
2. [Step 2]
3. [Observed: X, Expected: Y]

**Environment**:
- Layer: [Backend | Frontend | Database | All]
- Frequency: [Always | Intermittent | Under specific conditions]
- Introduced in: [Version / PR / date if known]

**Error output** (paste any of these available):
- Stack trace / exception:
- Browser console error:
- OpenTelemetry/Jaeger log:
- Database query log:
- Network request/response:
```

### Step 2 — Root Cause Analysis
```
Analyze this bug — find root cause:

**Bug Summary**: [from Step 1]
**Evidence**: [Stack traces, errors from Step 1]

**Code involved**:
[Paste relevant code — handler, service, component, SQL]

Determine:
1. Is this a business logic bug (wrong domain rule)?
2. Is this a data bug (bad data / missing migration)?
3. Is this a concurrency bug (race condition)?
4. Is this an async/await bug (deadlock, fire-and-forget)?
5. Is this an EF Core bug (N+1, wrong tracking)?
6. Is this an Angular bug (signal mutation, missing track)?
7. Is this a configuration bug (wrong connection string, missing DI)?

Provide:
- Root cause in one clear sentence
- Why this bug occurs (the mechanism)
- Why it might have been missed
```

### Step 3 — Fix
```
Fix this bug:

**Root cause** (from Step 2): [description]
**Code to fix**:
[Paste code]

Provide:
1. Fixed code
2. Explanation of the fix
3. Any side effects of the fix
4. Regression test to prevent recurrence
```

### Step 4 — Verify & Prevent
```
Write regression test for this bug:

**Bug**: [description]
**Fix applied**: [describe the fix]
**Code after fix**: [paste]

Provide:
1. Unit test that would have caught this bug BEFORE the fix fails, and AFTER the fix passes
2. Integration test if the bug was in a system interaction
3. Any monitoring/alerting to detect this class of bug in future
```

---

## Quick Debug Prompts

### Slow Query
```
This API endpoint is slow ([X] ms). Here's the relevant code:
[Paste handler + EF Core query]

OpenTelemetry/Jaeger shows: [paste slow dependency call details]

Use /query-optimization skill to diagnose.
```

### Angular Rendering Issue
```
This Angular component is not updating when expected:
[Paste component code]

Signal reads in template: [list them]
What triggers state change: [describe]

Check: signal mutation pattern, OnPush trigger, computed deps, effect cleanup.
```

### Async Deadlock
```
This async method deadlocks intermittently:
[Paste async C# code]

Check for:
- .Result or .Wait() blocking
- Missing ConfigureAwait(false) in library code
- HttpContext accessed from background thread
- DbContext accessed from multiple threads
```
