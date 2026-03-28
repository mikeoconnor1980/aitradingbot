param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [string]$Symbol = 'BTC',

    [string[]]$Intervals = @('15m'),

    [switch]$SkipHyperliquid,

    [switch]$SkipDatabaseChecks,

    [string]$DatabasePath = 'data/tradingapp.db'
)

$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)

    Write-Host ''
    Write-Host "=== $Message ===" -ForegroundColor Cyan
}

function Invoke-JsonPost {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri,

        [Parameter(Mandatory = $true)]
        [object]$Body
    )

    $json = $Body | ConvertTo-Json -Depth 10
    Write-Host "POST $Uri" -ForegroundColor DarkGray
    Write-Host $json -ForegroundColor DarkGray

    return Invoke-RestMethod -Method Post -Uri $Uri -ContentType 'application/json' -Body $json
}

function Show-Object {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Title,

        [Parameter(Mandatory = $true)]
        [object]$Value
    )

    Write-Host "`n$Title" -ForegroundColor Yellow
    $Value | ConvertTo-Json -Depth 10
}

function Get-Sqlite3Path {
    $command = Get-Command sqlite3 -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        return $null
    }

    return $command.Source
}

function Invoke-SqliteQuery {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Database,

        [Parameter(Mandatory = $true)]
        [string]$Query
    )

    $sqlite3 = Get-Sqlite3Path
    if ($null -eq $sqlite3) {
        Write-Warning 'sqlite3 was not found on PATH. Skipping database verification.'
        return
    }

    if (-not (Test-Path $Database)) {
        Write-Warning "Database file not found: $Database. Skipping database verification."
        return
    }

    & $sqlite3 $Database $Query
}

$normalizedBaseUrl = $BaseUrl.TrimEnd('/')

Write-Step '1. Binance candle ingestion'
$binanceBody = @{
    symbol = $Symbol
    intervals = $Intervals
}
$binanceResult1 = Invoke-JsonPost -Uri "$normalizedBaseUrl/api/candles/ingest/binance" -Body $binanceBody
Show-Object -Title 'First Binance candle ingestion result' -Value $binanceResult1

Write-Step '2. Binance candle ingestion idempotency rerun'
$binanceResult2 = Invoke-JsonPost -Uri "$normalizedBaseUrl/api/candles/ingest/binance" -Body $binanceBody
Show-Object -Title 'Second Binance candle ingestion result' -Value $binanceResult2

Write-Step '3. Binance mark-price candle ingestion'
$binanceMarkBody = @{
    symbol = $Symbol
    intervals = $Intervals
    includeMarkPrice = $true
}
$binanceMarkResult = Invoke-JsonPost -Uri "$normalizedBaseUrl/api/candles/ingest/binance" -Body $binanceMarkBody
Show-Object -Title 'Binance mark-price ingestion result' -Value $binanceMarkResult

Write-Step '4. Funding-rate ingestion'
$fundingBody = @{
    symbol = $Symbol
}
$fundingResult = Invoke-JsonPost -Uri "$normalizedBaseUrl/api/funding/ingest" -Body $fundingBody
Show-Object -Title 'Funding-rate ingestion result' -Value $fundingResult

if (-not $SkipHyperliquid) {
    Write-Step '5. Hyperliquid candle ingestion smoke test'
    $hyperliquidBody = @{
        symbol = $Symbol
        intervals = $Intervals
    }
    $hyperliquidResult = Invoke-JsonPost -Uri "$normalizedBaseUrl/api/candles/ingest" -Body $hyperliquidBody
    Show-Object -Title 'Hyperliquid ingestion result' -Value $hyperliquidResult
}

if (-not $SkipDatabaseChecks) {
    Write-Step '6. SQLite verification'

    Write-Host 'Candles by source and interval:' -ForegroundColor Yellow
    Invoke-SqliteQuery -Database $DatabasePath -Query "select Source, Symbol, Interval, count(*) from Candles where Symbol = '$Symbol' group by Source, Symbol, Interval order by Source, Interval;"

    Write-Host "`nFunding-rate coverage:" -ForegroundColor Yellow
    Invoke-SqliteQuery -Database $DatabasePath -Query "select Symbol, count(*), min(Timestamp), max(Timestamp) from FundingRates where Symbol = '$Symbol' group by Symbol;"
}

Write-Step '7. Quick review guidance'
Write-Host 'Check that Binance 15m candles extend back to around 2019 and that repeated ingestion does not create duplicate growth.'
Write-Host 'Check that mark-price candles are stored under mark-prefixed intervals such as mark-15m.'
Write-Host 'Check that funding rates exist for the requested symbol and span a long historical range.'
Write-Host 'Check that the Hyperliquid smoke test still succeeds if it was enabled.'