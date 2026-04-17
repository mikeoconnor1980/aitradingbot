# Custom Indicator Strategy Plugin

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-31T12:00:00Z
**Epic:** Pine Script Indicator Integration (Option F)

## User Story

As a **trader**, I want to **create a strategy that uses my custom indicator alerts as entry/exit signals** so that **the system automatically executes trades when my indicator conditions are met, using the existing grid controller, risk engine, and execution pipeline**.

### Business Value

This is the capstone of the Pine Script epic — it closes the loop from "paste indicator" to "automated trading." Without this, custom indicators produce signals (PBI 5) but there's no strategy that acts on them. This PBI creates a new `IStrategyEngine` implementation that is driven entirely by user-configured indicator alerts, enabling any TradingView indicator to become an automated trading strategy.

---

## Requirements

### Functional Requirements

- [ ] **`CustomIndicatorStrategy` implementation** — New `IStrategyEngine` implementation that evaluates `MarketContext.CustomIndicators` alert results instead of hardcoded grid setup logic. Returns `StrategyEvaluation { SetupDetected: true }` when any mapped alert fires
- [ ] **Strategy configuration entity** — New or extended entity `StrategyConfig` with: `Id`, `UserId`, `StrategyType` (`"grid"` or `"custom_indicator"`), `CustomIndicatorIds` (which indicators are active), `IsActive` (bool). JSON config field for strategy-specific settings
- [ ] **Strategy selection UI** — User can choose strategy type: "Grid Strategy" (existing) or "Custom Indicator Strategy" (new). When custom indicator is selected, user picks which saved indicators power it
- [ ] **Multi-indicator strategy** — A custom indicator strategy can use alerts from multiple custom indicators. All mapped alerts feed into the strategy evaluation. User configures priority/precedence if needed
- [ ] **Signal routing** — When `CustomIndicatorStrategy` detects a setup:
  - If alert maps to `DeployGrid` → emit signal, `GridController` deploys grid as usual
  - If alert maps to `TakeProfit` → emit signal, `GridController` processes take-profit
  - If alert maps to `OpenHedge` / `CloseHedge` → emit signal, hedge logic processes
  - If alert maps to `EmergencyExit` → emit signal, emergency exit logic processes
  - If alert maps to `IndicatorAlert` → log only, no execution action
- [ ] **Strategy activation API** — `POST /api/strategy/activate` with `{ strategyType, customIndicatorIds[], asset, timeframe }`. Validates indicators exist and have mappings
- [ ] **Strategy status endpoint** — `GET /api/strategy/status` returns current strategy type, active indicators, last evaluation time, last signal emitted
- [ ] **Scheduling integration** — `StrategyScheduler` recognizes custom indicator strategies. On candle close, fans out to users with active custom indicator strategies, calls `CustomIndicatorStrategy.EvaluateAsync()`
- [ ] **Backtest compatibility** — `CustomIndicatorStrategy` works with the existing backtest pipeline. `BacktestMarketContextBuilder` populates `CustomIndicators` the same way live does. Backtest replay engine calls `CustomIndicatorStrategy.EvaluateAsync()` at each candle
- [ ] **Strategy dashboard integration** — Extend existing dashboard to show custom indicator strategy status: active/paused, which indicators, recent signals, P&L if grid is deployed

### Non-Functional Requirements

- [ ] `CustomIndicatorStrategy.EvaluateAsync()` completes in < 10ms (alert result checking only — computation already done in `MarketContextBuilder`)
- [ ] No regression in existing grid strategy behaviour
- [ ] Strategy switch (grid ↔ custom indicator) is clean — pauses current strategy, activates new one
- [ ] Custom indicator strategy integrates with existing risk limits (max position size, max drawdown, etc.)

---

## User Flow

### Setting Up a Custom Indicator Strategy

1. User has created and saved indicators "EMA Crossover" and "RSI Divergence" (PBI 2)
2. User has configured alert mappings: "EMA Crossover Buy Signal" → `DeployGrid`, "RSI > 70" → `TakeProfit` (PBI 5)
3. User navigates to Strategy page
4. User selects "Custom Indicator Strategy" (instead of "Grid Strategy")
5. User picks indicators: "EMA Crossover" ✓, "RSI Divergence" ✓
6. User selects asset: BTC-PERP, timeframe: 15m
7. User clicks "Activate"
8. System validates: indicators exist, have alert mappings, asset/timeframe valid
9. Strategy is now active. Dashboard shows: "Custom Indicator Strategy — Active — 2 indicators — BTC-PERP 15m"

### Strategy Executing

1. 15m candle closes
2. `CandleClock` fires → `StrategyScheduler` picks up user
3. `MarketContextBuilder` fetches candles, computes built-in indicators, computes custom indicators (PBI 3)
4. Alert evaluation (PBI 5) detects: EMA 21 crossed above EMA 50, RSI = 28
5. "EMA Crossover Buy Signal" alert fires → cooldown check passes
6. `CustomIndicatorStrategy.EvaluateAsync()` finds alert triggered with `DeployGrid` mapping → returns `StrategyEvaluation { SetupDetected: true, Reason: "Alert 'Buy Signal' from 'EMA Crossover'" }`
7. `GridController` receives evaluation → deploys grid
8. `TradingSignal` emitted → `RiskEngine` validates → `ExecutionEngine` places orders
9. Dashboard updates: last signal "DeployGrid" at 14:30 UTC

### Backtesting a Custom Indicator Strategy

1. User navigates to Backtest page
2. Selects "Custom Indicator Strategy" with same indicators and mappings
3. Selects historical date range
4. Backtest engine runs:
   - For each candle in range, `BacktestMarketContextBuilder` computes indicators
   - `CustomIndicatorStrategy.EvaluateAsync()` checks alerts at each candle
   - Grid deploys/exits based on indicator signals
   - Results show P&L, drawdown, signal count
5. User compares backtest results with grid-only strategy

### Switching Back to Grid Strategy

1. User navigates to Strategy page
2. Selects "Grid Strategy"
3. System pauses custom indicator strategy, activates grid strategy
4. Existing grid configuration loads
5. No signals lost — strategy switch happens between candle closes

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| User activates strategy with no alert mappings | Validation error: "Selected indicators have no alert-signal mappings configured" |
| Custom indicator becomes invalid (user edits and breaks Pine Script) | Strategy pauses, notification: "Strategy paused: indicator 'X' is no longer valid" |
| All indicators deleted | Strategy auto-pauses |
| No candle data for configured timeframe | Strategy evaluation skipped for that candle, logged as warning |
| Multiple alerts fire simultaneously | All mapped signals emitted in sequence; `RiskEngine` may reject conflicting signals (e.g., `DeployGrid` + `EmergencyExit`) |

---

## Technical Considerations

### Bounded Context

**Context:** `TradePilot.Application/Trading` — the strategy lives alongside the existing grid strategy components.

### New/Modified Components

#### Backend

| Component | Layer | Action |
|-----------|-------|--------|
| `CustomIndicatorStrategy` | Application/Trading/Services | **New** — `IStrategyEngine` implementation. Checks `MarketContext.CustomIndicators` for triggered alerts with active mappings |
| `StrategyConfig` | Domain/Entities | **New or modified** — Entity tracking which strategy type is active per user. If a config entity already exists, extend it |
| `IStrategyConfigRepository` | Application/Abstractions/Repositories | **New** — Repository for strategy configuration |
| `StrategyConfigRepository` | Persistence/Repositories | **New** — EF Core implementation |
| `StrategyController` | Api/Controllers | **New or modified** — Endpoints for activate/deactivate/status |
| `StrategyScheduler` | Application/Scheduling | **Modified** — Route to correct `IStrategyEngine` implementation based on user's active strategy type |
| `BacktestMarketContextBuilder` | Application/Backtesting | **Modified** — Compute custom indicators during backtest replay |
| `BacktestEngine` | Application/Backtesting | **Modified** — Use `CustomIndicatorStrategy` when backtest config specifies it |
| `TradePilotDbContext` | Persistence | **Modified** — Add `DbSet<StrategyConfig>` |
| EF Migration | Persistence/Migrations | **New** — Add strategy config table |

#### Frontend

| Component | Action |
|-----------|--------|
| `StrategyConfigComponent` | **New** — Strategy selection page: choose type, select indicators, activate |
| `StrategyDashboardComponent` | **Modified** — Show custom indicator strategy status, active indicators, recent signals |
| `StrategyService` | **New or modified** — Angular service for strategy activation/status endpoints |

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/strategy/activate` | Activate a strategy (grid or custom indicator) |
| POST | `/api/strategy/deactivate` | Deactivate current strategy |
| GET | `/api/strategy/status` | Get current strategy status |

### Strategy Engine Resolution

```csharp
// StrategyScheduler resolves the correct engine per user:
IStrategyEngine engine = userConfig.StrategyType switch
{
    "grid" => _gridStrategyEngine,
    "custom_indicator" => _customIndicatorStrategy,
    _ => throw new InvalidOperationException($"Unknown strategy type: {userConfig.StrategyType}")
};

var evaluation = await engine.EvaluateAsync(marketContext, userConfig.ConfigJson, ct);
```

### CustomIndicatorStrategy Implementation (Conceptual)

```csharp
public class CustomIndicatorStrategy : IStrategyEngine
{
    public Task<StrategyEvaluation> EvaluateAsync(
        MarketContext context, string configJson, CancellationToken ct)
    {
        // configJson contains: { customIndicatorIds: [...] }
        // context.CustomIndicators contains computed results with AlertsTriggered
        
        foreach (var (indicatorId, result) in context.CustomIndicators)
        {
            if (result.AlertsTriggered.Any())
            {
                return Task.FromResult(new StrategyEvaluation
                {
                    SetupDetected = true,
                    Reason = $"Alert '{result.AlertsTriggered.First()}' from indicator '{indicatorId}'"
                });
            }
        }
        
        return Task.FromResult(new StrategyEvaluation { SetupDetected = false });
    }
}
```

---

## Dependencies

- **PBI: Custom Indicator CRUD** — provides saved indicator entities
- **PBI: Indicator Computation Pipeline** — populates `MarketContext.CustomIndicators`
- **PBI: Alert Condition → Signal Mapping** — provides alert evaluation and signal emission
- **Existing:** `IStrategyEngine`, `StrategyEvaluation`, `GridController`, `RiskEngine`, `ExecutionEngine`, `StrategyScheduler`, `BacktestEngine`

---

## Out of Scope

- Strategy optimization (auto-tuning indicator parameters based on backtest results)
- Multi-asset strategies (single strategy across multiple symbols)
- Strategy marketplace (sharing strategies between users)
- Complex strategy logic beyond "alert fires → emit signal" (e.g., state machines, conditional sequences)
- Paper trading mode separate from backtest

---

## Acceptance Criteria

- [ ] `CustomIndicatorStrategy` implements `IStrategyEngine` and returns `SetupDetected: true` when any mapped alert fires
- [ ] `POST /api/strategy/activate` activates a custom indicator strategy with selected indicators
- [ ] `GET /api/strategy/status` returns correct strategy type, active indicators, and last evaluation time
- [ ] `StrategyScheduler` correctly routes to `CustomIndicatorStrategy` for users with custom indicator strategy active
- [ ] When an alert fires with a `DeployGrid` mapping, `GridController` receives and processes the signal
- [ ] When an alert fires with a `TakeProfit` mapping, take-profit logic receives and processes the signal
- [ ] All signals pass through `RiskEngine` — custom indicator signals do not bypass risk checks
- [ ] Strategy can be switched between grid and custom indicator without data loss
- [ ] Custom indicator strategy works with backtest pipeline — same evaluation logic, same signals
- [ ] Backtest results include signal count, timing, and P&L for custom indicator strategies
- [ ] Strategy dashboard shows custom indicator strategy status and recent activity
- [ ] Validation rejects activation if selected indicators have no alert mappings
- [ ] Strategy auto-pauses if all linked indicators become invalid
- [ ] Unit tests verify `CustomIndicatorStrategy.EvaluateAsync()` with mock `MarketContext`
- [ ] Integration test verifies full pipeline: candle close → indicator computation → alert fire → strategy evaluation → signal emission

### Release Notes Information

- **Heading**: Custom Indicator Automated Strategy
- **Release note type**: Feature
- **Release Note Summary**: Create automated trading strategies powered by your custom Pine Script indicators. Map indicator alerts to trading actions and let the system execute automatically — using the same grid controller, risk engine, and execution pipeline as the built-in grid strategy. Includes backtest support.
- **Release Notes Audience**: Product
- **Breaking Change**: No (new strategy type alongside existing grid strategy)
