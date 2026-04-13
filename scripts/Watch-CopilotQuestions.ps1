<#
.SYNOPSIS
    Monitors VS Code Copilot agent activity and sends Teams notifications
    when the agent appears to be waiting for human input.

.DESCRIPTION
    Two modes:
    1. AUTO MODE (default): Watches for file system activity in your workspace.
       When activity stops for a configurable timeout after a burst, assumes
       the agent is waiting and sends a Teams notification.
    2. MANUAL MODE (-Manual): Sends a single "check in" notification immediately.
       Bind to a VS Code keybinding or run from terminal when stepping away.

.PARAMETER WebhookUrl
    Teams Incoming Webhook URL (Power Automate workflow URL).

.PARAMETER WorkspacePath
    Path to the workspace folder to monitor. Defaults to current directory.

.PARAMETER IdleTimeoutSeconds
    Seconds of inactivity after agent activity before sending notification.
    Default: 90

.PARAMETER Manual
    Send a single notification immediately and exit.

.PARAMETER Message
    Custom message for manual notifications.

.EXAMPLE
    # Auto mode - watches for idle periods
    .\Watch-CopilotQuestions.ps1 -WebhookUrl "https://prod-xx.westeurope.logic.azure.com/..."

    # Manual mode - send one notification now
    .\Watch-CopilotQuestions.ps1 -WebhookUrl "https://prod-xx.westeurope.logic.azure.com/..." -Manual

    # With custom idle timeout
    .\Watch-CopilotQuestions.ps1 -WebhookUrl "https://prod-xx.westeurope.logic.azure.com/..." -IdleTimeoutSeconds 60
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$WebhookUrl,

    [string]$WorkspacePath = (Get-Location).Path,

    [int]$IdleTimeoutSeconds = 90,

    [switch]$Manual,

    [string]$Message = "Copilot agent may need your input"
)

# --- Configuration ---
$debounceSeconds = 30        # Min seconds between notifications
$activityThreshold = 3       # Min file changes to count as "agent active"

# --- Validation ---
if ($WebhookUrl -notmatch '^https://') {
    Write-Error "WebhookUrl must be an HTTPS URL"
    exit 1
}

if (-not (Test-Path $WorkspacePath)) {
    Write-Error "Workspace path not found: $WorkspacePath"
    exit 1
}

# --- Teams notification function ---
function Send-TeamsNotification {
    param(
        [string]$Title,
        [string]$Body,
        [string]$Urgency = "default"
    )

    $color = if ($Urgency -eq "high") { "attention" } else { "default" }

    $card = @{
        type        = "message"
        attachments = @(
            @{
                contentType = "application/vnd.microsoft.card.adaptive"
                contentUrl  = $null
                content     = @{
                    '$schema' = "http://adaptivecards.io/schemas/adaptive-card.json"
                    type      = "AdaptiveCard"
                    version   = "1.4"
                    body      = @(
                        @{
                            type   = "TextBlock"
                            text   = $Title
                            weight = "Bolder"
                            size   = "Medium"
                            color  = $color
                        }
                        @{
                            type = "TextBlock"
                            text = $Body
                            wrap = $true
                        }
                        @{
                            type     = "TextBlock"
                            text     = "Workspace: $WorkspacePath"
                            isSubtle = $true
                            size     = "Small"
                            wrap     = $true
                        }
                        @{
                            type     = "TextBlock"
                            text     = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
                            isSubtle = $true
                            size     = "Small"
                        }
                    )
                }
            }
        )
    }

    $json = $card | ConvertTo-Json -Depth 10
    try {
        Invoke-RestMethod -Uri $WebhookUrl -Method Post -Body $json -ContentType "application/json" | Out-Null
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Notification sent to Teams" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Failed to send Teams notification: $_" -ForegroundColor Red
        return $false
    }
}

# --- Manual mode ---
if ($Manual) {
    Write-Host "Sending manual notification to Teams..."
    Send-TeamsNotification `
        -Title "Copilot Agent - Check In Needed" `
        -Body $Message `
        -Urgency "high"
    exit 0
}

# --- Auto mode: File system watcher ---
Write-Host "============================================" -ForegroundColor Cyan
Write-Host " Copilot Agent Activity Monitor" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Workspace:     $WorkspacePath"
Write-Host "Idle timeout:  $IdleTimeoutSeconds seconds"
Write-Host "Debounce:      $debounceSeconds seconds"
Write-Host ""
Write-Host "Watching for agent idle periods..." -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop."
Write-Host ""

# Track state
$script:changeCount = 0
$script:lastChangeTime = [DateTime]::MinValue
$script:lastNotifyTime = [DateTime]::MinValue
$script:agentActive = $false
$script:activityStartTime = [DateTime]::MinValue

# Set up file system watcher on the workspace
$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = $WorkspacePath
$watcher.IncludeSubdirectories = $true
$watcher.EnableRaisingEvents = $true
# Watch for file changes that agents typically cause
$watcher.NotifyFilter = [System.IO.NotifyFilters]::LastWrite -bor [System.IO.NotifyFilters]::FileName

# Filter out noise directories
$ignoreDirs = @('node_modules', '.git', 'bin', 'obj', 'artifacts', 'TestResults', '.angular')

$action = {
    $path = $Event.SourceEventArgs.FullPath
    $changeType = $Event.SourceEventArgs.ChangeType

    # Skip noise directories
    foreach ($dir in $ignoreDirs) {
        if ($path -like "*\$dir\*") { return }
    }

    # Skip temporary/lock files
    if ($path -match '\.(tmp|lock|swp|log)$') { return }

    $now = Get-Date
    $script:changeCount++
    $script:lastChangeTime = $now

    # Detect burst of activity (agent is working)
    if (-not $script:agentActive -and $script:changeCount -ge $activityThreshold) {
        $script:agentActive = $true
        $script:activityStartTime = $now
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Agent activity detected (files changing)" -ForegroundColor Cyan
    }
}

Register-ObjectEvent $watcher "Changed" -Action $action | Out-Null
Register-ObjectEvent $watcher "Created" -Action $action | Out-Null
Register-ObjectEvent $watcher "Renamed" -Action $action | Out-Null

# Also watch VS Code copilot logs for any output
$logWatcher = $null
$copilotLogDir = "$env:APPDATA\Code\logs"
if (Test-Path $copilotLogDir) {
    $logWatcher = New-Object System.IO.FileSystemWatcher
    $logWatcher.Path = $copilotLogDir
    $logWatcher.IncludeSubdirectories = $true
    $logWatcher.Filter = "*.log"
    $logWatcher.EnableRaisingEvents = $true

    $logAction = {
        $path = $Event.SourceEventArgs.FullPath
        if ($path -like "*copilot*" -or $path -like "*exthost*") {
            $now = Get-Date
            $script:lastChangeTime = $now
            if (-not $script:agentActive) {
                $script:changeCount++
                if ($script:changeCount -ge $activityThreshold) {
                    $script:agentActive = $true
                    $script:activityStartTime = $now
                    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Agent activity detected (copilot logs)" -ForegroundColor Cyan
                }
            }
        }
    }

    Register-ObjectEvent $logWatcher "Changed" -Action $logAction | Out-Null
}

# Main loop - check for idle periods
try {
    while ($true) {
        Start-Sleep -Seconds 5

        $now = Get-Date
        $idleSeconds = ($now - $script:lastChangeTime).TotalSeconds

        if ($script:agentActive -and $idleSeconds -ge $IdleTimeoutSeconds) {
            # Agent was active but has been idle for the timeout period
            $timeSinceNotify = ($now - $script:lastNotifyTime).TotalSeconds

            if ($timeSinceNotify -ge $debounceSeconds) {
                $duration = [math]::Round(($script:lastChangeTime - $script:activityStartTime).TotalMinutes, 1)

                Send-TeamsNotification `
                    -Title "Copilot Agent - Input May Be Needed" `
                    -Body "The agent was active for ~$duration min and has been idle for $IdleTimeoutSeconds seconds. It may be waiting for your response." `
                    -Urgency "high"

                $script:lastNotifyTime = $now
            }

            # Reset state
            $script:agentActive = $false
            $script:changeCount = 0
        }

        # Reset change count periodically if no sustained activity
        if (-not $script:agentActive -and $idleSeconds -gt 30) {
            $script:changeCount = 0
        }
    }
}
finally {
    # Cleanup
    $watcher.EnableRaisingEvents = $false
    $watcher.Dispose()
    if ($logWatcher) {
        $logWatcher.EnableRaisingEvents = $false
        $logWatcher.Dispose()
    }
    Write-Host "`nStopped watching." -ForegroundColor Yellow
}
