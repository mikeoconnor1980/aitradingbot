<#
.SYNOPSIS
    Builds the TradingApp Execution Agent installer package.

.DESCRIPTION
    Publishes the Worker project as a self-contained single-file executable,
    copies install/uninstall scripts alongside it, and optionally creates
    a ZIP archive ready for distribution to clients.

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER NoZip
    Skip creating the ZIP archive.

.EXAMPLE
    .\build-installer.ps1
    .\build-installer.ps1 -NoZip
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$NoZip
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path "$PSScriptRoot\..\..\").Path
$workerProject = Join-Path $repoRoot "src\TradingApp.Worker\TradingApp.Worker.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\worker"
$packageDir = Join-Path $repoRoot "artifacts\installer\TradingApp-ExecutionAgent"
$scriptDir = $PSScriptRoot

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Building Execution Agent Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# --- 1. Clean previous output ---
if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
}
if (Test-Path $packageDir) {
    Remove-Item -Path $packageDir -Recurse -Force
}

# --- 2. Publish ---
Write-Host "Publishing Worker (self-contained, single-file, win-x64)..." -ForegroundColor Green

dotnet publish $workerProject `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit 1
}

Write-Host "Published to: $publishDir" -ForegroundColor Green

# --- 3. Assemble installer package ---
Write-Host "Assembling installer package..." -ForegroundColor Green

New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

# Copy publishedFiles
Get-ChildItem -Path $publishDir -File | Copy-Item -Destination $packageDir

# Copy install/uninstall scripts
Copy-Item -Path (Join-Path $scriptDir "install.ps1") -Destination $packageDir
Copy-Item -Path (Join-Path $scriptDir "uninstall.ps1") -Destination $packageDir

# Copy README
$readmeSrc = Join-Path $scriptDir "README.md"
if (Test-Path $readmeSrc) {
    Copy-Item -Path $readmeSrc -Destination $packageDir
}

Write-Host "Package assembled at: $packageDir" -ForegroundColor Green

# --- 4. List package contents ---
Write-Host ""
Write-Host "Package contents:" -ForegroundColor Cyan
Get-ChildItem -Path $packageDir | ForEach-Object {
    $size = if ($_.Length -gt 1MB) { "{0:N1} MB" -f ($_.Length / 1MB) }
            elseif ($_.Length -gt 1KB) { "{0:N0} KB" -f ($_.Length / 1KB) }
            else { "$($_.Length) B" }
    Write-Host ("  {0,-45} {1,10}" -f $_.Name, $size) -ForegroundColor White
}

$totalSize = (Get-ChildItem -Path $packageDir -Recurse | Measure-Object -Property Length -Sum).Sum
Write-Host ""
Write-Host ("  Total: {0:N1} MB" -f ($totalSize / 1MB)) -ForegroundColor Cyan

# --- 5. Create ZIP ---
if (-not $NoZip) {
    $version = "0.1.0"
    # Try to read version from csproj
    $csprojContent = Get-Content $workerProject -Raw
    if ($csprojContent -match '<Version>([^<]+)</Version>') {
        $version = $Matches[1]
    }

    $zipName = "TradingApp-ExecutionAgent-v$version-win-x64.zip"
    $zipPath = Join-Path (Split-Path $packageDir -Parent) $zipName

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Write-Host ""
    Write-Host "Creating ZIP: $zipName" -ForegroundColor Green
    Compress-Archive -Path "$packageDir\*" -DestinationPath $zipPath -CompressionLevel Optimal

    $zipSize = (Get-Item $zipPath).Length
    Write-Host ("ZIP created: {0:N1} MB" -f ($zipSize / 1MB)) -ForegroundColor Green
    Write-Host "Path: $zipPath" -ForegroundColor White
}

# --- 6. Summary ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Build Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  To install on a client machine:" -ForegroundColor White
Write-Host "    1. Copy the package folder (or extract ZIP) to the client" -ForegroundColor Gray
Write-Host "    2. Open PowerShell as Administrator" -ForegroundColor Gray
Write-Host "    3. Run: .\install.ps1" -ForegroundColor Gray
Write-Host ""
