# Alert Condition → Signal Mapping

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-31T12:00:00Z
**Epic:** Pine Script Indicator Integration (Option F)

## User Story

As a **trader**, I want to **map alert conditions extracted from my Pine Script indicators to trading signals** so that **indicator events like crossovers and threshold breaches can trigger automated strategy actions (deploy grid, take profit, open hedge) through the existing execution pipeline**.

### Business Value

This PBI bridges indicator analysis and automated execution. Without it, custom indicators are visual-only — the user sees signals on the chart but must manually act. With alert-to-signal mapping, a user's Pine Script `alertcondition("Buy Signal")` can automatically trigger a `DeployGrid` action, completing the loop from indicator → signal → risk engine → order execution.

---

## Requirements

### Functional Requirements

- [ ] **Alert-to-signal mapping entity** — New entity `AlertSignalMapping` with: `Id`, `UserId`, `CustomIndicatorId`, `AlertTitle` (from extracted `alertcondition()`), `SignalType` (string — maps to existing `TradingSignal.SignalType` values), `IsEnabled` (bool), `Parameters` (JSON — additional signal parameters like grid spacing)
- [ ] **Supported signal types** — Map alerts to existing `TradingSignal.SignalType` values: `DeployGrid`, `TakeProfit`, `OpenHedge`, `CloseHedge`, `EmergencyExit`, plus a new `IndicatorAlert` type for informational-only signals
- [ ] **Mapping configuration API** — CRUD endpoints for alert-signal mappings:
  - `GET /api/indicators/{id}/mappings` — list mappings for an indicator
  - `PUT /api/indicators/{id}/mappings` — bulk upsert mappings for an indicator
- [ ] **Mapping configuration UI** — Within the indicator editor (PBI 2), a "Signal Mapping" section showing:
  - Each extracted `alertcondition()` as a row
  - Each extracted `plotshape()` boolean condition as a row
  - Derived conditions (crossovers detected between plotted series) as rows
  - Dropdown to select target signal type (or "None" to disable)
  - Optional parameters per signal type (e.g., grid size for `DeployGrid`)
  - Enable/disable toggle per mapping
- [ ] **Alert evaluation on candle close** — During backend indicator computation (PBI 3), evaluate extracted alert conditions at each candle close. Return `AlertsTriggered: string[]` in `CustomIndicatorResult`
- [ ] **Condition evaluation engine** — Evaluate `ConditionExpression` trees (from PBI 1 extractor) against computed indicator values:
  - Comparisons: `ema21 > ema50`, `rsi < 30`
  - Logical operators: `condition1 and condition2`, `not condition`
  - Function conditions: `ta.crossover(ema21, ema50)` → detect ema21 crossing above ema50 between previous and current candle
  - `ta.crossunder(a, b)` → detect a crossing below b
- [ ] **Signal emission** — When an alert fires and has an active mapping, emit a `TradingSignal` with:
  - `SignalType` from the mapping
  - `Symbol` from the current market context
  - `Reason` including indicator name and alert title (e.g., "Custom indicator 'My EMA Crossover' alert 'Buy Signal'")
  - `Parameters` merged from mapping configuration
- [ ] **Cooldown / debounce** — Configurable per-mapping cooldown period to prevent rapid-fire signals (e.g., "Don't re-emit this signal within 5 candles of the last emission")
- [ ] **Alert history log** — Record alert triggers in a lightweight log: `{ Timestamp, IndicatorId, AlertTitle, SignalType, Symbol, WasEmitted (bool — false if cooldown suppressed it) }`

### Non-Functional Requirements

- [ ] Condition evaluation adds < 5ms overhead per indicator per candle close
- [ ] Alert-to-signal mapping configuration changes take effect on the next candle close (no restart required)
- [ ] Signal emission integrates with existing `RiskEngine` validation — custom indicator signals do not bypass risk checks
- [ ] Alert history log capped at 1000 entries per user (rolling window)

---

## User Flow

### Configuring Alert Mappings

1. User opens their saved "EMA Crossover + RSI" indicator in the editor
2. User scrolls to the "Signal Mapping" section
3. Section shows extracted alerts:
   - `alertcondition: "Buy Signal"` (condition: `ta.crossover(ema21, ema50) and rsi < 30`)
   - `plotshape: "Overbought Warning"` (condition: `rsi > 70`)
4. User maps "Buy Signal" → `DeployGrid` signal type
5. User sets parameters: grid size = 5, grid spacing = 0.5%
6. User sets cooldown: 10 candles
7. User maps "Overbought Warning" → `IndicatorAlert` (informational only)
8. User clicks "Save Mappings"

### Signal Triggering (Automated)

1. CandleClock fires on 15m candle close
2. Strategy scheduler processes user with active indicator strategy
3. MarketContextBuilder computes custom indicators (PBI 3)
4. Alert evaluation detects: EMA 21 crossed above EMA 50, and RSI = 28 (< 30)
5. "Buy Signal" alert triggered → mapping exists to `DeployGrid`
6. Cooldown check: last "Buy Signal" was 15 candles ago (> 10 cooldown) → proceed
7. System emits `TradingSignal { SignalType: "DeployGrid", Symbol: "BTC-PERP", Reason: "Custom indicator 'EMA Crossover + RSI' alert 'Buy Signal'", Parameters: { gridSize: 5, gridSpacing: 0.005 } }`
8. Signal passes to `RiskEngine` for validation → then to `ExecutionEngine`
9. Alert logged to history

### Signal Suppressed by Cooldown

1. Same scenario, but last "Buy Signal" was 3 candles ago (< 10 cooldown)
2. Alert is triggered but signal is NOT emitted
3. Alert logged to history with `WasEmitted: false`
4. User can see in the alert history that the signal was suppressed

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Indicator has no `alertcondition()` or `plotshape()` | Signal Mapping section shows "No alert conditions found in this indicator" |
| Mapped signal type is not recognized | Validation error on save; mapping rejected |
| Alert condition evaluation fails (e.g., division by zero) | Log error, skip that alert, continue with others |
| Custom indicator entity deleted while mapping exists | Cascade delete mappings, or orphan-clean on next access |
| User maps alert to `DeployGrid` but no grid strategy is active | Signal emitted but GridController ignores it if no grid is configured — existing behavior |

---

## Technical Considerations

### Bounded Context

**Context:** Domain entity in `TradePilot.Domain`, evaluation in `TradePilot.Application/PineScript`, signal emission integrates with `TradePilot.Application/Trading`.

### New/Modified Components

#### Backend

| Component | Layer | Action |
|-----------|-------|--------|
| `AlertSignalMapping` | Domain/Entities | **New** — Mapping entity |
| `IAlertSignalMappingRepository` | Application/Abstractions/Repositories | **New** — Repository interface |
| `AlertSignalMappingRepository` | Persistence/Repositories | **New** — EF Core implementation |
| `ConditionEvaluator` | Application/PineScript/Services | **New** — Evaluates `ConditionExpression` trees against computed indicator values |
| `AlertEvaluationService` | Application/PineScript/Services | **New** — Runs alert evaluation for a custom indicator at a point in time. Returns triggered alerts |
| `SignalEmitter` | Application/PineScript/Services | **New** — Looks up mappings for triggered alerts, applies cooldown, emits `TradingSignal` |
| `AlertHistoryEntry` | Domain/Entities | **New** — Alert trigger log entry |
| `IndicatorController` | Api/Controllers | **Modified** — Add mapping endpoints |
| `CustomIndicatorComputeService` | Application/PineScript/Services | **Modified** — Call `AlertEvaluationService` during computation, return triggered alerts in result |
| `TradePilotDbContext` | Persistence | **Modified** — Add `DbSet<AlertSignalMapping>`, `DbSet<AlertHistoryEntry>` |
| EF Migration | Persistence/Migrations | **New** — Add mapping and alert history tables |

#### Frontend

| Component | Action |
|-----------|--------|
| `AlertMappingComponent` | **New** — Section within indicator editor showing alert conditions and mapping dropdowns |
| `AlertHistoryComponent` | **New** — List/panel showing recent alert trigger history with emit/suppress status |
| `AlertMappingService` | **New** — Angular service calling mapping endpoints |
| `IndicatorEditorComponent` | **Modified** — Integrate AlertMappingComponent |

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/indicators/{id}/mappings` | List alert-signal mappings for an indicator |
| PUT | `/api/indicators/{id}/mappings` | Bulk upsert mappings |
| GET | `/api/indicators/{id}/alert-history?limit=50` | Recent alert trigger history |

### Database Schema

```sql
CREATE TABLE AlertSignalMappings (
    Id TEXT PRIMARY KEY,
    UserId TEXT NOT NULL,
    CustomIndicatorId TEXT NOT NULL,
    AlertTitle TEXT NOT NULL,
    SignalType TEXT NOT NULL,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    CooldownCandles INTEGER NOT NULL DEFAULT 0,
    ParametersJson TEXT,
    CreatedUtc TEXT NOT NULL,
    UpdatedUtc TEXT NOT NULL,
    FOREIGN KEY (CustomIndicatorId) REFERENCES CustomIndicators(Id) ON DELETE CASCADE
);

CREATE TABLE AlertHistoryEntries (
    Id TEXT PRIMARY KEY,
    UserId TEXT NOT NULL,
    CustomIndicatorId TEXT NOT NULL,
    AlertTitle TEXT NOT NULL,
    Symbol TEXT NOT NULL,
    SignalType TEXT,
    WasEmitted INTEGER NOT NULL,
    SuppressedReason TEXT,
    TimestampUtc TEXT NOT NULL
);

CREATE INDEX IX_AlertSignalMappings_CustomIndicatorId ON AlertSignalMappings(CustomIndicatorId);
CREATE INDEX IX_AlertHistoryEntries_UserId_TimestampUtc ON AlertHistoryEntries(UserId, TimestampUtc DESC);
```

### Crossover Detection Algorithm

```
crossover(a, b) at candle N:
  return a[N] > b[N] AND a[N-1] <= b[N-1]

crossunder(a, b) at candle N:
  return a[N] < b[N] AND a[N-1] >= b[N-1]
```

Requires previous candle's indicator values, which are available from the computation pipeline.

---

## Dependencies

- **PBI: Pine Script Pattern Extractor** — provides `ConditionExpression` trees in extraction result
- **PBI: Custom Indicator CRUD** — provides `CustomIndicator` entities and editor UI to integrate into
- **PBI: Indicator Computation Pipeline** — provides computed indicator values that conditions evaluate against
- **Existing:** `TradingSignal` model, `RiskEngine`, `GridController`, `ExecutionEngine`

---

## Out of Scope

- Strategy-level logic for when to act on signals (see PBI: Custom Indicator Strategy Plugin)
- Notification channels (email, SMS, push) for alerts — future feature
- Alert conditions based on multiple indicators from different custom indicator definitions
- Complex alert scheduling (e.g., "only during London session")
- Backtesting of alert-signal mappings

---

## Acceptance Criteria

- [ ] `AlertSignalMapping` entity persists with correct schema and tenant isolation
- [ ] `PUT /api/indicators/{id}/mappings` creates/updates mappings
- [ ] `GET /api/indicators/{id}/mappings` returns mappings for an indicator
- [ ] Condition evaluator correctly evaluates comparison expressions (`ema > threshold`)
- [ ] Condition evaluator correctly evaluates logical expressions (`condition1 and condition2`)
- [ ] Condition evaluator correctly detects crossover events between two series
- [ ] When an alert fires and has an enabled mapping, a `TradingSignal` is emitted with correct type, symbol, reason, and parameters
- [ ] Cooldown suppresses signal emission when last emit was within cooldown period
- [ ] Suppressed alerts are logged to history with `WasEmitted: false`
- [ ] Emitted signals pass through `RiskEngine` — not bypass it
- [ ] Alert Mapping UI shows extracted alert conditions with signal type dropdowns
- [ ] Alert History UI shows recent triggers with emit/suppress status
- [ ] Mapping changes take effect on the next candle close without restart
- [ ] Unit tests for `ConditionEvaluator` with known expression trees
- [ ] Integration test: indicator computation → alert triggered → mapping lookup → signal emitted

### Release Notes Information

- **Heading**: Indicator Alert-to-Signal Mapping
- **Release note type**: Feature
- **Release Note Summary**: Map Pine Script alert conditions to automated trading signals. Configure which indicator alerts trigger grid deployment, take profit, or other actions. Includes cooldown controls and alert history logging.
- **Release Notes Audience**: Product
- **Breaking Change**: No (new tables, new signal type `IndicatorAlert`)
