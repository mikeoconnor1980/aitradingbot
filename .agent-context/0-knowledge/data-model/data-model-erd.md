# Data Model ERD

This ERD reflects the entities currently mapped in `TradingAppDbContext`. It intentionally removes the earlier aspirational tables and distinguishes between relationships enforced by EF Core foreign keys and relationships that are only tracked by application-level identifiers.

## Relationship Legend

- Labels beginning with `FK` are enforced by the database schema.
- Labels beginning with `logical` describe application-level links that are present in columns and repositories but are not configured as EF Core foreign keys.

## Entity Relationship Diagram

```mermaid
erDiagram
    User {
        guid Id PK
        string Email UK
        string DisplayName
        string PasswordHash nullable
        long CreatedAtUtc
        bool IsActive
        string PreferredNetwork
        string AuthProvider nullable
        string ExternalProviderId nullable
    }

    Subscription {
        guid Id PK
        guid UserId FK
        int Tier
        int Status
        long StartedAtUtc
        long ExpiresAtUtc
        long CreatedAtUtc
    }

    UserWalletAddress {
        guid Id PK
        guid UserId FK
        string Exchange
        string WalletAddress
        long CreatedAtUtc
        bool IsActive
    }

    Strategy {
        guid Id PK
        string UserId
        string Name
        string StrategyType
        string ConfigJson
        int Version
        bool IsActive
        bool IsRunning
        decimal HighWaterMarkUsd nullable
        long CreatedAtUtc
        long UpdatedAtUtc
    }

    StrategyRevision {
        guid Id PK
        guid StrategyId FK
        int RevisionNumber
        string ConfigJson
        string Source
        string Label nullable
        string ChangeSummary
        long CreatedAtUtc
    }

    StrategyReview {
        guid Id PK
        guid StrategyId FK
        int RevisionNumber
        string ReviewMarkdown
        string ModelName
        bool IsFallback
        long CreatedAtUtc
    }

    Candle {
        long Id PK
        string Source
        string Symbol
        string Interval
        long Timestamp
        decimal Open
        decimal High
        decimal Low
        decimal Close
        decimal Volume
        int NumTrades
    }

    FundingRate {
        long Id PK
        string Symbol
        long Timestamp
        decimal Rate
        decimal MarkPrice
    }

    BacktestRun {
        guid Id PK
        string Symbol
        string IntervalsJson
        long StartDateUtc
        long EndDateUtc
        string StrategyConfigJson
        string ExecutionConfigJson
        decimal InitialCapital
        int Status
        int Progress
        int TotalCandles
        string ErrorMessage nullable
        int CandlesReplayed
        long ElapsedMs
        decimal TotalPnl
        decimal MaxDrawdown
        string TradesJson
        string EquityTimeSeriesJson
        bool AuditLogEnabled
        guid StrategyId nullable
        int StrategyRevisionId nullable
        long CreatedAtUtc
    }

    GridCycle {
        guid Id PK
        string GridCycleId UK
        string StrategyName
        string Symbol
        decimal AnchorPrice
        int TotalLevels
        int FilledLevels
        string Lifecycle
        datetime StartedAtUtc
        datetime ClosedAtUtc nullable
        string CloseReason nullable
        decimal RealisedPnl nullable
        string UserId
    }

    LiveOrder {
        guid Id PK
        string OrderId UK
        string GridCycleId
        int Level
        string Symbol
        string Side
        string OrderType
        decimal Price
        decimal Size
        string TradeType
        string Status
        datetime PlacedAtUtc
        datetime FilledAtUtc nullable
        datetime CancelledAtUtc nullable
        string UserId
    }

    LiveFill {
        guid Id PK
        string OrderId
        string Symbol
        string Side
        string Direction
        decimal Price
        decimal Size
        decimal Fee
        decimal ClosedPnl
        datetime FilledAtUtc
        string UserId
    }

    LlmContextSnapshot {
        guid Id PK
        string Symbol
        string MarketSentiment
        string MacroRegime
        string EventRisk
        decimal Confidence
        string DerivedRegime
        string Summary
        long GeneratedAtUtc
    }

    MacroEvent {
        guid Id PK
        string Provider
        string ProviderEventId
        string Title
        string Country
        string Currency
        string Category
        long ScheduledAtUtc
        long ReleasedAtUtc nullable
        int Importance
        int Status
        long BlockStartUtc
        long BlockEndUtc
        long LastSeenUtc
        long CreatedAtUtc
        long UpdatedAtUtc
    }

    MacroSyncRun {
        guid Id PK
        string Provider
        long StartedAtUtc
        long CompletedAtUtc nullable
        bool Succeeded
        int EventsFetched
        int EventsInserted
        int EventsUpdated
        string Error nullable
    }

    OptimizationRun {
        guid Id PK
        string Symbol
        long StartDateUtc
        long EndDateUtc
        decimal InitialCapital
        string SweepConfigJson
        string ThresholdsJson
        int TotalCombinations
        int CompletedCount
        int QualifiedCount
        int FailedCount
        int Status
        string ErrorMessage nullable
        long ElapsedMs
        long CreatedAtUtc
    }

    OptimizationResult {
        guid Id PK
        guid OptimizationRunId FK
        int Rank
        decimal FitnessScore
        string StrategyConfigJson
        string SignalDescription
        decimal TotalPnl
        decimal WinRate
        decimal MaxDrawdown
        int TotalTrades
        decimal OosTotalPnl nullable
        decimal OosFitnessScore nullable
        decimal SharpeRatio nullable
        decimal SortinoRatio nullable
        decimal ProfitFactor nullable
        decimal CalmarRatio nullable
    }

    User ||--o{ Subscription : "FK UserId"
    User ||--o{ UserWalletAddress : "FK UserId"
    Strategy ||--o{ StrategyRevision : "FK StrategyId"
    Strategy ||--o{ StrategyReview : "FK StrategyId"
    OptimizationRun ||--o{ OptimizationResult : "FK OptimizationRunId"

    User ||--o{ Strategy : "logical UserId"
    User ||--o{ GridCycle : "logical UserId"
    User ||--o{ LiveOrder : "logical UserId"
    User ||--o{ LiveFill : "logical UserId"
    Strategy ||--o{ BacktestRun : "logical StrategyId"
    StrategyRevision ||--o| StrategyReview : "logical StrategyId + RevisionNumber"
    GridCycle ||--o{ LiveOrder : "logical GridCycleId"
    LiveOrder ||--o{ LiveFill : "logical OrderId"
```

## Notes On Schema Reality

| Topic | Current Reality |
|---|---|
| Tenant ownership | `Subscription` and `UserWalletAddress` use real `User` foreign keys. `Strategy`, `GridCycle`, `LiveOrder`, and `LiveFill` store tenant identity as strings without an FK. |
| Strategy configuration | There is no `StrategyConfig` table. Configuration is embedded as JSON on `Strategy`, with immutable snapshots stored on `StrategyRevision`. |
| Live trading persistence | The schema records `GridCycle`, `LiveOrder`, and `LiveFill`, but the links between them are identifier-based rather than FK-based. |
| Backtesting | `BacktestRun` merges job metadata and result payloads into one table. `StrategyId` and `StrategyRevisionId` are optional provenance fields, not enforced relationships. |
| Optimization | `OptimizationRun` and `OptimizationResult` are persisted. Only `OptimizationResult -> OptimizationRun` is a database-enforced relationship. |
| AI review | `StrategyReview` is unique per `(StrategyId, RevisionNumber)`, but the database does not enforce a composite FK to `StrategyRevision`. |

## Entities Intentionally Excluded

These items appeared in the original ERD but are not persisted in the current schema:

- `UserExchangeCredential`
- `StrategyConfig`
- `StrategyRun`
- `StrategyPerformance`
- `StrategyExecutionCheckpoint`
- `Signal`
- `GridState`
- `GridPlan`
- `Order`
- `Fill`
- `Position`
- `Backtest`
- `BacktestResult`
- `StrategyStateSnapshot`
- `CounterfactualBranch`
- `RiskEvent`
- `AuditLog`

---

### Operations

**RiskEvent** — Records when the RiskEngine blocks, modifies, or flags a signal. Audit trail for risk decisions.
- Event types: `SignalBlocked`, `PositionSizeReduced`, `CooldownTriggered`, `DailyLossLimitHit`, `MaxLeverageExceeded`

**AuditLog** — General-purpose audit trail for platform actions (user logins, config changes, admin actions).

---

## Enum Reference

| Entity | Field | Values |
|---|---|---|
| Subscription | Plan | Basic, Pro |
| Subscription | Status | Active, Paused, Cancelled, Expired |
| Strategy | StrategyType | GridStrategy, TrendBreakoutStrategy, MeanReversionStrategy |
| StrategyRun | Status | Running, Stopped, Error |
| Signal | SignalType | DeployGrid, CancelGrid, TakeProfit, FlattenPosition, OpenHedge, AdjustHedge, CloseHedge, PauseStrategy, Cooldown |
| Signal | Status | Generated, Validated, Approved, Executed |
| Order | Side | Buy, Sell |
| Order | OrderType | Limit, Market |
| Order | Status | Pending, Open, PartiallyFilled, Filled, Cancelled |
| Position | Direction | Long, Short |
| Position | Status | Open, Closed |
| GridState | LifecyclePhase | Inactive, Planning, Deploying, Active, PartiallyFilled, FullyFilled, Closing, Closed |
| LlmSnapshot | MarketSentiment | Bullish, Neutral, Bearish |
| LlmSnapshot | MacroRegime | RiskOn, Neutral, RiskOff |
| LlmSnapshot | EventRisk | Low, Medium, High |
| Backtest | Status | Pending, Running, Completed, Failed |
| RiskEvent | EventType | SignalBlocked, PositionSizeReduced, CooldownTriggered, DailyLossLimitHit, MaxLeverageExceeded |

---

## Multi-Tenancy

All entities except `Candle` and `LlmSnapshot` are tenant-scoped by `UserId`.

Market data (Candle) and LLM context (LlmSnapshot) are shared resources — they are computed once and consumed by all active subscribers.

All database queries must filter by `UserId` to enforce data isolation.

---

## EF Core Mapping Notes

- All GUIDs are `Guid` type — generated application-side
- JSON columns (`ConfigJson`, `PayloadJson`, `GridLevelsJson`, etc.) use EF Core owned types or `HasColumnType("jsonb")` in PostgreSQL / `nvarchar(max)` in SQL Server
- `EncryptedPrivateKey` in `UserExchangeCredential` is encrypted at rest; in Azure phase stored in Key Vault
- Composite unique index on `StrategyExecutionCheckpoint` (UserId, Symbol, Timeframe) prevents duplicate execution
- Composite unique index on `Candle` (Symbol, Timeframe, OpenTime) prevents duplicate candle storage
