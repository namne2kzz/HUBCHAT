# PreToolUse hook — runs before every Write tool call
# Receives Claude tool event as JSON via stdin
# Exit 0 = allow write, Exit 2 = BLOCK write (shows error to Claude)

param()

$event = $input | ConvertFrom-Json -ErrorAction SilentlyContinue
if ($null -eq $event) { exit 0 }

$file = $event.tool_input.file_path
if ([string]::IsNullOrWhiteSpace($file)) { exit 0 }

$fileLower = $file.ToLower()

# Block writing secrets to sensitive config files
$blockedPatterns = @(
    '\.env$',
    '\.env\.',
    'secrets\.json$',
    'appsettings\.production\.json$',
    'appsettings\.staging\.json$',
    'appsettings\.prod\.json$'
)

foreach ($pattern in $blockedPatterns) {
    if ($fileLower -match $pattern) {
        $msg = "BLOCKED: Writing to '$file' is not allowed.`n" +
               "Secrets must be stored in Azure Key Vault or .NET user-secrets (dev only).`n" +
               "Run: dotnet user-secrets set `"Key`" `"Value`" --project src/YourApp.WebApi"
        Write-Error $msg
        exit 2
    }
}

# Warn (but allow) writing to appsettings.Development.json
if ($fileLower -match 'appsettings\.development\.json$') {
    Write-Warning "[hook] Writing to appsettings.Development.json — never put real secrets here. Use user-secrets instead."
}

exit 0
