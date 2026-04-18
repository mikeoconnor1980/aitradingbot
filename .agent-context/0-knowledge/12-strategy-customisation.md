# Strategy Customisation

Users create and maintain strategy instances through the API and Angular builder flows. A strategy is stored as a `Strategy` domain entity plus versioned `ConfigJson`, with optional interpretation and review features layered on top.

## Core Model

Each strategy consists of:

- A `Strategy` record in the domain model.
- A JSON configuration stored in `Strategy.ConfigJson` and deserialized into `TradingApp.Application.StrategyAuthoring.Models.StrategyConfig`.
- A revision history in `StrategyRevision`.
- An optional AI review per revision in `StrategyReview`.

The `Strategy` entity also stores `HighWaterMarkUsd`, which is updated during live or backtest execution and used by the drawdown system.

## Strategy Creation Paths

`SourceMetadata.EntryPoint` uses `StrategyEntryPoint`, not the older `RevisionSource` naming used in early drafts.

| `StrategyEntryPoint` value | Meaning |
|---------------------------|---------|
| `UiBuilder` | Created or edited from the manual UI builder |
| `UiWizard` | Created from the guided wizard flow |
| `NaturalLanguage` | Created from natural-language interpretation |
| `PineImport` | Imported from Pine Script translation flow |
| `Migration` | Created by migration or system import logic |
| `Optimizer` | Produced by the optimizer workflow |

`RevisionSourceMapper` then maps those entry points to persisted `RevisionSource` values on `StrategyRevision`.

## Runtime-Relevant Customisation Fields

The most important user-controlled knobs are:

| Area | Key Fields |
|------|------------|
| Mode | `strategyMode` (`grid` or `signal`) |
| Instrument | `market`, `timeframe`, `direction` |
| Grid | `levels`, `spacing`, `entryMode`, `anchorPrice`, `breakdownThreshold` |
| Signal | `entryLogic`, `entryConditions`, optional `trendFilter` |
| Exit | `takeProfit`, `stopLoss`, `exitOnOppositeSignal` |
| Risk | `positionSizeType`, `positionSizeValue`, `riskPerTradePercent`, `leverage`, `autoLeverage`, cooldown settings |

For the canonical schema, see [13-strategy-config-schema.md](13-strategy-config-schema.md).

### Signal-Mode Authoring Surface

The builder now supports both simple indicator thresholds and higher-order price-structure conditions in `entryConditions`.

| Condition family | Implemented types |
|------------------|-------------------|
| Indicator threshold / crossover | `rsi`, `price_vs_ema`, `macd`, `support_resistance` |
| Derived signal / price structure | `candle_pattern`, `liquidity_sweep`, `structure_shift` |

Derived signal conditions are configured in the same authoring flow as other entry conditions and are combined with the same `all` / `any` entry logic.

## Position Sizing Options

`PositionSizeType` currently supports:

| Enum value | Serialized value | Meaning |
|------------|------------------|---------|
| `PercentWallet` | `percent_wallet` | Size as a percent of account equity |
| `FixedNotional` | `fixed_notional` | Fixed USD notional |
| `RiskBased` | `risk_based` | Derived from account equity and stop-loss distance |

The older `percent_of_equity` label is not the current serialized form.

## API Surface

### Core Strategy Endpoints

| Method | Endpoint | Notes |
|--------|----------|-------|
| `GET` | `/api/strategies` | List active strategies for the current user |
| `GET` | `/api/strategies/{id}` | Retrieve a single strategy with full config |
| `POST` | `/api/strategies` | Create a strategy after validation |
| `PUT` | `/api/strategies/{id}` | Update a strategy and create a new revision |
| `DELETE` | `/api/strategies/{id}` | Soft-delete by setting `IsActive = false` |
| `POST` | `/api/strategies/validate` | Run `CompositeStrategyValidator` without persisting |
| `POST` | `/api/strategies/interpret` | Convert natural-language input into a proposed strategy config |

Duplicate strategy names are rejected per user with HTTP 409.

### Revision and Review Endpoints

| Method | Endpoint | Notes |
|--------|----------|-------|
| `GET` | `/api/strategies/{id}/versions` | Paginated revision history |
| `GET` | `/api/strategies/{id}/versions/{rev:int}` | Retrieve a single revision |
| `GET` | `/api/strategies/{id}/diff` | Field-level diff between revisions |
| `POST` | `/api/strategies/{id}/versions/{rev:int}/restore` | Restore a past revision as a new revision |
| `POST` | `/api/strategies/{id}/versions/{rev:int}/review` | Generate an AI review for a revision |
| `GET` | `/api/strategies/{id}/versions/{rev:int}/review` | Retrieve the stored review for that revision |

## Revisioning and Persistence

`ConfigJson` is stored directly on `Strategy`. Each create or update writes a new `StrategyRevision` snapshot and increments `Strategy.Version`.

Revision metadata includes:

- Full config snapshot.
- Auto-generated change summary.
- Persisted `RevisionSource` mapped from `StrategyEntryPoint`.
- Optional original NL prompt in `SourceMetadata.SourceText`.

The docs previously referred to `StrategyRun` and `StrategyPerformance` as persisted records. Those entities do not exist in the current codebase.

## Strategy Review Feature

Strategy review is a separate feature from strategy interpretation.

| Component | Purpose |
|-----------|---------|
| `RequestStrategyReviewCommand` | Generates a review for a specific strategy revision |
| `StrategyReviewDto` | API DTO returned to the frontend |
| `StrategyReview` | Persisted domain entity storing markdown review output |
| `IStrategyReviewer` | AI service abstraction used by the command handler |

The review flow loads the selected revision, optionally enriches the prompt with the latest completed backtest and funding-rate range, generates markdown feedback, replaces any prior review for that revision, and stores the latest result.

## Execution Notes

- Multiple strategies can be stored per user, but the worker/session model typically runs one active strategy per user session.
- `Strategy.IsRunning` exists and is enforced for some write operations, but the POC worker still treats it as a partial/stubbed runtime flag.
- `Strategy.HighWaterMarkUsd` is updated by the scheduler so risk scaling survives restarts.

## Future Recommendations

- Add a dedicated live-session entity if detailed `StrategyRun` style observability becomes necessary.
- Add per-revision performance snapshots if the product needs durable leaderboard or comparison views.
- Add stronger UI surfacing for `StrategyReview` history and fallback-model diagnostics.