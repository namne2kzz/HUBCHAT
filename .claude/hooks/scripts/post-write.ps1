# PostToolUse hook — runs after every Write or Edit tool call
# Receives Claude tool event as JSON via stdin
# Exit 0 = success, non-zero = warning (does NOT block Claude)

param()

$event = $input | ConvertFrom-Json -ErrorAction SilentlyContinue
if ($null -eq $event) { exit 0 }

$file = $event.tool_input.file_path
if ([string]::IsNullOrWhiteSpace($file)) { exit 0 }

# .NET: run dotnet format on changed .cs file
if ($file -match '\.cs$') {
    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        Write-Host "[hook] Formatting $file"
        dotnet format --include $file --severity error 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "[hook] dotnet format reported issues in $file"
        }
    }
}

# Angular: notify on .ts/.html changes (ng lint is slow — run manually)
elseif ($file -match '\.(ts|html|scss)$') {
    Write-Host "[hook] Angular file changed: $(Split-Path $file -Leaf) — run 'ng lint' to verify"
}

# EF Core migration: remind to update snapshot
elseif ($file -match 'Migrations.*\.cs$' -and $file -notmatch 'Snapshot') {
    Write-Host "[hook] Migration file written — run 'dotnet ef database update' on dev DB"
}

exit 0
