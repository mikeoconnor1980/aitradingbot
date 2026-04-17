# Fear & Greed Index Integration

## Overview

The system integrates the Crypto Fear & Greed Index from [alternative.me](https://alternative.me/crypto/fear-and-greed-index/) as an additional market context signal. This provides a crowd-sentiment gauge that influences regime classification for both live trading and backtesting.

## Data Source

- **API**: `GET https://api.alternative.me/fng/?limit={N}`
- **Cost**: Free, no API key required
- **Update frequency**: Once per day
- **History**: Available since February 2018 (`?limit=0` returns all)
- **Response format**: JSON with `data[]` containing `value` (0–100), `value_classification`, and `timestamp` (Unix seconds)

## Classification Buckets

| Range   | Classification |
| ------- | -------------- |
| 0–24    | Extreme Fear   |
| 25–49   | Fear           |
| 50      | Neutral        |
| 51–74   | Greed          |
| 75–100  | Extreme Greed  |

## Architecture

### Domain Layer

- **`FearGreedReading`** — Entity persisted to the `FearGreedReadings` table with a unique index on `Timestamp`
- **`FearGreedSnapshot`** — Immutable value object attached to `MarketContext.FearGreed`
- **`FearGreedClassification`** — Enum mapping value ranges to sentiment buckets

### Infrastructure Layer

- **`IFearGreedClient` / `FearGreedClient`** — Typed `HttpClient` calling the alternative.me API with retry resilience
- **`FearGreedSyncWorker`** — `BackgroundService` that fetches the latest reading every 6 hours (configurable)

### Application Layer

- **`IFearGreedReadingRepository`** — Repository interface with `GetLatestAsync`, `GetRangeAsync`, `GetCountAsync`, `GetEarliestAsync`, `BulkUpsertAsync`
- **`FearGreedOptions`** — Configuration class bound to `appsettings.json` section `"FearGreed"`

### API Layer

- **`FearGreedController`** — Endpoints:
  - `GET /api/fear-greed/status` — Latest reading + total count + date range
  - `GET /api/fear-greed/history?from=&to=` — Historical readings in a time range
  - `POST /api/fear-greed/backfill` — Fetches full history from API and persists

### Frontend

- **Data Management** page (`/candle-data`) uses a `mat-tab-group` wrapper with two tabs:
  - "Candle Data" — existing candle management component
  - "Fear & Greed Index" — shows latest value, classification, reading counts, and a backfill button

## Regime Influence

The Fear & Greed Index applies a **one-level regime shift** on extreme readings:

- **Extreme Fear (≤24)**: Shifts regime one step toward Defensive/RiskOff
  - Aggressive → Normal
  - Normal → Defensive
  - Defensive → RiskOff
- **Extreme Greed (≥75)**: Shifts regime one step toward Aggressive
  - RiskOff → Defensive
  - Defensive → Normal
  - Normal → Aggressive

This shift is applied in the `SyntheticRegimeProvider` (used by both live and backtest builders).

## Staleness Rule

Readings older than **48 hours** are considered stale and ignored. This prevents the system from acting on outdated sentiment data if the API is unavailable for an extended period.

## Backtest Support

The `BacktestMarketContextBuilder` accepts a pre-loaded list of `FearGreedReading` entities. During replay, it looks up the most recent reading at or before each trigger candle's timestamp and applies the same staleness check and regime shift as live trading.

The `BacktestRunner` loads Fear & Greed readings for the backtest date range from the repository and injects them into the builder before the replay loop begins.

## Configuration

```json
{
  "FearGreed": {
    "BaseUrl": "https://api.alternative.me/",
    "Enabled": true,
    "SyncIntervalMinutes": 360,
    "StalenessThresholdHours": 48
  }
}
```

## LLM Integration

When the LLM context provider is active, the Fear & Greed value is appended to the user prompt as additional context. The system prompt instructs the LLM to treat extreme readings as a tiebreaker for regime classification.
