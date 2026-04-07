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
    [switch]$NoZip,
    [switch]$NoInnoSetup
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

# --- 5. Read version from csproj ---
$version = "0.1.0"
$csprojContent = Get-Content $workerProject -Raw
if ($csprojContent -match '<Version>([^<]+)</Version>') {
    $version = $Matches[1]
}

# --- 6. Create Inno Setup installer ---
if (-not $NoInnoSetup) {
    $issScript = Join-Path $scriptDir "installer.iss"
    $iscc = $null
    $toolsDir = Join-Path $repoRoot ".tools\InnoSetup"

    # Search for ISCC.exe: system install -> local .tools -> PATH
    $isccPaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        (Join-Path $toolsDir "ISCC.exe")
    )
    foreach ($p in $isccPaths) {
        if (Test-Path $p) { $iscc = $p; break }
    }

    if (-not $iscc) {
        $iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
    }

    # Auto-download Inno Setup if not found anywhere
    if (-not $iscc) {
        Write-Host ""
        Write-Host "Inno Setup not found. Downloading portable copy..." -ForegroundColor Yellow

        $innoInstallerUrl = "https://jrsoftware.org/download.php/is.exe"
        $innoInstallerPath = Join-Path $env:TEMP "innosetup-installer.exe"

        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -Uri $innoInstallerUrl -OutFile $innoInstallerPath -UseBasicParsing

            Write-Host "Installing Inno Setup to .tools/InnoSetup/ ..." -ForegroundColor Yellow
            New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null

            # Silent install to local .tools directory (no Start Menu, no desktop icon)
            # /CURRENTUSER avoids requiring admin elevation
            $innoLog = Join-Path $env:TEMP "innosetup-install.log"
            Start-Process -FilePath $innoInstallerPath -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /CURRENTUSER /NORESTART /DIR=`"$toolsDir`" /NOICONS /LOG=`"$innoLog`"" -Wait -NoNewWindow

            if ((Test-Path (Join-Path $toolsDir "ISCC.exe"))) {
                $iscc = Join-Path $toolsDir "ISCC.exe"
                Write-Host "Inno Setup installed to: $toolsDir" -ForegroundColor Green
            } else {
                Write-Warning "Inno Setup install completed but ISCC.exe not found. Check $innoLog"
            }
        } catch {
            Write-Warning "Failed to download Inno Setup: $_"
        } finally {
            if (Test-Path $innoInstallerPath) {
                Remove-Item $innoInstallerPath -Force -ErrorAction SilentlyContinue
            }
        }
    }

    if ($iscc -and (Test-Path $issScript)) {
        Write-Host ""
        Write-Host "Building Inno Setup installer..." -ForegroundColor Green

        & $iscc "/DAppVersion=$version" $issScript

        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Inno Setup compilation failed (exit code $LASTEXITCODE). Skipping installer EXE."
        } else {
            $installerName = "TradingApp-ExecutionAgent-v$version-Setup.exe"
            $installerDir = Join-Path $repoRoot "artifacts\installer"
            $installerPath = Join-Path $installerDir $installerName

            if (Test-Path $installerPath) {
                # Generate SHA256 hash file
                $hash = (Get-FileHash -Path $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
                $hashFile = "$installerPath.sha256"
                "$hash  $installerName" | Set-Content -Path $hashFile -NoNewline

                $installerSize = (Get-Item $installerPath).Length
                Write-Host ("Installer created: {0} ({1:N1} MB)" -f $installerName, ($installerSize / 1MB)) -ForegroundColor Green
                Write-Host "SHA256: $hash" -ForegroundColor White
            }
        }
    } else {
        if (-not $iscc) {
            Write-Host ""
            Write-Host "Inno Setup could not be downloaded. Skipping installer EXE creation." -ForegroundColor Yellow
        }
        if (-not (Test-Path $issScript)) {
            Write-Warning "installer.iss not found at $issScript"
        }
    }
}

# --- 7. Create ZIP ---
if (-not $NoZip) {
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

# --- 8. Summary ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Build Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Option A: Inno Setup installer (recommended for clients)" -ForegroundColor White
Write-Host "    Double-click the Setup EXE from artifacts/installer/" -ForegroundColor Gray
Write-Host ""
Write-Host "  Option B: Manual PowerShell install" -ForegroundColor White
Write-Host "    1. Copy the package folder (or extract ZIP) to the client" -ForegroundColor Gray
Write-Host "    2. Open PowerShell as Administrator" -ForegroundColor Gray
Write-Host "    3. Run: .\install.ps1" -ForegroundColor Gray
Write-Host ""
