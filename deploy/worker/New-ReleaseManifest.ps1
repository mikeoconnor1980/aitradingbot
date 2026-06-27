[CmdletBinding()]
param(
    [string]$InstallerDirectory = (Join-Path (Resolve-Path "$PSScriptRoot\..\..").Path "artifacts\installer"),
    [string]$OutputPath,
    [DateTimeOffset]$PublishedAtUtc = [DateTimeOffset]::UtcNow,
    [string]$MinimumSupportedVersion = "",
    [string]$ReleaseNotes = ""
)

$ErrorActionPreference = 'Stop'

if (-not $PSBoundParameters.ContainsKey('OutputPath')) {
    $OutputPath = Join-Path $InstallerDirectory 'latest.json'
}

if (-not (Test-Path $InstallerDirectory)) {
    throw "Installer directory not found: $InstallerDirectory"
}

$exe = Get-ChildItem -Path $InstallerDirectory -File |
    Where-Object { $_.Name -match '^TradePilot-ExecutionAgent-v(?<version>.+)-Setup\.exe$' } |
    Sort-Object Name -Descending |
    Select-Object -First 1

if (-not $exe) {
    throw "Could not find a setup executable in $InstallerDirectory"
}

$versionMatch = [regex]::Match($exe.Name, '^TradePilot-ExecutionAgent-v(?<version>.+)-Setup\.exe$')
if (-not $versionMatch.Success) {
    throw "Could not extract a version from $($exe.Name)"
}

$version = $versionMatch.Groups['version'].Value
$zipName = "TradePilot-ExecutionAgent-v$version-win-x64.zip"
$sha256Name = "$($exe.Name).sha256"

$zip = Get-Item -LiteralPath (Join-Path $InstallerDirectory $zipName) -ErrorAction Stop
$sha256File = Get-Item -LiteralPath (Join-Path $InstallerDirectory $sha256Name) -ErrorAction Stop

$exeSha256 = (Get-FileHash -Path $exe.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$zipSha256 = (Get-FileHash -Path $zip.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$sha256ArtifactSha256 = (Get-FileHash -Path $sha256File.FullName -Algorithm SHA256).Hash.ToLowerInvariant()

$expectedSha256Content = "$exeSha256  $($exe.Name)"
$actualSha256Content = (Get-Content -LiteralPath $sha256File.FullName -Raw).Trim()
if ($actualSha256Content -ne $expectedSha256Content) {
    throw "SHA256 artifact content does not match $($exe.Name). Expected '$expectedSha256Content' but found '$actualSha256Content'."
}

$minimumVersion = if ([string]::IsNullOrWhiteSpace($MinimumSupportedVersion)) {
    $version
} else {
    $MinimumSupportedVersion
}

$releaseNotesValue = if ([string]::IsNullOrWhiteSpace($ReleaseNotes)) {
    $null
} else {
    $ReleaseNotes
}

$manifest = [ordered]@{
    version = $version
    publishedAtUtc = $PublishedAtUtc.ToUniversalTime().ToString('O')
    releaseNotes = $releaseNotesValue
    minimumSupportedVersion = $minimumVersion
    files = [ordered]@{
        exe = [ordered]@{
            filename = $exe.Name
            blobName = "v$version/$($exe.Name)"
            contentType = 'application/octet-stream'
            sizeBytes = $exe.Length
            sha256 = $exeSha256
        }
        zip = [ordered]@{
            filename = $zip.Name
            blobName = "v$version/$($zip.Name)"
            contentType = 'application/zip'
            sizeBytes = $zip.Length
            sha256 = $zipSha256
        }
    }
    artifacts = [ordered]@{
        sha256 = [ordered]@{
            filename = $sha256File.Name
            blobName = "v$version/$($sha256File.Name)"
            contentType = 'text/plain'
            sizeBytes = $sha256File.Length
            sha256 = $sha256ArtifactSha256
        }
    }
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$manifestJson = $manifest | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($OutputPath, $manifestJson, [System.Text.UTF8Encoding]::new($false))

Write-Host "Release manifest written to $OutputPath" -ForegroundColor Green