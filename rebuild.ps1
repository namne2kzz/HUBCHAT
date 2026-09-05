<#
.SYNOPSIS
  Rebuild and restart specific HUB services without touching the rest of the stack.

.PARAMETER Services
  One or more docker compose service names. Defaults to 'hub-view' when omitted.
  Valid names come from docker-compose.yml: postgres, minio, pgadmin, jaeger,
  otel-collector, dashboard-gateway, chat, realtime, notification, media,
  gateway, hub-view.

.EXAMPLE
  .\rebuild.ps1
  .\rebuild.ps1 chat
  .\rebuild.ps1 dashboard-gateway hub-view
#>
param(
  [string[]]$Services = @('hub-view')
)

$ErrorActionPreference = 'Stop'

# Always operate on HUB's compose file, no matter where the script is invoked from.
Set-Location $PSScriptRoot

function Stop-WithError {
  param([string]$Message)
  Write-Host ""
  Write-Host "  $Message" -ForegroundColor Red
  Write-Host ""
  # Keep the window open when launched by double-click / "Run with PowerShell".
  if ([Environment]::UserInteractive) {
    try { Read-Host "  Press Enter to close" | Out-Null } catch { }
  }
  exit 1
}

Write-Host ""
Write-Host "  HUB rebuild :: $($Services -join ', ')" -ForegroundColor Cyan
Write-Host "  Compose dir  :: $PSScriptRoot" -ForegroundColor DarkGray
Write-Host ""

# Validate service names up front so a typo fails with a readable message.
$known = docker compose config --services
if ($LASTEXITCODE -ne 0) {
  Stop-WithError "Cannot read docker-compose.yml. Is Docker Desktop running?"
}

$unknown = $Services | Where-Object { $known -notcontains $_ }
if ($unknown) {
  Write-Host "  Unknown service(s): $($unknown -join ', ')" -ForegroundColor Red
  Write-Host "  Available: $($known -join ', ')" -ForegroundColor DarkGray
  Stop-WithError "Nothing was rebuilt."
}

foreach ($svc in $Services) {
  Write-Host "  Building $svc ..." -ForegroundColor Yellow
  docker compose build $svc
  if ($LASTEXITCODE -ne 0) { Stop-WithError "Build failed for $svc (exit $LASTEXITCODE)." }
}

Write-Host ""
Write-Host "  Starting services ..." -ForegroundColor Yellow
docker compose up -d @Services
if ($LASTEXITCODE -ne 0) { Stop-WithError "docker compose up failed (exit $LASTEXITCODE)." }

Write-Host ""
docker compose ps $Services
Write-Host ""
Write-Host "  Done." -ForegroundColor Green
Write-Host ""
