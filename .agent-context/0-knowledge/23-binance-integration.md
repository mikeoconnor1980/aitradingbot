# Binance USDⓈ-M Futures Integration

Binance now serves two roles in the platform:

1. Historical and contextual market data ingestion via the Binance USDⓈ-M Futures REST API (`fapi.binance.com`)
2. Exchange-backed account, metadata, and execution support behind the shared exchange abstractions used by the API and Worker

The ingestion path still fetches kline (OHLCV) candles, mark price candles, and funding rate history for backtesting and strategy context. In addition, the infrastructure layer now includes Binance execution, account, market metadata, exchange-info cache, and symbol-metadata adapter components that can be resolved through `IExecutionEngine`, `IExchangeAccountClient`, `IExchangeMarketMetadataProvider`, and `IExchangeSymbolMetadataProvider`.

---

## Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `BinanceAssetMapper` | `src/TradePilot.Infrastructure/Binance/BinanceAssetMapper.cs` | Maps display symbols to futures symbols; resolves intervals to ms; handles mark-price prefix |
| `BinanceExecutionEngine` | `src/TradePilot.Infrastructure/Binance/BinanceExecutionEngine.cs` | Places, cancels, and modifies Binance orders behind `IExecutionEngine`; normalizes size and price using exchange metadata |
| `BinanceAccountAdapter` | `src/TradePilot.Infrastructure/Binance/BinanceAccountAdapter.cs` | Exposes balances, positions, open orders, and fills behind `IExchangeAccountClient` |
| `BinanceMarketMetadataProvider` | `src/TradePilot.Infrastructure/Binance/BinanceMarketMetadataProvider.cs` | Provides mark price, funding rate, volume, and open interest behind `IExchangeMarketMetadataProvider` |
| `BinanceExchangeInfoCache` | `src/TradePilot.Infrastructure/Binance/BinanceExchangeInfoCache.cs` | Caches Binance symbol precision and leverage metadata for normalization and UI asset discovery |
| `BinanceSymbolMetadataProvider` | `src/TradePilot.Infrastructure/Binance/BinanceSymbolMetadataProvider.cs` | Adapts Binance exchange-info data to the shared `IExchangeSymbolMetadataProvider` contract |
| `BinanceParsing` | `src/TradePilot.Infrastructure/Binance/BinanceParsing.cs` | Shared parsing helpers for decimals/integers with `NumberStyles.Any` for scientific notation |
| `BinanceApiException` | `src/TradePilot.Application/Abstractions/Exceptions/BinanceApiException.cs` | Binance-specific exception carrying error codes and transience classification |
| `IBinanceFuturesRestClient` | `src/TradePilot.Application/Abstractions/Services/IBinanceFuturesRestClient.cs` | Interface for Binance Futures REST calls |
| `IBinanceFuturesAuthClient` | `src/TradePilot.Application/Abstractions/Services/IBinanceFuturesAuthClient.cs` | Authenticated Binance Futures account and trading client used by execution and account services |
| `BinanceFuturesRestClient` | `src/TradePilot.Infrastructure/Services/BinanceFuturesRestClient.cs` | Typed `HttpClient` implementation; calls `/fapi/v1/klines`, `/fapi/v1/markPriceKlines`, `/fapi/v1/fundingRate` |
| `BinanceFuturesAuthClient` | `src/TradePilot.Infrastructure/Services/BinanceFuturesAuthClient.cs` | Signed Binance Futures REST client for balances, orders, leverage, margin mode, fills, and exchange execution |
| `IBinanceCandleIngestionService` | `src/TradePilot.Application/Abstractions/Services/IBinanceCandleIngestionService.cs` | Interface for Binance candle ingestion |
| `BinanceCandleIngestionService` | `src/TradePilot.Infrastructure/Services/BinanceCandleIngestionService.cs` | Paginates klines and mark-price klines; writes to `ICandleRepository` with `Source = "Binance"` |
| `IFundingRateIngestionService` | `src/TradePilot.Application/Abstractions/Services/IFundingRateIngestionService.cs` | Interface for funding rate ingestion |
| `FundingRateIngestionService` | `src/TradePilot.Infrastructure/Services/FundingRateIngestionService.cs` | Paginates funding rate history; writes to `IFundingRateRepository` |
| `BinanceIngestionOptions` | `src/TradePilot.Application/Abstractions/Configuration/BinanceIngestionOptions.cs` | Typed options for ingestion (page size, timeouts, default start date) |
| `IngestBinanceCandlesCommand` | `src/TradePilot.Application/Candles/Commands/IngestBinanceCandlesCommand.cs` | MediatR command dispatching to `IBinanceCandleIngestionService` |
| `IngestFundingRatesCommand` | `src/TradePilot.Application/FundingRates/Commands/IngestFundingRatesCommand.cs` | MediatR command dispatching to `IFundingRateIngestionService` |
| `IFundingRateRepository` | `src/TradePilot.Application/Abstractions/Repositories/IFundingRateRepository.cs` | Repository interface (bulk insert + latest timestamp query) |
| `FundingRateRepository` | `src/TradePilot.Persistence/Repositories/FundingRateRepository.cs` | EF Core implementation; uses `INSERT OR IGNORE` in batches of 500 |

---

## Asset Mapper

`BinanceAssetMapper` mirrors the role of `HyperliquidAssetMapper`:

| Supported Symbols | Binance Futures Symbol |
|---|---|
| `BTC` | `BTCUSDT` |
| `ETH` | `ETHUSDT` |
| `SOL` | `SOLUSDT` |
| `DOGE` | `DOGEUSDT` |
| `AVAX` | `AVAXUSDT` |
| `ARB` | `ARBUSDT` |
| `LINK` | `LINKUSDT` |
| `OP` | `OPUSDT` |

Supported intervals: `5m`, `15m`, `1h`, `4h`, `1d`

---

## Mark Price Kline Convention

To request mark price candles instead of trade price candles, prefix the interval with `mark-`:

- `15m` → standard klines (`/fapi/v1/klines`)
- `mark-15m` → mark price klines (`/fapi/v1/markPriceKlines`)

`BinanceAssetMapper.GetIntervalMs` strips the prefix before resolving to milliseconds. `BinanceCandleIngestionService` detects the prefix to route the REST call accordingly. Mark price candles are stored in the `Candles` table with `Source = "Binance"` and `Interval = "mark-15m"`.

---

## Execution Engine & Order Safety

`BinanceExecutionEngine` includes metadata-driven normalization and fail-fast safeguards:

| Feature | Behavior |
|---------|----------|
| Order size normalization | Rounds to symbol-specific decimals via `BinanceExchangeInfoCache`; throws if normalizes to zero |
| Price normalization | Rounds limit/trigger prices to symbol-specific decimals; throws if normalizes to zero |
| Fail-fast cancel | Requires in-memory `ConcurrentDictionary<orderId, asset>` mapping; throws if mapping lost (e.g., after process restart) |
| Modify compensation | Cancels existing order, places replacement, retries once on failure; throws `DomainException` if both attempts fail |
| Margin-type switching | Calls `SetMarginTypeAsync` before `SetLeverageAsync`; Binance `-4046` (already set) treated as success |

## Exception Hierarchy

| Exception Type | Base | When Thrown |
|---|---|---|
| `ExchangeApiException` | `Exception` | Abstract base carrying `ExchangeStatusCode` and `ErrorCategory` |
| `RateLimitException` | `ExchangeApiException` | HTTP 429 after retries exhausted (any exchange) |
| `SigningException` | `ExchangeApiException` | EIP-712 signing fails (Hyperliquid) |
| `BinanceApiException` | `ExchangeApiException` | Binance REST errors with `IsTransient` classification |
| `HyperliquidApiException` | `ExchangeApiException` | Hyperliquid REST errors (sealed) |

Binance error mapping: HTTP 403/451 → permanent, 418 → rate limit, 5xx → transient, business codes `-1111`/`-2019`/`-4003` → permanent `DomainException`.

## Resilience & HTTP Clients

The `binance-public` named HTTP client has resilience in both API and Worker hosts:

| Policy | Configuration |
|---|---|
| Retry strategy | Exponential backoff, 5 attempts, 1s–60s delay, jitter |
| Retry conditions | HTTP 429, 418, or 5xx |
| Per-attempt timeout | 5 seconds |
| Client timeout | 30 seconds (outer cap) |

---

## API Endpoints

| Method | Route | Command | Description |
|--------|-------|---------|-------------|
| `POST` | `/api/candles/ingest/binance` | `IngestBinanceCandlesCommand` | Ingest klines (and optionally mark-price klines) for a symbol + interval list |
| `POST` | `/api/funding/ingest` | `IngestFundingRatesCommand` | Ingest funding rate history for a symbol |

Separate order, account, and asset-discovery endpoints resolve the active exchange through the shared exchange abstractions. When Binance is the current exchange, those endpoints now use Binance-backed implementations for execution, account reads, market metadata, and symbol metadata.

Both endpoints accept `StartTime`/`EndTime` as Unix milliseconds (nullable). Omitting `StartTime` resumes from the latest stored record.

---

## Consuming Ingested Candle Data

Candles written to `ICandleRepository` via ingestion are served to the frontend by a separate read path. The `GetHistoricalCandlesQuery` reads directly from `ICandleRepository` — it does **not** go through Hyperliquid.

| Component | Location |
|-----------|----------|
| `GetHistoricalCandlesQuery` + Handler | `src/TradePilot.Application/MarketData/Queries/GetHistoricalCandlesQuery.cs` |
| Endpoint | `GET /api/market/candles/history` (`MarketDataController`) |

**Endpoint parameters**: `asset`, `timeframe`, `endTime` (Unix ms, optional), `limit` (default 500, max 5000).

**Asset mapping**: The handler strips the `-PERP` suffix from the asset parameter (`BTC-PERP` → `BTC`) using its own inline mapping — it does not use `BinanceAssetMapper`.

**Pagination** uses a reverse-cursor pattern: `endTime` anchors the end of the window; `startTime` is derived as `endTime - (limit × timeframeMs)`. To page backwards, pass the oldest candle timestamp as `endTime`.

**Frontend fallback**: `MarketDataComponent` tries the history endpoint first; if it returns an empty result (no local data), it falls back to `GET /api/market/candles` (live Hyperliquid data). This applies to both initial chart load and "load more older candles" requests.

---

## Configuration

Config section: `BinanceIngestion`

| Key | Default | Description |
|-----|---------|-------------|
| `BaseUrl` | `https://fapi.binance.com` | Binance USDⓈ-M Futures REST base URL |
| `PageSize` | `1500` | Candles per API request (max 1500) |
| `BatchDelayMs` | `250` | Delay between pagination requests |
| `MaxRetries` | `3` | Retry attempts for failed requests |
| `MaxIngestionTimeoutMs` | `7200000` | Overall ingestion timeout (2 hours) |
| `DefaultStartDate` | `2019-09-01` | Earliest candle/funding date to request when no stored data exists |

---

## Ingestion Pattern

Both `BinanceCandleIngestionService` and `FundingRateIngestionService` share the same pattern:

1. Uses a `SemaphoreSlim(1,1)` guard — only one ingestion allowed at a time per service
2. Resumes from the latest stored timestamp when `StartTime` is not supplied
3. Paginates forward in time using `startTime` / `endTime` window shifting
4. Exponential backoff retry for transient failures (configurable `MaxRetries`)
5. Writes via `INSERT OR IGNORE` bulk insert — safe for re-ingestion
6. Applies a configurable `BatchDelayMs` between pages to respect API rate limits
7. Throws `IngestionAlreadyRunningException` (→ HTTP 409) if already in progress

---

## Extending

To add a new Binance symbol:
1. Add entry to `BinanceAssetMapper.SymbolToFuturesSymbol`
2. The entry is automatically included in `BinanceAssetMapper.SupportedAssets` (single source of truth)
3. All execution, account, and metadata consumers respect the new asset immediately
4. Verify the asset's max leverage default in `BinanceExchangeInfoCache.MaxLeverageByAsset` (BTC/ETH = 125x, others = 25x)

To add a new interval:
1. Add entry to `BinanceAssetMapper.IntervalToMs`

To add a new Binance data type (e.g., open interest):
1. Add response model to `src/TradePilot.Infrastructure/Binance/Models/`
2. Add method to `IBinanceFuturesRestClient` + implement in `BinanceFuturesRestClient`
3. Add Application service interface + implementation following the `FundingRateIngestionService` pattern
4. Add MediatR command + handler
5. Add controller endpoint
