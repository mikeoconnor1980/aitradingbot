<!-- markdownlint-disable-file -->

# Task Details: Knowledge Base Audit & Refresh

## Phase 2: Domain Model & Data (04, ERD)

## Standards and Knowledge References

- `.github/instructions/agent-knowledge.instructions.md` — documentation standards
- Entity details should focus on structure and key relationships, not every property

### Task 2.1: Update `04-domain-model.md` {#task-21-update-domain-model}

Realign the domain model documentation with actual entities.

- **Complexity**: High
- **Risk Factors**: Many entity changes to track accurately
- **Files**:
  - `.agent-context/0-knowledge/04-domain-model.md` — update
- **Success**:
  - `UserExchangeCredential` replaced with `UserWalletAddress`
  - `User` entity fields updated (PreferredNetwork, AuthProvider, ExternalProviderId, factory methods)
  - `Subscription` entity uses `SubscriptionTier` enum with correct statuses
  - Non-existent entities removed: `Order`, `Fill`, `Signal` (persisted), `BotState`
  - New entities added: `GridCycle`, `LiveOrder`, `LiveFill`, `LlmContextSnapshot`, `MacroEvent`, `MacroSyncRun`, `OptimizationRun`, `OptimizationResult`, `StrategyReview`
  - Mutable setter inconsistency noted for GridCycle/LiveOrder/LiveFill
  - Future Recommendations section added

#### Changes Required

**Entities to REPLACE:**

| Old Entity | New Entity | Key Differences |
|---|---|---|
| `UserExchangeCredential` | `UserWalletAddress` | No `EncryptedPrivateKey` — only stores `WalletAddress`. Fields: `Id`, `UserId`, `Exchange`, `WalletAddress`, `CreatedAtUtc`, `IsActive` |

**Entities to UPDATE:**

| Entity | Changes |
|---|---|
| `User` | Add: `PreferredNetwork`, `AuthProvider`, `ExternalProviderId`. Two factory methods: `User.Create(email, displayName, passwordHash)` for email/password, `User.CreateExternal(email, displayName, authProvider, externalProviderId)` for OAuth. Add `User.LinkExternalProvider(authProvider, externalProviderId)`. |
| `Subscription` | `Plan` → `Tier` (`SubscriptionTier.Free = 0` only). Statuses: `Active=0`, `Expired=1`, `Cancelled=2` (no `Paused`). No `ExternalBillingId` — no Stripe integration. |
| `Strategy` | Add: `HighWaterMarkUsd` (decimal?), `UpdateHighWaterMark()` method. `IsRunning` still a stub. |

**Entities to REMOVE (never implemented as persisted entities):**

- `Order` → replaced by `LiveOrder` (different structure)
- `Fill` → replaced by `LiveFill` (different structure)
- `Signal` (persisted) → signals are in-memory only, never persisted
- `BotState` → runtime state managed by `GridState` (in-memory) and `GridCycle` (persisted)

**Entities to ADD:**

| Entity | Purpose | Key Fields |
|---|---|---|
| `GridCycle` | Persisted record of a live grid trading cycle | `GridCycleId`, `StrategyName`, `Symbol`, `AnchorPrice`, `TotalLevels`, `FilledLevels`, `Lifecycle`, `StartedAtUtc`, `ClosedAtUtc`, `CloseReason`, `RealisedPnl`, `UserId` |
| `LiveOrder` | Persisted live orders with grid cycle linkage | `OrderId`, `Level`, `Side`, `OrderType`, `Status`, `PlacedAtUtc`, `FilledAtUtc` |
| `LiveFill` | Persisted fill records | `OrderId`, `Symbol`, `Side`, `Direction`, `Price`, `Size`, `Fee`, `ClosedPnl`, `FilledAtUtc` |
| `LlmContextSnapshot` | Persisted LLM analysis snapshots | `Symbol`, `MarketSentiment`, `MacroRegime`, `EventRisk`, `Confidence`, `DerivedRegime`, `Summary`, `GeneratedAtUtc` |
| `MacroEvent` | Economic calendar event entity | `Provider`, `ProviderEventId`, `Title`, `Country`, `Currency`, `Importance`, `BlockStartUtc`, `BlockEndUtc` |
| `MacroSyncRun` | Audit log of macro calendar sync runs | `Provider`, `StartedAtUtc`, `EventsFetched/Inserted/Updated`, `Error` |
| `OptimizationRun` | Strategy parameter sweep run | Links to strategy, config, status tracking |
| `OptimizationResult` | Individual optimization result | Metrics (Sharpe, Sortino, Calmar, Kelly), IS/OOS splits |
| `StrategyReview` | Per-revision AI review | `ModelName`, `IsFallback`, review markdown content |

**Note inconsistency**: `GridCycle`, `LiveOrder`, `LiveFill` use mutable public setters instead of the established `static Create` + private setters pattern used by all other domain entities.

**Add Future Recommendations:**
- Migrate `GridCycle`/`LiveOrder`/`LiveFill` to use `static Create` factory + private setter pattern
- Add `StrategyRun` entity for tracking live trading sessions
- Consider event sourcing for live trading audit trail
- Add `RiskEvent` entity for persisting risk engine decisions

---

### Task 2.2: Rewrite `data-model/data-model-erd.md` {#task-22-rewrite-erd}

Rebuild the ERD from actual database entities.

- **Complexity**: High
- **Risk Factors**: Must accurately represent all FK relationships
- **Files**:
  - `.agent-context/0-knowledge/data-model/data-model-erd.md` — rewrite
- **Success**:
  - All ~17 actual entities are represented in the ERD
  - Relationships (FK, 1:many, etc.) match actual EF Core configuration
  - Non-existent entities removed (StrategyConfig, StrategyRun, StrategyPerformance, GridPlan, Position, Signal, etc.)
  - ERD uses Mermaid syntax

#### Changes Required

The ERD needs a complete rewrite. Summary of entity differences:

| ERD Entity | Actual Status |
|---|---|
| `User` | ✅ + new fields |
| `Subscription` | ✅ different fields |
| `UserExchangeCredential` | ❌ → `UserWalletAddress` |
| `StrategyConfig` (separate entity) | ❌ embedded JSON in `Strategy` |
| `StrategyRun` | ❌ does not exist |
| `StrategyPerformance` | ❌ does not exist |
| `StrategyExecutionCheckpoint` | ❌ runtime only |
| `Signal` (persisted) | ❌ in-memory only |
| `GridState` (persisted) | ❌ runtime only → `GridCycle` |
| `GridPlan` | ❌ does not exist |
| `Order` / `Fill` | ❌ → `LiveOrder` / `LiveFill` |
| `Position` | ❌ fetched live from exchange |
| `LlmSnapshot` | ⚠️ → `LlmContextSnapshot` + new fields |
| `Backtest` + `BacktestResult` | ❌ → merged `BacktestRun` |
| `StrategyStateSnapshot` | ❌ does not exist |
| `CounterfactualBranch` | ❌ does not exist |
| `RiskEvent` | ❌ does not exist |
| `AuditLog` | ❌ does not exist |

**Entities to ADD to ERD**: `StrategyRevision`, `StrategyReview`, `GridCycle`, `LiveOrder`, `LiveFill`, `MacroEvent`, `MacroSyncRun`, `OptimizationRun`, `OptimizationResult`

**Key Relationships**:
- `User` 1→N `Subscription`, `UserWalletAddress`, `Strategy`
- `Strategy` 1→N `StrategyRevision`, `BacktestRun`, `OptimizationRun`
- `StrategyRevision` 1→0..1 `StrategyReview`
- `GridCycle` 1→N `LiveOrder`
- `LiveOrder` 1→N `LiveFill`
- `OptimizationRun` 1→N `OptimizationResult`

## Phase Success Criteria

- Domain model doc accurately lists all persisted entities
- ERD matches actual database schema (no phantom entities)
- Relationships are correctly documented
- Non-existent entities clearly removed
