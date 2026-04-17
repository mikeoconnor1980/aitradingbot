#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Uninstalls the TradePilot Execution Agent Windows Service.

.DESCRIPTION
    Stops the service, removes the service registration, and optionally
    deletes the install directory and private key environment variable.

.PARAMETER InstallDir
    Installation directory. Defaults to C:\Program Files\TradePilot\ExecutionAgent

.PARAMETER ServiceName
    The Windows Service name. Defaults to TradePilot.ExecutionAgent

.PARAMETER RemoveData
    Also delete the data directory (SQLite DB). Off by default to preserve trade history.

.PARAMETER RemoveKey
    Remove the Hyperliquid__PrivateKey environment variable.

.EXAMPLE
    .\uninstall.ps1
    .\uninstall.ps1 -RemoveData -RemoveKey
#>
[CmdletBinding()]
param(
    [string]$InstallDir = "C:\Program Files\TradePilot\ExecutionAgent",
    [string]$ServiceName = "TradePilot.ExecutionAgent",
    [switch]$RemoveData,
    [switch]$RemoveKey
)

$ErrorActionPreference = 'Stop'

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  TradePilot Execution Agent Uninstaller" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# --- 1. Stop and remove service ---
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    if ($existing.Status -ne 'Stopped') {
        Write-Host "Stopping service..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }

    Write-Host "Removing service registration..." -ForegroundColor Yellow
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
    Write-Host "Service removed." -ForegroundColor Green
}
else {
    Write-Host "Service '$ServiceName' not found. Skipping." -ForegroundColor Gray
}

# --- 2. Remove install directory ---
if (Test-Path $InstallDir) {
    if (-not $RemoveData) {
        # Preserve the data directory
        $dataDir = Join-Path $InstallDir "data"
        if (Test-Path $dataDir) {
            Write-Host "Preserving data directory: $dataDir" -ForegroundColor Yellow
            Write-Host "  (Use -RemoveData to also delete trade history)" -ForegroundColor Gray
        }

        # Remove everything except the data folder
        Get-ChildItem -Path $InstallDir -Exclude "data" | Remove-Item -Recurse -Force
        Write-Host "Application files removed (data preserved)." -ForegroundColor Green
    }
    else {
        Remove-Item -Path $InstallDir -Recurse -Force
        Write-Host "Install directory removed (including data)." -ForegroundColor Green
    }
}
else {
    Write-Host "Install directory not found: $InstallDir" -ForegroundColor Gray
}

# --- 3. Optionally remove private key ---
$envVarName = "Hyperliquid__PrivateKey"
if ($RemoveKey) {
    $existingKey = [Environment]::GetEnvironmentVariable($envVarName, 'Machine')
    if ($existingKey) {
        [Environment]::SetEnvironmentVariable($envVarName, $null, 'Machine')
        Write-Host "Private key environment variable removed." -ForegroundColor Green
    }
    else {
        Write-Host "No private key environment variable found." -ForegroundColor Gray
    }
}
else {
    Write-Host "Private key environment variable preserved." -ForegroundColor Yellow
    Write-Host "  (Use -RemoveKey to also remove it)" -ForegroundColor Gray
}

# --- 4. Summary ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Uninstall Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
