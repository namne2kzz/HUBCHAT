<#
.SYNOPSIS
  Rebuild and restart specific HUB services without touching the rest of the stack.

.PARAMETER Services
  One or more docker compose service names. Defaults to 'hub-view' when omitted.
  Valid names come from docker-compose.yml: postgres, minio, pgadmin, jaeger,
  otel-collector, dashboard-gateway, chat, realtime, notification, media,
  gateway, hub-view.

.PARAMETER NoWait
  Skip the "Press any key" pause at the end (useful in CI / automated pipelines).

.EXAMPLE
  .\rebuild.ps1
  .\rebuild.ps1 chat
  .\rebuild.ps1 dashboard-gateway hub-view
#>
param(
  [string[]]$Services = @('hub-view'),
  [switch]$NoWait
)

# ── Detect whether this console was opened just for this script ───────────────
# If the parent is explorer.exe, the window will close on exit — so we pause.
$_pauseOnExit = -not $NoWait.IsPresent
try {
  $ppid   = (Get-CimInstance Win32_Process -Filter "ProcessId = $PID" -ErrorAction Stop).ParentProcessId
  $parent = (Get-Process -Id $ppid -ErrorAction Stop).Name
  # Only force-pause when launched from Explorer; terminal users already keep their window.
  if ($parent -ne 'explorer') { $_pauseOnExit = $false }
} catch {
  # If WMI fails, stay safe and keep the pause.
}

function Pause-IfNeeded {
  if ($_pauseOnExit) {
    Write-Host ""
    Write-Host "  Press any key to close this window..." -ForegroundColor DarkGray
    try { $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown') } catch { Start-Sleep 3 }
  }
}

# ── Global error trap ─────────────────────────────────────────────────────────
# Catches unhandled exceptions (e.g. $ErrorActionPreference = Stop throwing).
trap {
  Write-Host ""
  Write-Host "  Unexpected error: $_" -ForegroundColor Red
  Write-Host ""
  Pause-IfNeeded
  exit 1
}

function Stop-WithError {
  param([string]$Message)
  Write-Host ""
  Write-Host "  ERROR: $Message" -ForegroundColor Red
  Write-Host ""
  Pause-IfNeeded
  exit 1
}

# ── Always operate on HUB's compose file ─────────────────────────────────────
Set-Location $PSScriptRoot

Write-Host ""
Write-Host "  HUB rebuild :: $($Services -join ', ')" -ForegroundColor Cyan
Write-Host "  Compose dir  :: $PSScriptRoot" -ForegroundColor DarkGray
Write-Host ""

# ── Validate service names ────────────────────────────────────────────────────
$known = docker compose config --services
if ($LASTEXITCODE -ne 0) {
  Stop-WithError "Cannot read docker-compose.yml. Is Docker Desktop running?"
}

$unknown = $Services | Where-Object { $known -notcontains $_ }
if ($unknown) {
  Write-Host "  Unknown service(s): $($unknown -join ', ')" -ForegroundColor Red
  Write-Host "  Available        : $($known -join ', ')" -ForegroundColor DarkGray
  Stop-WithError "Nothing was rebuilt."
}

# ── Build ─────────────────────────────────────────────────────────────────────
foreach ($svc in $Services) {
  Write-Host "  Building $svc ..." -ForegroundColor Yellow
  docker compose build $svc
  if ($LASTEXITCODE -ne 0) { Stop-WithError "Build failed for '$svc' (exit $LASTEXITCODE)." }
}

# ── Restart ───────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "  Starting services ..." -ForegroundColor Yellow
docker compose up -d @Services
if ($LASTEXITCODE -ne 0) { Stop-WithError "docker compose up failed (exit $LASTEXITCODE)." }

# ── Status ────────────────────────────────────────────────────────────────────
Write-Host ""
docker compose ps $Services
Write-Host ""
Write-Host "  Done." -ForegroundColor Green

Pause-IfNeeded
