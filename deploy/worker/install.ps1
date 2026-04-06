#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs the TradingApp Execution Agent as a Windows Service.

.DESCRIPTION
    Copies files to the install directory, prompts for the Hyperliquid private key,
    sets it as a machine-level environment variable, registers and starts the Windows Service.

.PARAMETER InstallDir
    Target installation directory. Defaults to C:\Program Files\TradingApp\ExecutionAgent

.PARAMETER ServiceName
    The Windows Service name. Defaults to TradingApp.ExecutionAgent

.PARAMETER NoStart
    Register the service but don't start it immediately.

.EXAMPLE
    .\install.ps1
    .\install.ps1 -InstallDir "D:\TradingAgent"
    .\install.ps1 -NoStart
#>
[CmdletBinding()]
param(
    [string]$InstallDir = "C:\Program Files\TradingApp\ExecutionAgent",
    [string]$ServiceName = "TradingApp.ExecutionAgent",
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'

$exeName = "TradingApp.ExecutionAgent.exe"
$displayName = "TradingApp Execution Agent"
$description = "Executes trading strategies on Hyperliquid. Private key never leaves this machine."

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  TradingApp Execution Agent Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# --- 1. Determine source directory (script location) ---
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$sourceExe = Join-Path $scriptDir $exeName

if (-not (Test-Path $sourceExe)) {
    Write-Error "Cannot find $exeName in $scriptDir. Run build-installer.ps1 first."
    exit 1
}

# --- 2. Stop existing service if upgrading ---
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Existing service found (Status: $($existing.Status)). Stopping..." -ForegroundColor Yellow
    if ($existing.Status -ne 'Stopped') {
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
    Write-Host "Previous installation removed." -ForegroundColor Yellow
}

# --- 3. Copy files to install directory ---
Write-Host "Installing to: $InstallDir" -ForegroundColor Green

if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# Create data subdirectory for SQLite DB
$dataDir = Join-Path $InstallDir "data"
if (-not (Test-Path $dataDir)) {
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
}

# Create logs subdirectory
$logsDir = Join-Path $InstallDir "logs"
if (-not (Test-Path $logsDir)) {
    New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
}

# Copy all published files (exe + appsettings + pdb if present)
Get-ChildItem -Path $scriptDir -File | ForEach-Object {
    if ($_.Name -ne 'install.ps1' -and $_.Name -ne 'uninstall.ps1') {
        Copy-Item -Path $_.FullName -Destination $InstallDir -Force
    }
}

Write-Host "Files copied." -ForegroundColor Green

# --- 4. Configure private key ---
$envVarName = "Hyperliquid__PrivateKey"
$existingKey = [Environment]::GetEnvironmentVariable($envVarName, 'Machine')

if ([string]::IsNullOrWhiteSpace($existingKey)) {
    Write-Host ""
    Write-Host "--- Private Key Configuration ---" -ForegroundColor Cyan
    Write-Host "Your Hyperliquid private key is required to sign orders." -ForegroundColor White
    Write-Host "It will be stored as a machine-level environment variable." -ForegroundColor White
    Write-Host "The key NEVER leaves this machine." -ForegroundColor Yellow
    Write-Host ""

    $key = Read-Host "Enter your Hyperliquid private key (0x...)"

    if ([string]::IsNullOrWhiteSpace($key)) {
        Write-Warning "No private key provided. You must set $envVarName before starting the service."
    }
    else {
        # Validate basic format
        if ($key -notmatch '^0x[0-9a-fA-F]{64}$') {
            Write-Warning "Key does not match expected format (0x + 64 hex chars). Setting anyway."
        }

        [Environment]::SetEnvironmentVariable($envVarName, $key, 'Machine')
        Write-Host "Private key stored as machine environment variable." -ForegroundColor Green
    }
}
else {
    Write-Host "Existing private key found in environment. Keeping current value." -ForegroundColor Green
}

# --- 5. Register Windows Service ---
$exePath = Join-Path $InstallDir $exeName

Write-Host ""
Write-Host "Registering Windows Service: $ServiceName" -ForegroundColor Green

sc.exe create $ServiceName `
    binPath= "`"$exePath`"" `
    start= delayed-auto `
    DisplayName= "$displayName" | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create service. Exit code: $LASTEXITCODE"
    exit 1
}

sc.exe description $ServiceName "$description" | Out-Null

# Set recovery: restart after 30s on first failure, 60s on second, 120s on subsequent
sc.exe failure $ServiceName reset= 86400 actions= restart/30000/restart/60000/restart/120000 | Out-Null

Write-Host "Service registered with delayed-auto start and auto-recovery." -ForegroundColor Green

# --- 6. Start service ---
if (-not $NoStart) {
    Write-Host "Starting service..." -ForegroundColor Green
    Start-Service -Name $ServiceName

    Start-Sleep -Seconds 2
    $svc = Get-Service -Name $ServiceName
    if ($svc.Status -eq 'Running') {
        Write-Host "Service is running." -ForegroundColor Green
    }
    else {
        Write-Warning "Service status: $($svc.Status). Check Event Viewer > Application for errors."
    }
}
else {
    Write-Host "Service registered but not started (use -NoStart was specified)." -ForegroundColor Yellow
    Write-Host "Start manually: Start-Service -Name '$ServiceName'" -ForegroundColor Yellow
}

# --- 7. Summary ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Installation Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Install Dir : $InstallDir" -ForegroundColor White
Write-Host "  Service Name: $ServiceName" -ForegroundColor White
Write-Host "  Data Dir    : $dataDir" -ForegroundColor White
Write-Host "  Logs Dir    : $logsDir" -ForegroundColor White
Write-Host ""
Write-Host "  Config file : $InstallDir\appsettings.json" -ForegroundColor White
Write-Host "  Edit strategy settings in appsettings.json, then restart:" -ForegroundColor Gray
Write-Host "    Restart-Service -Name '$ServiceName'" -ForegroundColor Gray
Write-Host ""
Write-Host "  To view logs: Get-EventLog -LogName Application -Source 'TradingApp.ExecutionAgent' -Newest 20" -ForegroundColor Gray
Write-Host "  To uninstall: Run uninstall.ps1 as Administrator" -ForegroundColor Gray
Write-Host ""
