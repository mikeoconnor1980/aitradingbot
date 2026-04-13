# Risk Management & Trade Sizing

## Overview

This document defines the risk management methodology for the trading platform.
The system uses **R-based position sizing** as the universal framework:
every trade's risk, reward, and position size derives from a single value — **R**.

---

## Core Concept: R (Risk)

R is the dollar amount a trader is willing to lose on any single trade.

```
R = AccountEquity × RiskPercent
```

Example: $10,000 account, risking 1% → R = $100

Position size follows from R and stop-loss distance:

```
PositionNotional = R / (StopLossDistance / EntryPrice)
```

Or equivalently:

```
PositionSizeCoins = R / StopLossDistance
```

Where `StopLossDistance` is the absolute price difference between entry and stop-loss.

This creates an **anti-martingale** effect: after wins, equity grows → R grows →
position sizes grow. After losses, equity shrinks → R shrinks → positions shrink.
The system self-corrects.

### Survivability

At 1% risk per trade, even 10 consecutive losses lose only ~9.6% of account
(due to compounding down). At 2%, 10 consecutive losses lose ~18%.

---

## Position Sizing Modes

The platform supports three sizing modes via `PositionSizeType`:

### 1. PercentWallet (existing)

Allocate X% of equity as the total position notional per grid level.

```
notional = equity × (positionSizeValue / 100)
```

Simple but does not directly control dollar risk — the actual R depends on
where the stop-loss is placed.

### 2. FixedNotional (existing)

Fixed USD amount per grid level.

```
notional = positionSizeValue
```

Same limitation — R is implicit, not controlled.

### 3. RiskBased (new)

The professional approach. "I want to risk exactly X% of my equity on this trade."
The system works backwards from R and the stop-loss to determine position size.

```
R = equity × riskPerTradePercent
positionNotional = R / (stopLossPercent / 100)
```

Example: $10,000 equity, 1% risk, 2% stop-loss:
- R = $100
- positionNotional = $100 / 0.02 = $5,000

The tighter the stop-loss, the larger the position (but the dollar risk stays constant at R).
The wider the stop-loss, the smaller the position.

For a grid with N levels, the total grid notional is divided across levels:

```
notionalPerLevel = positionNotional / gridLevels
```

---

## Leverage Aligned to R

### The Concept

Instead of choosing leverage arbitrarily, set leverage such that:
- **Margin posted ≈ R** (the dollar risk amount, plus a small buffer)
- **Stop-loss fires first** at the intended R-loss level
- **Liquidation sits slightly beyond the SL** as a catastrophic backstop only

### Stop-Loss vs Liquidation — Always Use the Stop-Loss

**The stop-loss is always the primary exit. Liquidation is the emergency backstop only.**

Hyperliquid liquidation works in two stages:

| Stage | Trigger | How It Closes | What You Keep |
|-------|---------|---------------|---------------|
| **Market liquidation** | Equity < maintenance margin | Market order to the book | Any remaining collateral after fill |
| **Backstop liquidation** | Equity < 2/3 of maintenance margin | Position transferred to HLP liquidator vault | **Nothing** — entire isolated margin forfeited |

Key facts from Hyperliquid docs:
- Maintenance margin = half of initial margin at max leverage (e.g., 1% for 50x assets, 2.5% for 20x assets)
- On backstop liquidation, **the maintenance margin is not returned** to the user
- Hyperliquid does **not** charge a clearance/liquidation fee on market liquidations (better than most CEXs)
- Mark price (which combines external CEX prices with the order book) triggers liquidation — this can diverge from book price in volatile conditions

Why the SL is always better:

1. **Liquidation fires at a worse price than SL** — it triggers beyond the SL level because
   the maintenance margin buffer places it further from entry
2. **Same execution mechanism** — both SL and market liquidation send market orders to the book,
   so slippage characteristics are identical, but at a worse trigger price for liquidation
3. **Backstop liquidation forfeits all margin** — if the market order doesn't fill and it
   falls to backstop, the HLP vault seizes the entire position and returns nothing
4. **The SL gives you control** — you choose the price; liquidation is at the exchange's mercy

The correct flow:

```
Entry Price
  ↓ price moves against you
Stop-Loss Price        ← SL fires here, you lose ≈ R (normal outcome)
  ↓ if SL somehow fails (gap, no liquidity, extreme volatility)
Liquidation Price      ← market order to book, lose R + buffer (rare)
  ↓ if still not filled
Backstop Liq Price     ← position seized, ALL margin lost (extreme)
```

### The Formula (with Buffer)

The naive formula `leverage = 1 / SL%` places liquidation *at* the stop-loss —
this is wrong because it means any slippage past the SL triggers liquidation
immediately with no room for the SL to execute first.

The correct formula adds a buffer for the maintenance margin:

```
leverage = 1 / (stopLossPercent / 100 + maintenanceMarginRate)
margin = positionNotional / leverage
positionNotional = R / (stopLossPercent / 100)
```

Where `maintenanceMarginRate` = 0.5 / maxLeverage for the asset (e.g., 1% for
BTC at 50x max leverage, 2.5% for a 20x max leverage alt).

This means margin posted is slightly more than R:

```
margin = R × (1 + maintenanceMarginRate / (stopLossPercent / 100))
```

| SL% | Maint. Margin (50x asset) | Leverage (with buffer) | Margin   | Liq. at |
|-----|:-------------------------:|:----------------------:|:--------:|:-------:|
| 1%  | 1%                        | 50x                    | ~2.0R    | ~2%     |
| 2%  | 1%                        | ~33x                   | ~1.5R    | ~3%     |
| 5%  | 1%                        | ~16x                   | ~1.2R    | ~6%     |
| 10% | 1%                        | ~9x                    | ~1.1R    | ~11%    |
| 20% | 1%                        | ~5x                    | ~1.05R   | ~21%    |

The tradeoff: you post slightly more than pure R as margin, but the SL can
fire cleanly before the liquidation engine ever comes into play.

### Isolated Margin Required

This approach **requires isolated margin** on Hyperliquid (not cross margin).
With isolated margin:
- Liquidation only affects the single position, not other open positions
- Margin is contained to that position
- Other positions and cross margin are untouched

With cross margin, a liquidation would cascade across all positions — defeating
the purpose of R-based risk containment.

### Benefits

- Dollar risk is always approximately R (controlled by the SL)
- Liquidation is a safety net, not the exit mechanism
- Different trades can use different leverage based on stop-loss distance
- Each position's risk is contained via isolated margin
- Remaining equity is available for other positions
- Maximum capital efficiency — margin ≈ R plus a small safety buffer

### Grid Variant

For a grid with N levels and a single portfolio stop-loss:

```
totalGridNotional = R / (stopLossPercent / 100)
notionalPerLevel = totalGridNotional / N
marginPerLevel ≈ R / N  (plus buffer)
leverage = 1 / (stopLossPercent / 100 + maintenanceMarginRate)
```

The overall grid stop-loss defines the total position size from R. Leverage is
set once per asset (Hyperliquid is per-asset, not per-order), and the margin is
divided across levels.

### Exchange Integration

The system must:
1. Use **isolated margin** mode for the asset
2. Call `SetLeverage` on the exchange before placing orders (per-asset on Hyperliquid)
3. Place the SL trigger order immediately after fills
4. The auto-calculated leverage replaces the current manual/decorative leverage config

---

## R-Multiple Targets

### Concept

Instead of arbitrary percentage take-profit levels, express targets as multiples of R:

| Target | Meaning                   | Example (R=$100) |
|--------|---------------------------|:----------------:|
| 1R     | Profit equals risk        | $100 profit      |
| 2R     | Double the risk           | $200 profit      |
| 3R     | Triple the risk           | $300 profit      |
| 0.5R   | Half the risk (sub-optimal) | $50 profit     |

### Minimum R-Multiple Threshold

Professional traders typically require a minimum 2R reward-to-risk ratio.
A trade offering less than 1R is generally not worth taking unless compensated by
a very high win rate (>66%).

### Partial-Close at R-Levels

Scale out of winners at R-multiple milestones:

| Tranche      | Close At   | Effect                                       |
|--------------|------------|----------------------------------------------|
| 25% position | 1R profit  | Locks in 0.25R, remaining position = "free"  |
| 25% position | 2R profit  | Additional 0.5R locked                       |
| 50% position | 3R+ trail  | Let the winner run with trailing stop         |

After the first partial at 1R, the remaining position is risk-free
(the realized profit covers the original R).

### Grid Context

A grid strategy with TP at 0.8% above average entry and SL at 2% has an
effective R:R of 0.4:1 — this is a sub-1R trade. It relies on high win rate.
R-multiple thinking helps evaluate whether this trade structure makes sense
given historical win rates. The **expectancy** must be positive:

```
Expectancy = (WinRate × AvgR-multiple) - (LossRate × 1R)
```

---

## Portfolio-Level Risk

### Portfolio Heat

Total R-exposure across all simultaneous open positions:

```
PortfolioHeat = sum of R for each open position
```

Professional rule: portfolio heat should not exceed 5–6% of equity.
At 1% risk per trade, this allows 5–6 simultaneous positions.

### Why It Matters

Correlated positions (e.g., multiple long crypto perps in a bull market)
can all fail simultaneously. Portfolio heat limits prevent catastrophic
correlated drawdowns.

### Configuration

New field in `RiskLimitsConfig`:

```
MaxPortfolioHeatPercent = 6  (default; 0 = disabled)
```

The RiskEngine blocks new entries when total open R exceeds this limit.

---

## Adaptive Risk (Drawdown-Adjusted)

After sustained losses, reduce risk percentage dynamically:

| Account Drawdown | Risk Adjustment       |
|------------------|-----------------------|
| 0–5%             | Full risk (e.g., 1%)  |
| 5–10%            | 75% risk (0.75%)      |
| 10–15%           | 50% risk (0.5%)       |
| 15%+             | Circuit breaker — halt |

This overlay sits on top of the base R calculation. It compounds with
the natural anti-martingale effect (shrinking equity shrinks R) to provide
additional protection during drawdown periods.

---

## Kelly Criterion (Advisory)

After backtesting, the system can suggest an optimal risk percentage:

```
Kelly% = W - (1 - W) / R_ratio
```

Where W = win probability, R_ratio = average win / average loss.

Most practitioners use "half-Kelly" (Kelly% / 2) to reduce variance.

This is an informational metric displayed in backtest results, not an
automatic configuration. The trader decides whether to adopt the suggestion.

---

## Volatility-Scaled Risk

Instead of a fixed stop-loss percentage, use ATR for stop-loss distance:

```
stopLossDistance = ATR(period) × multiplier
positionSize = R / stopLossDistance
```

This naturally sizes smaller in volatile markets and larger in calm markets.
The system already supports ATR trailing stops — extending ATR to initial
stop-loss distance and sizing is the natural next step.

---

## R-Multiple Trade Tracking

Every closed trade should record:

| Field              | Description                                    |
|--------------------|------------------------------------------------|
| `InitialR`         | Dollar risk at trade entry                     |
| `RMultipleResult`  | (PnL / InitialR) — the realized R-multiple     |
| `MaxFavourable`    | Maximum favourable excursion in R              |
| `MaxAdverse`       | Maximum adverse excursion in R                 |

Aggregate metrics:

| Metric       | Formula                                          |
|--------------|--------------------------------------------------|
| Expectancy   | `mean(RMultipleResult)` across all trades        |
| Win Rate     | Trades with RMultiple > 0 / total trades         |
| Avg Winner   | Mean R-multiple of winning trades                |
| Avg Loser    | Mean R-multiple of losing trades (should be ≈-1) |
| Profit Factor| Sum of positive R / abs(sum of negative R)       |
| System Quality Number | `(Expectancy / StdDev(R-multiples)) × sqrt(N)` |

---

## Strategy-Agnostic Architecture

### Design Principle

**R-based sizing is a cross-cutting concern, not a strategy-specific calculation.**

The user configures risk settings once in `RiskConfig`. Any strategy type
(grid, signal, or future strategies) that emits entry signals gets correctly
sized positions. The risk/sizing logic lives in a shared resolver — controllers
only need to supply the current stop-loss distance.

### User-Provided Inputs

When the user selects `RiskBased` mode, they provide:

| Field | Description | Example |
|-------|-------------|---------|
| `riskPerTradePercent` | "I risk X% of my account per trade" | 1.0 |
| `autoLeverage` | Derive leverage from R and SL distance | true |
| Stop-loss config | Any supported SL type in `ExitConfig` | fixed 2%, ATR×3, etc. |

Everything else is derived at runtime:

```
R = equity × riskPerTradePercent
SL distance = resolved from ExitConfig at trade time
positionNotional = R / (SL distance / entryPrice)
leverage = 1 / (SL% + maintenanceMarginBuffer)   [if autoLeverage]
```

In `PercentWallet` or `FixedNotional` mode, the existing fields still work
as before — R is implicit and the user controls notional directly.

### How Controllers Supply SL Distance

Each controller resolves the effective stop-loss distance from `ExitConfig`
at the point of signal emission. This is necessary because the SL distance
depends on context:

| Exit Type | SL Distance Resolution |
|-----------|------------------------|
| `FixedPercent` | `config.Exit.StopLoss.Value / 100` — constant |
| `AtrTrailing` | `(ATR × multiplier) / entryPrice` — varies per candle |
| Grid breakdown | `config.Grid.BreakdownThreshold` — grid-specific |

The controller passes this resolved distance to `PositionSizeResolver`:

```csharp
// Both GridController and SignalController call this identically:
var stopLossPercent = ResolveStopLossPercent(config.Exit, context);
var notional = PositionSizeResolver.ResolveNotional(
    config.Risk, context.AccountEquity, stopLossPercent);
```

This keeps the sizing math in one place. Adding a new strategy type in
the future only requires it to resolve its SL distance and call the resolver.

### `PositionSizeResolver` — Proposed Signature

```csharp
public static decimal ResolveNotional(
    RiskConfig risk,
    decimal accountEquity,
    decimal? stopLossPercent = null)
{
    return risk.PositionSizeType switch
    {
        PositionSizeType.PercentWallet  => equity × (risk.PositionSizeValue / 100),
        PositionSizeType.FixedNotional  => risk.PositionSizeValue,
        PositionSizeType.RiskBased      => CalculateRiskBased(risk, equity, stopLossPercent),
        _ => risk.PositionSizeValue
    };
}

private static decimal CalculateRiskBased(
    RiskConfig risk, decimal equity, decimal? stopLossPercent)
{
    // R = equity × riskPerTradePercent
    var r = equity * (risk.RiskPerTradePercent / 100);
    // positionNotional = R / SL%
    return r / (stopLossPercent!.Value / 100);
}
```

The `stopLossPercent` parameter is only required when mode = `RiskBased`.
Existing callers using `PercentWallet`/`FixedNotional` pass null and are
unaffected.

### Auto-Leverage Calculation

When `autoLeverage = true`, a separate utility derives the leverage to use:

```csharp
public static int CalculateLeverage(
    decimal stopLossPercent,
    decimal maintenanceMarginRate,
    int maxLeverage)
{
    var raw = 1m / (stopLossPercent / 100m + maintenanceMarginRate);
    return Math.Clamp((int)Math.Floor(raw), 1, maxLeverage);
}
```

The `maintenanceMarginRate` comes from the asset's margin tier on Hyperliquid
(e.g., 1% for BTC at 50x max, 2.5% for 20x alts). `maxLeverage` is the
exchange-imposed cap for the asset.

When `autoLeverage = false`, the user's manual `leverage` value is used
as before.

### RiskConfig — Updated Fields

```csharp
public sealed record RiskConfig
{
    // Existing fields
    public PositionSizeType PositionSizeType { get; init; }
    public decimal PositionSizeValue { get; init; }
    public decimal Leverage { get; init; } = 1m;
    public int MaxOpenTrades { get; init; } = 1;
    public int CooldownValue { get; init; }
    public CooldownUnit CooldownUnit { get; init; }
    public bool AllowSameCandleReentry { get; init; }

    // New fields for RiskBased mode
    public decimal? RiskPerTradePercent { get; init; }  // e.g., 1.0 = risk 1% per trade; null/0 for non-RiskBased modes
    public bool AutoLeverage { get; init; }             // derive leverage from SL distance
}
```

When `PositionSizeType = RiskBased`:
- `RiskPerTradePercent` is required (validated > 0)
- `PositionSizeValue` is ignored
- `Leverage` is ignored if `AutoLeverage = true` (derived from SL + margin)
- `Leverage` is used as-is if `AutoLeverage = false`

When `PositionSizeType = PercentWallet` or `FixedNotional`:
- `RiskPerTradePercent` and `AutoLeverage` are ignored
- Existing behaviour is unchanged

---

## Optimizer Integration

### Sweeping Risk Parameters

The optimizer can sweep `riskPerTradePercent` as a variable alongside
stop-loss parameters. Since R is a function of both risk% and SL distance,
sweeping both dimensions tests different risk profiles:

```
riskPerTradePercent: [0.5, 1.0, 1.5, 2.0]
stopLoss:            [1%, 2%, 3%, 5%]
```

Each combination produces a different position size, leverage, and risk
profile. The fitness function already evaluates total PnL, max drawdown,
and win rate — so it naturally selects the risk% that produces the best
risk-adjusted returns.

### ParameterBounds — New Fields

```csharp
// Existing
public decimal[] PositionSizeOptions { get; init; } = [10m, 15m, 20m];

// Selects which sizing strategy the optimizer generates configs for
public PositionSizeMode PositionSizeMode { get; init; } = PositionSizeMode.PercentWallet;

// Risk-based sizing options (used when PositionSizeMode = RiskBased)
public decimal[] RiskPerTradePercentOptions { get; init; } = [0.25m, 0.5m, 1.0m, 1.5m, 2.0m, 3.0m];
public bool IncludeAutoLeverage { get; init; } = true;
```

### StrategyConfigGenerator Changes

When `PositionSizeMode = RiskBased`, the generator always produces `RiskBased` configs,
picking from `RiskPerTradePercentOptions` and setting `AutoLeverage` stochastically
based on `IncludeAutoLeverage`. When `PositionSizeMode = PercentWallet` (the default),
it picks from `PositionSizeOptions` as before.

The leverage sweep (`LeverageMin`/`LeverageMax`) is only applied when
`AutoLeverage = false` — otherwise leverage is derived from SL distance.

### What the Optimizer Discovers

By sweeping risk% alongside SL and TP parameters, the optimizer can find:

- **Optimal R per trade** — how much risk maximizes risk-adjusted returns
- **Optimal SL distance** — tighter SL = larger position (same R), wider = smaller position
- **Natural leverage selection** — auto-leverage means the optimizer doesn't need
  to sweep leverage as an independent variable when using `RiskBased` mode
- **Kelly-optimal risk%** — post-optimization, compare the best risk% against
  the Kelly criterion suggestion from the backtest metrics

---

## Current State & Gaps

### What Exists

| Component | Status |
|-----------|--------|
| `PositionSizeType.PercentWallet` | Working |
| `PositionSizeType.FixedNotional` | Working |
| `PositionSizeType.RiskBased` | Working — R-based sizing with SL distance resolution |
| `PositionSizeResolver` | Working (3 modes) |
| `StopLossDistanceResolver` | Working — resolves FixedPercent, AtrTrailing, grid breakdown fallback |
| `GridController` RiskBased branch | Working — resolves SL%, computes total notional, divides by grid levels |
| `SignalController` RiskBased branch | Working — resolves SL%, passes to resolver |
| `BusinessRuleValidator` RiskBased | Working — validates RiskPerTradePercent range, high-risk warning |
| `CrossFieldValidator` RiskBased | Working — requires SL when RiskBased; grid breakdown fallback accepted |
| `LiveRiskEngine` circuit breaker | Working (but loss recording not wired) |
| `LiveRiskEngine` max order size | Working — checks `notionalUsd` key; GridController, SignalController, LivePositionManager and BacktestPositionManager all use `notionalUsd` |
| `RiskConfig.Leverage` | Stored but never applied to sizing math |
| `SetLeverage` agent command | Stub (not implemented) |
| ATR trailing stop | Working for exits |
| ATR initial stop (volatility-scaled entry lock) | Working — `ExitRuleType.AtrInitial` in `ExitRuleConfig`; ATR captured at entry via `GridState.AtrAtEntry`; fixed stop distance for lifecycle; `TriggerOrderManager` skips SL updates when locked |
| `maxOpenTrades` | Working |
| Optimizer `RiskBased` sweep | Working — `PositionSizeMode.RiskBased` in `ParameterBounds`; sweeps `RiskPerTradePercentOptions`, optional `IncludeAutoLeverage`; `StrategyConfigGenerator` branches on mode; `RunOptimizationRequest` accepts `positionSizeMode`, `riskPerTradePercentOptions`, `includeAutoLeverage` |
| `PortfolioHeatCalculator` | Working — computes total R across positions via `CalculateFromPositions()` and `CalculateFromTrackedRisks()` |
| `LiveRiskEngine` portfolio heat check | Working — blocks entry signals when `currentHeat + estimatedRiskUsd > equity × maxHeatPercent / 100` |
| `BacktestRiskEngine` heat enforcement | Working — enforces heat limits during replay with per-run `HeatBlockedSignalCount` reporting |
| `DeployGrid` signal `estimatedRiskUsd` | Working — `GridController` adds `estimatedRiskUsd` to signal parameters for risk tracking |
| `IRiskEngine.UpdatePortfolioState` | Working — called by `StrategyScheduler` each candle close to track account equity |
| `IRiskEngine.RecordPositionClosed` | Working — called by `FillProcessor` when positions close to clear tracked risk |
| `MaxPortfolioHeatPercent` config | Working — field in `RiskLimitsConfig` (default 6); 0 disables heat checks |
| API `GET /api/risk/portfolio-heat` | Working — `RiskController` returns per-position heat breakdown and aggregate utilization |

### What's Missing

| Capability | Priority |
|------------|----------|
| ~~`RiskBased` sizing mode (R calculation)~~ | ~~P1~~ Done |
| ~~Strategy-agnostic SL distance resolution (both controllers)~~ | ~~P1~~ Done |
| Auto-leverage from R and SL distance | P1 |
| `SetLeverage` exchange integration before first order | P1 |
| Isolated margin mode enforcement | P1 |
| ~~`notionalUsd`/`notionalPerLevel` key mismatch fix~~ | ~~P1 (bug)~~ Done |
| `RecordLoss` wiring in LivePositionManager | P1 (bug) |
| ~~Optimizer sweep of `riskPerTradePercent`~~ | ~~P1~~ Done |
| ~~Portfolio heat enforcement~~ | ~~P2~~ Done |
| R-multiple exit types | P2 |
| R-multiple trade tracking & expectancy | P2 |
| Partial-close at R-levels | P3 |
| Drawdown-adjusted risk | P3 |
| ~~Volatility-scaled initial SL~~ | ~~P3~~ Done |
| Kelly criterion suggestion in backtest results | P3 |
| Periodic live equity refresh | P3 |

---

## Affected Components

Implementation touches every layer of the pipeline:

| Component | File(s) | Change |
|-----------|---------|--------|
| PositionSizeType enum | `PositionSizeType.cs` | Add `RiskBased` |
| RiskConfig | `RiskConfig.cs` | Add `RiskPerTradePercent`, `AutoLeverage` |
| PositionSizeResolver | `PositionSizeResolver.cs` | Add RiskBased branch with SL distance param; keep backward compat for existing modes |
| GridController | `GridController.cs` | Resolve SL% from ExitConfig, pass to resolver; emit auto-leverage in signal params |
| SignalController | `SignalController.cs` | Same: resolve SL%, pass to resolver; emit auto-leverage in signal params |
| LiveRiskEngine | `LiveRiskEngine.cs` | ~~Fix `notionalUsd`/`notionalPerLevel` key mismatch~~ Done; add portfolio heat check; ensure `RecordLoss` called |
| RiskLimitsConfig | `RiskLimitsConfig.cs` | Add `MaxPortfolioHeatPercent` |
| LivePositionManager | `LivePositionManager.cs` | Call `SetLeverage` before first order; call `RecordLoss` on trade close |
| BacktestPositionManager | `BacktestPositionManager.cs` | Same sizing changes; respect leverage in simulated margin |
| TriggerOrderManager | `TriggerOrderManager.cs` | Support R-multiple TP mode |
| AgentCheckInService | `AgentCheckInService.cs` | Implement `SetLeverage` |
| StrategyScheduler | `StrategyScheduler.cs` | Consider periodic equity refresh for live |
| SimulatedExecutionEngine | `SimulatedExecutionEngine.cs` | Track margin, leverage, liquidation |
| ExitConfig / ExitRuleConfig | `ExitConfig.cs`, `ExitRuleConfig.cs` | Add R-multiple exit type |
| ParameterBounds | `ParameterBounds.cs` | Added `PositionSizeMode` enum, `RiskPerTradePercentOptions`, `IncludeAutoLeverage` (`IncludeRiskBasedSizing` was not implemented) |
| StrategyConfigGenerator | `StrategyConfigGenerator.cs` | Generate `RiskBased` configs; skip leverage sweep when auto-leverage |
| Strategy config schema | `13-strategy-config-schema.md` | Document `riskPerTradePercent`, `autoLeverage`, `risk_based` enum value |
| Frontend risk card | `risk-management-card.component.*` | RiskBased mode, riskPerTradePercent input, auto-leverage toggle, calculated R preview |
| Trade results / performance | Domain entities | Record R-multiple per trade |
| Backtest summary | Backtest DTOs | R-multiple distribution, expectancy, Kelly suggestion |
