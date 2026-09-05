# ─────────────────────────────────────────────────────────────────────────────
# HUB — tạo EF Core migrations cho 3 services.
# Chạy từ thư mục gốc C:\DEV\HUB
# Prerequisites: dotnet tool install --global dotnet-ef
# ─────────────────────────────────────────────────────────────────────────────

Set-Location $PSScriptRoot

function Add-MigrationIfNeeded {
    param($label, $project, $startup, $context)
    Write-Host "`n$label ..." -ForegroundColor Cyan
    $existing = dotnet ef migrations list --project $project --startup-project $startup --context $context 2>&1
    if ($existing -match 'InitialCreate') {
        Write-Host "  [skip] InitialCreate already exists." -ForegroundColor DarkGray
        return
    }
    dotnet ef migrations add InitialCreate `
        --project $project --startup-project $startup `
        --output-dir Persistence/Migrations --context $context
    if ($LASTEXITCODE -ne 0) { Write-Error "$label migration failed"; exit 1 }
}

Add-MigrationIfNeeded "[1/3] chat-service (hub_chat)" `
    "src/Services/Chat/HUB.Chat.Infrastructure" `
    "src/Services/Chat/HUB.Chat.WebApi" `
    "ChatDbContext"

Add-MigrationIfNeeded "[2/3] notification-service (hub_notif)" `
    "src/Services/Notification/HUB.Notification.Infrastructure" `
    "src/Services/Notification/HUB.Notification.WebApi" `
    "NotificationDbContext"

Add-MigrationIfNeeded "[3/3] media-service (hub_media)" `
    "src/Services/Media/HUB.Media.Infrastructure" `
    "src/Services/Media/HUB.Media.WebApi" `
    "MediaDbContext"

Write-Host "`nDone! Migrations ready." -ForegroundColor Green
Write-Host "Services auto-apply them on startup (Database.MigrateAsync)." -ForegroundColor Gray
