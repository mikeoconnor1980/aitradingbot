# Domain Model

This document describes the persisted domain entities that exist in the current codebase and the most important runtime concepts around them. It replaces the original aspirational model with the entities actually present in `TradingApp.Domain` and `TradingApp.Persistence`.

## Overview

The current model falls into five broad areas:

| Area | Persisted Entities |
|---|---|
| Identity | User, Subscription, UserWalletAddress |
| Strategy Authoring | Strategy, StrategyRevision, StrategyReview |
| Market and Analysis Data | Candle, FundingRate, LlmContextSnapshot, MacroEvent, MacroSyncRun |
| Replay and Optimization | BacktestRun, OptimizationRun, OptimizationResult |
| Live Trading Audit | GridCycle, LiveOrder, LiveFill |

Two design constraints shape the rest of the model:

- Some associations are application-level only and are not enforced by EF Core foreign keys.
- Most domain entities use `static Create(...)` plus private setters, but the live-trading audit entities do not follow that pattern yet.

## Entity Inventory

| Entity | Scope | Purpose |
|---|---|---|
| User | Tenant root | Authentication identity and profile |
| Subscription | Tenant-scoped | Access tier and expiry state |
| UserWalletAddress | Tenant-scoped | Exchange wallet registration without private key custody |
| Strategy | Tenant-scoped | Saved strategy definition and latest config JSON |
| StrategyRevision | Strategy-scoped | Immutable revision history per saved strategy |
| StrategyReview | Strategy-scoped | AI review for a specific revision number |
| Candle | Shared market data | OHLCV history for live context and backtesting |
| FundingRate | Shared market data | Funding history used by strategy and analysis features |
| LlmContextSnapshot | Shared analysis cache | Stored market context snapshot from LLM/classifier pipeline |
| MacroEvent | Shared operations data | Economic calendar event with blocking window |
| MacroSyncRun | Shared operations data | Audit log for calendar sync jobs |
| BacktestRun | Shared execution record | Queued/running/completed backtest job and results |
| OptimizationRun | Shared execution record | Parameter sweep job state |
| OptimizationResult | Optimization-scoped | Ranked result row for one optimization run |
| GridCycle | Tenant-scoped | Persisted record of one live grid lifecycle |
| LiveOrder | Tenant-scoped | Persisted live order telemetry |
| LiveFill | Tenant-scoped | Persisted live fill telemetry |

## Removed From The Old Model

The following items were documented previously but do not exist as persisted entities in the current implementation:

- `UserExchangeCredential` was replaced by `UserWalletAddress`.
- `Order` was replaced by `LiveOrder`.
- `Fill` was replaced by `LiveFill`.
- `Signal` is in-memory only and is not persisted.
- `BotState` is runtime state, primarily represented by `GridState` in memory.
- `StrategyConfig` is not a separate table; it is embedded as `Strategy.ConfigJson`.
- `StrategyRun`, `StrategyPerformance`, `GridPlan`, `Position`, `RiskEvent`, `AuditLog`, `StrategyExecutionCheckpoint`, and replay-debugger entities such as `CounterfactualBranch` are not persisted in the current schema.

## Identity And Access

### User

`User` is the root identity entity used for both local credentials and external sign-in.

| Field | Notes |
|---|---|
| Id | `Guid` primary key |
| Email | Unique, normalised to lower case |
| DisplayName | Mutable profile name |
| PasswordHash | Nullable for OAuth-created users |
| CreatedAtUtc | Unix milliseconds |
| IsActive | Soft activation flag |
| PreferredNetwork | `mainnet` or `testnet`, default `mainnet` |
| AuthProvider | Nullable external provider name |
| ExternalProviderId | Nullable provider subject identifier |

Key behaviours:

- `User.Create(email, displayName, passwordHash)` creates a local account.
- `User.CreateExternal(email, displayName, authProvider, externalProviderId)` creates an OAuth-backed account.
- `User.LinkExternalProvider(...)` attaches an external identity to an existing user.
- `User.UpdatePreferredNetwork(...)` enforces `mainnet` or `testnet`.

Persistence notes:

- Unique index on `Email`.
- Unique filtered index on `(AuthProvider, ExternalProviderId)`.

### Subscription

`Subscription` records access entitlement. The current product model only exposes the free tier.

| Field | Notes |
|---|---|
| Id | `Guid` primary key |
| UserId | `Guid` foreign key to `User` |
| Tier | `SubscriptionTier` enum |
| Status | `SubscriptionStatus` enum |
| StartedAtUtc | Unix milliseconds |
| ExpiresAtUtc | Unix milliseconds |
| CreatedAtUtc | Unix milliseconds |

Enums currently implemented:

| Enum | Values |
|---|---|
| SubscriptionTier | `Free = 0` |
| SubscriptionStatus | `Active = 0`, `Expired = 1`, `Cancelled = 2` |

Key behaviours:

- `Subscription.Create(userId, tier, durationDays)` creates an active subscription.
- `Expire()` transitions the status to `Expired`.
- `IsExpired(nowUtcMs)` evaluates expiry without mutating state.

There is no `Paused` status and no external billing identifier because billing integration is not implemented.

### UserWalletAddress

`UserWalletAddress` records the public wallet address the platform should trade against. The server does not store the user's private key.

| Field | Notes |
|---|---|
| Id | `Guid` primary key |
| UserId | `Guid` foreign key to `User` |
| Exchange | Defaults to `Hyperliquid` |
| WalletAddress | Ethereum-style `0x...` address |
| CreatedAtUtc | Unix milliseconds |
| IsActive | Soft activation flag |

Key behaviours:

- `Create(userId, walletAddress, exchange)` validates the address format.
- `UpdateAddress(...)` revalidates the replacement address.
- `Deactivate()` soft-disables the mapping.

## Strategy Authoring

### Strategy

`Strategy` is the editable saved strategy aggregate. It stores the latest JSON configuration directly on the entity.

| Field | Notes |
|---|---|
| Id | `Guid` primary key |
| UserId | String tenant identifier used by the application layer |
| Name | Unique per active strategy within a user scope |
| StrategyType | String discriminator |
| ConfigJson | Full strategy configuration JSON |
| Version | Incremented on update |
| IsActive | Soft-delete flag |
| IsRunning | Runtime stub persisted for UI/control use |
| HighWaterMarkUsd | Nullable drawdown-tracking anchor |
| CreatedAtUtc | Unix milliseconds |
| UpdatedAtUtc | Unix milliseconds |

Key behaviours:

- `Create(...)` initialises version `1`, active status, and `IsRunning = false`.
- `Update(...)` replaces the config and increments the version.
- `SoftDelete()` disables the strategy and clears the running flag.
- `SetRunningState(...)` blocks reactivation of inactive strategies.
- `UpdateHighWaterMark(...)` stores the highest observed equity watermark.

Persistence notes:

- Unique filtered index on `(UserId, Name)` where `IsActive = 1`.
- Index on `(UserId, IsActive)` for active strategy lookups.
- `UserId` is a string field and is not configured as an EF Core foreign key to `User`.

### StrategyRevision

`StrategyRevision` is the immutable audit trail for each save or restore action.

| Field | Notes |
|---|---|
| Id | `Guid` primary key |
| StrategyId | `Guid` foreign key to `Strategy` |
| RevisionNumber | Starts at 1 and increments per strategy |
| ConfigJson | Full configuration snapshot |
| Source | `RevisionSource` enum stored as string |
| Label | Optional user label |
| ChangeSummary | Diff summary |
| CreatedAtUtc | Unix milliseconds |

`RevisionSource` currently includes `Ui`, `Api`, `Import`, `Restore`, and `Optimizer`.

Persistence notes:

- Cascade delete from `Strategy`.
- Unique index on `(StrategyId, RevisionNumber)`.

### StrategyReview

`StrategyReview` stores the markdown output of the AI review flow for a specific strategy revision number.

| Field | Notes |
|---|---|
| Id | `Guid` primary key |
| StrategyId | `Guid` foreign key to `Strategy` |
| RevisionNumber | Logical link to a strategy revision |
| ReviewMarkdown | Full markdown review content |
| ModelName | Model identifier used to generate the review |
| IsFallback | Whether a fallback model/path was used |
| CreatedAtUtc | Unix milliseconds |

Persistence notes:

- Cascade delete from `Strategy`.
- Unique index on `(StrategyId, RevisionNumber)`.
- The link to `StrategyRevision` is logical only; there is no composite EF Core foreign key from `(StrategyId, RevisionNumber)` to `StrategyRevision`.

## Market, Context, And Operations Data

### Candle

`Candle` stores OHLCV market data shared by all users.

Key fields: `Source`, `Symbol`, `Interval`, `Timestamp`, `Open`, `High`, `Low`, `Close`, `Volume`, `NumTrades`.

Persistence notes:

- Auto-increment `long` key.
- Unique index on `(Source, Symbol, Interval, Timestamp)`.
- Decimal values are mapped through SQLite double conversions.

### FundingRate

`FundingRate` stores historical perp funding data shared across the system.

Key fields: `Symbol`, `Timestamp`, `Rate`, `MarkPrice`.

Persistence notes:

- Auto-increment `long` key.
- Unique index on `(Symbol, Timestamp)`.

### LlmContextSnapshot

`LlmContextSnapshot` stores derived market context snapshots for reuse by the application.

| Field | Notes |
|---|---|
| Symbol | Asset or market symbol |
| MarketSentiment | Stored as string |
| MacroRegime | Stored as string |
| EventRisk | Stored as string |
| Confidence | Decimal mapped to SQLite double |
| DerivedRegime | Strategy-facing regime label |
| Summary | Nullable/short textual summary |
| GeneratedAtUtc | Unix milliseconds |

Persistence notes:

- Index on `(Symbol, GeneratedAtUtc)`.
- Separate index on `GeneratedAtUtc`.

### MacroEvent

`MacroEvent` is the persisted economic calendar event used to block trading around high-impact releases.

Important fields:

- Provider identity: `Provider`, `ProviderEventId`
- Event description: `Title`, `Country`, `Currency`, `Category`
- Timing: `ScheduledAtUtc`, `ReleasedAtUtc`, `BlockStartUtc`, `BlockEndUtc`
- Severity and status: `Importance`, `Status`
- Values: `Actual`, `Forecast`, `Previous`, `Revised`
- Audit: `SourceUrl`, `RawPayloadJson`, `LastSeenUtc`, `CreatedAtUtc`, `UpdatedAtUtc`

Enums currently implemented:

| Enum | Values |
|---|---|
| MacroEventImportance | `Unknown`, `Low`, `Medium`, `High`, `Critical` |
| MacroEventStatus | `Scheduled`, `Live`, `Released`, `Revised`, `Cancelled` |

Persistence notes:

- Unique index on `(Provider, ProviderEventId)`.
- Indices on `ScheduledAtUtc`, `BlockStartUtc`, `BlockEndUtc`, and `Importance`.

### MacroSyncRun

`MacroSyncRun` records one calendar provider sync attempt.

| Field | Notes |
|---|---|
| Provider | Source system name |
| StartedAtUtc | Unix milliseconds |
| CompletedAtUtc | Nullable completion time |
| Succeeded | Boolean success flag |
| EventsFetched | Count from provider |
| EventsInserted | New rows created |
| EventsUpdated | Existing rows refreshed |
| Error | Nullable failure reason |

Persistence note: index on `StartedAtUtc` for recent-run queries.

## Replay And Optimization Records

### BacktestRun

`BacktestRun` combines the old “backtest definition” and “backtest result” concepts into one persisted job record.

Important fields:

- Request shape: `Symbol`, `IntervalsJson`, `StartDateUtc`, `EndDateUtc`, `StrategyConfigJson`, `ExecutionConfigJson`, `InitialCapital`
- Job state: `Status`, `Progress`, `TotalCandles`, `ErrorMessage`, `CreatedAtUtc`
- Result metrics: `CandlesReplayed`, `ElapsedMs`, `TotalTrades`, `WinningTrades`, `LosingTrades`, `WinRate`, `TotalPnl`, `MaxDrawdown`, `AverageTradePnl`, `AverageHoldTimeMinutes`, `HedgesOpened`, `TotalFeesPaid`
- Advanced metrics: `Expectancy`, `ProfitFactor`, `Sqn`, `KellyPercent`, `HalfKellyPercent`, `WinLossRRatio`
- Audit payloads: `TradesJson`, `EquityTimeSeriesJson`, `AuditLogEnabled`, `CandleLogJson`, `OrderEventLogJson`, `GridCycleLogJson`
- Optional provenance: `StrategyId`, `StrategyRevisionId`

`BacktestStatus` values are `Queued`, `Running`, `Completed`, `Failed`, and `Cancelled`.

Persistence notes:

- Index on `StrategyId`.
- `StrategyId` and `StrategyRevisionId` are stored, but EF Core does not configure them as foreign keys.

### OptimizationRun

`OptimizationRun` stores the state of one parameter sweep execution.

| Field | Notes |
|---|---|
| Id | `Guid` primary key |
| Symbol | Optimized market |
| StartDateUtc / EndDateUtc | Replay window |
| InitialCapital | Starting balance |
| SweepConfigJson | Search-space definition |
| ThresholdsJson | Qualification thresholds |
| TotalCombinations | Planned combination count |
| CompletedCount | Progress counter |
| QualifiedCount | Number of runs meeting thresholds |
| FailedCount | Number of failed combinations |
| Status | `OptimizationStatus` enum |
| ErrorMessage | Nullable error detail |
| ElapsedMs | Total runtime |
| CreatedAtUtc | Unix milliseconds |

`OptimizationStatus` values are `Queued`, `Running`, `Completed`, `Failed`, and `Cancelled`.

Persistence notes:

- Index on `CreatedAtUtc`.
- Unlike the original plan, the current schema does not store a `StrategyId` on `OptimizationRun`.

### OptimizationResult

`OptimizationResult` stores one ranked outcome within an optimization run.

Important fields:

- Linkage and ordering: `OptimizationRunId`, `Rank`
- Inputs and interpretation: `StrategyConfigJson`, `SignalDescription`
- In-sample metrics: `FitnessScore`, `TotalPnl`, `WinRate`, `MaxDrawdown`, `TotalTrades`, `WinningTrades`, `LosingTrades`, `TotalFeesPaid`, `AverageTradePnl`, `AverageHoldTimeMinutes`
- Out-of-sample metrics: `OosTotalPnl`, `OosWinRate`, `OosMaxDrawdown`, `OosTotalTrades`, `OosFitnessScore`
- Risk-adjusted metrics: `SharpeRatio`, `SortinoRatio`, `ProfitFactor`, `CalmarRatio`

Persistence notes:

- Foreign key to `OptimizationRun` with cascade delete.
- Unique index on `(OptimizationRunId, Rank)`.

## Live Trading Audit Entities

### GridCycle

`GridCycle` is the persisted summary record for one live grid lifecycle.

| Field | Notes |
|---|---|
| Id | `Guid` primary key |
| GridCycleId | External/application cycle identifier |
| StrategyName | Strategy label at execution time |
| Symbol | Traded market |
| AnchorPrice | Grid anchor |
| TotalLevels / FilledLevels | Fill progress |
| Lifecycle | Current lifecycle label |
| StartedAtUtc / ClosedAtUtc | `DateTime` timestamps |
| CloseReason | Nullable terminal reason |
| RealisedPnl | Nullable realised result |
| UserId | String tenant identifier |

Persistence notes:

- Unique index on `GridCycleId`.
- Indices on `(StrategyName, Symbol, Lifecycle)` and `UserId`.
- No EF Core foreign key to `User`.

### LiveOrder

`LiveOrder` stores one persisted live order record, typically linked to a grid cycle by identifier rather than foreign key.

Important fields: `OrderId`, `GridCycleId`, `Level`, `Symbol`, `Side`, `OrderType`, `Price`, `Size`, `TradeType`, `Status`, `PlacedAtUtc`, `FilledAtUtc`, `CancelledAtUtc`, `UserId`.

Enums in use:

| Enum | Values |
|---|---|
| OrderSide | `Buy`, `Sell` |
| OrderStatus | `Pending`, `Resting`, `PartiallyFilled`, `Filled`, `Cancelled` |

Persistence notes:

- Unique index on `OrderId`.
- Indices on `GridCycleId` and `UserId`.
- `GridCycleId` and `UserId` are not enforced as foreign keys.

### LiveFill

`LiveFill` stores fill telemetry for live or exchange-native protection orders.

Important fields: `OrderId`, `Symbol`, `Side`, `Direction`, `Price`, `Size`, `Fee`, `ClosedPnl`, `FilledAtUtc`, `UserId`.

Persistence notes:

- Indices on `OrderId`, `(Symbol, FilledAtUtc)`, and `UserId`.
- `OrderId` is an application-level link to `LiveOrder.OrderId`, not an EF Core foreign key.

## Relationship Notes

The most important schema relationships today are:

| Relationship | Type | Notes |
|---|---|---|
| User -> Subscription | Enforced FK | `Subscription.UserId` is a real foreign key |
| User -> UserWalletAddress | Enforced FK | `UserWalletAddress.UserId` is a real foreign key |
| User -> Strategy | Logical only | `Strategy.UserId` is stored as string with no FK |
| Strategy -> StrategyRevision | Enforced FK | Cascade delete |
| Strategy -> StrategyReview | Enforced FK | Revision linkage is logical via `RevisionNumber` |
| Strategy -> BacktestRun | Logical only | `BacktestRun.StrategyId` is indexed but not an FK |
| OptimizationRun -> OptimizationResult | Enforced FK | Cascade delete |
| GridCycle -> LiveOrder | Logical only | Shared by `GridCycleId` string |
| LiveOrder -> LiveFill | Logical only | Shared by `OrderId` string |
| User -> GridCycle / LiveOrder / LiveFill | Logical only | String `UserId` fields used for tenancy |

## Design Inconsistencies

The current implementation has several model inconsistencies worth documenting because they affect future extensions:

- `Strategy.UserId`, `GridCycle.UserId`, `LiveOrder.UserId`, and `LiveFill.UserId` are string identifiers, while `User.Id` is a `Guid`. That prevents database-enforced tenant relationships for those tables.
- `GridCycle`, `LiveOrder`, and `LiveFill` use public mutable setters instead of the domain pattern used by the rest of the model.
- `StrategyReview` is logically tied to a `StrategyRevision`, but the database only enforces a foreign key to `Strategy`.
- `BacktestRun` stores optional strategy provenance fields, but those are not enforced as foreign keys.
- `OptimizationRun` has no stored link back to a saved strategy, even though optimization is conceptually a strategy-authoring feature.

## Future Recommendations

- Migrate `GridCycle`, `LiveOrder`, and `LiveFill` to the `static Create(...)` plus private-setter pattern used elsewhere in the domain.
- Add a persisted `StrategyRun` entity if live session tracking becomes a first-class feature.
- Consider event sourcing or append-only audit tables for live-trading state changes.
- Add a persisted `RiskEvent` entity if risk-engine decisions need historical analysis or compliance review.
- Align tenant identifiers so strategy and live-trading tables can use enforced foreign keys to `User`.