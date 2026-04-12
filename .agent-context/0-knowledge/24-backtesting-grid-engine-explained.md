# Backtesting Grid Engine — Explained

This document provides a plain-English summary, a detailed walkthrough, pseudo-code for
the grid engine, and manual testing steps so that the algorithm can be understood and
verified by a human.

---

## 1. Summary (TL;DR)

The backtest replays historical 15-minute candles through the **exact same** strategy
pipeline used in live trading. On every candle close it asks: "Is there a setup?" and
"Do I have an open position?". If a setup exists and no grid is active, it deploys a
grid of limit-buy orders below the current price. If the first levels start filling,
the remaining ladder stays active so deeper pullbacks can improve average entry. The
controller only transitions to closing when stop loss triggers, when a partially-filled
grid reaches candle-close take profit, or when a fully-filled grid places its normal
take-profit order. A `SimulatedExecutionEngine` fills those orders when historical
price reaches the order levels. Fees and slippage are deducted at fill time. At the end
of the replay, metrics (PnL, win rate, drawdown, etc.) are calculated from the trade
log and equity curve.

**Key guarantee:** Because `GridController`, `StrategyScheduler`, `RiskEngine`, and
`PositionManager` are the same classes used in live mode, a passing backtest proves the
live pipeline will behave identically given the same candle data.

---

## 2. Component Glossary

| Component | Role |
|-----------|------|
| `BacktestRunner` | Top-level orchestrator — loads data, runs the loop, returns results |
| `CandleReplayEngine` | Loads 15m/1h/4h candles from the DB, aligns warmup boundary |
| `CandleClock` + `StrategyScheduler` | Fires a strategy evaluation on every 15m candle close (same as live) |
| `GridStrategyEngine` (`IStrategyEngine`) | Decides whether a "setup" exists (currently: always true when config is valid and HTF candles are available) |
| `GridController` (`IGridController`) | The brain — emits `DeployGrid` or `TakeProfit` / `StopLoss` signals based on grid lifecycle |
| `RiskEngine` (`IRiskEngine`) | Validates signals before they execute (passthrough in current backtest) |
| `BacktestPositionManager` (`IPositionManager`) | Translates signals into simulated orders |
| `SimulatedExecutionEngine` | Fills orders against candle OHLC, tracks position, calculates PnL |
| `BacktestMetricsCalculator` | Post-run: computes TotalPnL, Win Rate, Max Drawdown, etc. |

---

## 3. Detailed Walkthrough — What Happens Step by Step

### Phase 1: Validation
`BacktestRunner.ValidateConfig` checks: symbol is set, start < end, capital > 0,
intervals include 15m + 1h + 4h, strategy config JSON is parseable, warmup ≥ 0.

### Phase 2: Data Load
`CandleReplayEngine.LoadAsync`:
1. Calculates a warmup start time: `startDate - (warmupPeriod × 15 minutes)`.
2. Loads 15m, 1h, 4h candles from the database **in parallel**.
3. Higher-timeframe queries start one extra interval earlier to ensure alignment.
4. Deduplicates and sorts each set by timestamp.
5. Finds the `warmupEndIndex` — the first 15m candle at or after `StartDateUtc`.
6. Validates sufficient data exists for warmup and higher-timeframe context.

### Phase 3: Warmup (Indicator Seeding)
For each 15m candle **before** `warmupEndIndex`, call
`MarketContextBuilder.UpdateIndicators(candle)` to seed EMA, RSI, etc.
No signals are generated during warmup.

### Phase 4: Main Evaluation Loop
For each 15m candle **from** `warmupEndIndex` to end:

```
1. SimulatedExecutionEngine.ProcessCandle(candle)
   → Checks every open order against this candle's OHLC
   → Buys processed before sells (prevents same-candle pairing)
   → Limit buy fills if candle.Low ≤ order.Price
   → Limit sell fills if candle.High ≥ order.Price
   → Market orders fill at candle.Close
   → Each fill: deduct fees, update position (size, avg entry, realised PnL)
   → Record fills into trade log

2. MarketContextBuilder.UpdateIndicators(candle)
   → Updates rolling indicators with new candle data

3. Resolve latest closed 1h and 4h candles
   → GetLatestClosedCandle: finds the most recent HTF candle whose
     (timestamp + intervalMs) ≤ current 15m candle timestamp
   → Prevents look-ahead bias

4. Update scheduler with current GridState + PositionState
   → PositionState comes from SimulatedExecutionEngine.GetPosition()

5. CandleClock.ProcessCandleAsync(candle)
   → Fires CandleClosed event
   → StrategyScheduler.HandleCandleClosedAsync runs:

     a. MarketContextBuilder.Build(triggerCandle, 1hCandle, 4hCandle)
        → Produces MarketContext with indicator snapshot

     b. StrategyEngine.EvaluateAsync(context, configJson)
        → Returns SetupDetected = true/false

     c. GridController.ProcessAsync(evaluation, context, gridState, positionState, configJson)
        → THE CORE DECISION LOGIC (see pseudo-code below)
        → Returns 0 or more TradingSignals

     d. RiskEngine.ValidateAsync(signals)
        → Filters out any signals that violate risk rules

     e. PositionManager.ExecuteSignalsAsync(approvedSignals)
        → Translates signals into simulated orders

6. Check if a grid cycle completed (lifecycle → Closed)
7. Record equity snapshot: initialCapital + realisedPnL + unrealisedPnL
```

### Phase 5: Metrics
`BacktestMetricsCalculator.Calculate(tradeLog, equityCurve, initialCapital, gridCycles)`:
- Pairs entries with exits (FIFO within compatible types)
- Counts wins/losses, computes averages
- Walks equity curve for max drawdown (peak-to-trough)

---

## 4. Pseudo-Code — GridController Decision Logic

This is the actual decision tree implemented in `GridController.ProcessAsync`:

```
FUNCTION ProcessCandle(evaluation, context, gridState, position, config):

    # ── BRANCH A: Position is open ──
    IF position.IsOpen:
        stopPrice  = position.AvgEntry × (1 − config.StopLoss%)
        tpPrice    = position.AvgEntry × (1 + config.TakeProfit%)

        IF config.StopLoss > 0 AND candle.Close ≤ stopPrice:
            gridState.Lifecycle = Closing
      EMIT exit(type=Market, reason=StopLoss, size=position.Size)
      RETURN

    IF gridState.Lifecycle == Closing:
      RETURN

    IF gridState.Lifecycle == PartiallyFilled:
      IF candle.Close ≥ tpPrice:
        gridState.Lifecycle = Closing
        EMIT exit(type=Market, reason=TakeProfit, size=position.Size)
      RETURN

    # Fully-filled / fallback open-position path
            gridState.Lifecycle = Closing
      EMIT exit(type=Limit, reason=TakeProfit, price=tpPrice, size=position.Size)
        RETURN

    # ── BRANCH B: No position, no setup detected ──
    IF NOT evaluation.SetupDetected:
        RETURN (no signals)

    # ── BRANCH C: Grid already active ──
    IF gridState.Lifecycle NOT IN (Inactive, Closed):
        RETURN (no signals)  # wait for current cycle to finish

    # ── BRANCH D: Deploy new grid ──
    gridState.GridCycleId = new GUID
    gridState.Lifecycle   = Deploying
    gridState.TotalLevels = config.GridLevels
    gridState.FilledLevels = 0

    EMIT DeployGrid(
        anchorPrice    = candle.Close,
        gridLevels     = config.GridLevels,
        gridSpacingPct = config.GridSpacing,
        notionalUsd    = config.PositionSize
    )
```

---

## 5. Pseudo-Code — Grid Deployment (PositionManager)

When `DeployGrid` signal is received:

```
FUNCTION DeployGrid(signal):
    CANCEL all open orders for symbol

    FOR level = 1 TO signal.gridLevels:
        price = anchorPrice × (1 − (gridSpacing% / 100) × level)
        size  = notionalUsd / price

        PLACE limit buy order at (price, size, type=GridFill)
```

**Example** with anchorPrice=$100,000, gridLevels=10, gridSpacing=0.5%, positionSize=$100:

Formula per level: `price = anchorPrice × (1 − (gridSpacing% / 100) × level)`, `size = positionSize / price`

| Level | Price | Offset from Anchor | Size (BTC) | Notional |
|-------|-------|--------------------|------------|----------|
| 1 | $99,500.00 | −0.50% | 0.00100503 | $100.00 |
| 2 | $99,000.00 | −1.00% | 0.00101010 | $100.00 |
| 3 | $98,500.00 | −1.50% | 0.00101523 | $100.00 |
| 4 | $98,000.00 | −2.00% | 0.00102041 | $100.00 |
| 5 | $97,500.00 | −2.50% | 0.00102564 | $100.00 |
| 6 | $97,000.00 | −3.00% | 0.00103093 | $100.00 |
| 7 | $96,500.00 | −3.50% | 0.00103627 | $100.00 |
| 8 | $96,000.00 | −4.00% | 0.00104167 | $100.00 |
| 9 | $95,500.00 | −4.50% | 0.00104712 | $100.00 |
| 10 | $95,000.00 | −5.00% | 0.00105263 | $100.00 |

**Total grid exposure if all 10 levels fill:** ~0.01028503 BTC ($1,000 notional across 10 orders).

---

## 6. How the Buy/Sell Mechanism Actually Works

> **One sentence answer:** The grid places multiple buy orders, keeps the remaining
> ladder active after partial fills, and only places the single closing sell when an
> explicit exit condition is reached.

---

### 6.1 The Rules

| Rule | Detail |
|------|--------|
| **Buys** | One limit buy per ladder level, each at a progressively lower price |
| **Partial fills** | Remaining buy levels stay open after the first fill so the average entry can keep improving |
| **Partial-fill TP** | Checked by the controller on candle close using the current weighted-average entry |
| **Fully-filled TP** | Once all levels fill, the controller places one limit sell for the whole position |
| **Stop loss** | Checked from any open-position state and closes the whole position with a market order |
| **Closing sell** | Always exactly **1** sell order covering the full accumulated size |
| **Cancellation timing** | Unfilled buy levels are cancelled only when TP or SL actually transitions the cycle into `Closing` |

---

### 6.2 Worked Example A — Ladder Stays Open After the First Fill

**Config:** anchor=$100,000, gridLevels=5, gridSpacing=0.5%, positionSize=$100, TP=1%, SL=5%

```
CANDLE 1 — Close: $100,000
  Grid deploys five buys at: $99,500 / $99,000 / $98,500 / $98,000 / $97,500

CANDLE 2 — Low: $99,400, Close: $99,600
  Buy 1 fills at $99,500
  Position size opens, avg entry = $99,500
  GridState = PartiallyFilled
  Candle close ($99,600) is still below TP ($100,495)
  Result: Buys 2-5 stay open. No sell is placed yet.

CANDLE 3 — Low: $98,400, Close: $98,600
  Buy 2 fills at $99,000
  Buy 3 fills at $98,500
  Avg entry improves to about $98,999
  Candle close ($98,600) is still below TP (~$99,989)
  Result: Buys 4-5 still remain active.

CANDLE 4 — Close: $100,200
  Candle close is now above the partial-fill TP trigger
  GridController transitions to Closing
  PositionManager cancels the two remaining buy orders
  PositionManager places ONE market sell for the full accumulated position

CANDLE 5 — Market sell fills at candle close
  Position closes, lifecycle moves to Closed
```

**Result:** three grid levels participate in the cycle, the average entry improves, and
the unfilled ladder is only cancelled at the moment take profit is actually triggered.

---

### 6.3 Worked Example B — Fully Filled Grid Uses the Normal Limit TP Flow

```
CANDLE 1 — Grid deploys.

CANDLE 2 — Price trades down through every ladder level.
  All buy levels fill on the same candle.
  GridState = FullyFilled.

Same candle close:
  GridController sees a fully filled position.
  It transitions to Closing and emits one limit take-profit order for the full size.

Later candle:
  High reaches the limit TP price.
  The sell fills as maker liquidity and the cycle closes.
```

**Result:** a fully filled grid still exits with one sell order, but unlike a partial
fill it uses the standing limit take-profit order immediately.

---

### 6.4 Worked Example C — Stop Loss From a Partial Fill

```
CANDLE 1 — Grid deploys.

CANDLE 2 — Buy 1 fills at $99,500.
  GridState = PartiallyFilled.

CANDLE 3 — Close drops below the stop trigger while the next ladder level is still unfilled.
  GridController transitions to Closing.
  PositionManager cancels the remaining buy orders.
  PositionManager places ONE market sell for the open size.

CANDLE 4 — Market sell fills at candle close.
  Position closes at a loss and lifecycle becomes Closed.
```

**Result:** stop loss is available from the partially-filled state, and the audit log can
distinguish this path from partial-fill take profit even though both use market exits.

---

### 6.5 Visual Summary — Partial Fill Lifecycle

```
Price
  ▲
  │  $100,000 ── Grid deploys ── ①
  │                  ╲
  │   $99,500 ─ ─ ─ ─ Buy 1 fills ── ②
  │                  ╱            remaining buys stay open
  │   $99,000 ─ ─ ─ ─ Buy 2 fills ── ③
  │   $98,500 ─ ─ ─ ─ Buy 3 fills ── ④
  │                                      candle close reaches TP
  │  $100,000 ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ⑤ cancel remaining buys + place one sell
  │                                      next candle fills exit
  └──────────────────────────────────────────────────────► Time

  ① Deploy: ladder orders are placed below anchor
  ② Partial fill: first level opens the position
  ③-④ Accumulate: deeper levels can still fill on later candles
  ⑤ Close: TP/SL cancels residual ladder orders and exits the whole position
```

---

### 6.6 Why Most Trades Show Exactly $2.00 Profit

With positionSize=$100 and TP=2%:

```
  Profit = positionSize × takeProfitPercent / 100
         = $100 × 2 / 100
         = $2.00
```

This is approximately true when only 1 level fills (the most common case), because:
- The buy is ~$100 of notional value
- The sell is at +2% from entry
- So profit ≈ $2.00 minus fees (~$0.02)

When 3 levels fill, profit ≈ $6.00. When 5 levels fill, profit ≈ $10.00. And so on.

Negative PnL trades (-$5.44, -$10.33 etc.) are **stop losses** where price dropped
5%+ from entry and a market sell was triggered.

---

## 7. Pseudo-Code — Order Fill Logic (SimulatedExecutionEngine)

```
FUNCTION ProcessCandle(candle):
    sort open orders: buys first, then sells

    FOR EACH order:
        IF order is Limit Buy AND candle.Low ≤ order.Price:
            FILL at order.Price (maker fee)
        ELIF order is Limit Sell AND candle.High ≥ order.Price:
            FILL at order.Price (maker fee)
        ELIF order is Market:
            FILL at candle.Close (taker fee)

    FOR EACH fill:
        Apply slippage to fill price
        Calculate fee = size × fillPrice × feeRate
        Update position:
            IF buying into long: weighted-average entry
            IF selling out of long: realise PnL = (fillPrice − avgEntry) × size
            Deduct fee from realisedPnL

    Update unrealised PnL = (candle.Close − avgEntry) × remainingSize
```

---

## 8. Pseudo-Code — Trade Pairing (FIFO)

See [Section 6](#6-how-the-buysell-mechanism-actually-works--10-buys-1-sell) for a detailed
explanation of how trade pairing works in practice, including the one-to-one pairing
limitation when multiple grid levels fill.

```
FUNCTION RecordFill(tradeLog, fill):
    IF fill is GridFill or HedgeOpen:
        ADD new open trade entry to log
    ELIF fill is TakeProfit:
        FIND first open trade where type=GridFill
        PAIR: set exitPrice, exitTime, calculate PnL
    ELIF fill is HedgeClose:
        FIND first open trade where type=HedgeOpen
        PAIR: set exitPrice, exitTime, calculate PnL
```

---

## 9. Grid Lifecycle State Machine

```
    ┌──────────┐
    │ Inactive │ ─── setup detected ──→ ┌───────────┐
    └──────────┘                        │ Deploying │
         ↑                              └─────┬─────┘
         │                                    │
    grid cycle                          orders placed
    completes                                 │
         │                              ┌─────▼──────────┐
    ┌────┴───┐   all levels filled     │ PartiallyFilled │
    │ Closed │ ←── (or TP/SL hit) ←── └─────┬───────────┘
    └────────┘                               │
         ↑                              all levels fill
         │                                   │
    ┌────┴────┐                        ┌─────▼───────┐
    │ Closing │ ←── TP/SL emitted ─── │ FullyFilled │
    └─────────┘                        └─────────────┘
```

State transitions in code:
- **Inactive/Closed → Deploying**: `GridController` when SetupDetected and no active grid
- **Deploying → PartiallyFilled**: `RecordFill` when first GridFill arrives
- **PartiallyFilled → FullyFilled**: `RecordFill` when filledLevels == totalLevels
- **Any with open position → Closing**: `GridController` emits TakeProfit or StopLoss
- **Closing → Closed**: `RecordFill` when TakeProfit fill arrives

---

## 10. How to Verify the Algorithm Is Correct — Manual Testing Steps

### Test 1: Single Grid Cycle (Happy Path)
**Goal:** One grid deploys, fills partially, takes profit.
1. Configure: gridLevels=3, gridSpacing=1%, takeProfit=2%, stopLoss=5%, positionSize=$100
2. Use a short date range where price dips 2-3% then recovers.
3. **Expected:**
   - 1 DeployGrid signal with 3 buy levels at -1%, -2%, -3% from anchor.
   - Some (1-3) grid levels fill when candle lows reach those prices.
   - TakeProfit order placed at avgEntry × 1.02.
   - TP fills when candle high reaches target.
   - Trade log shows entry/exit paired, PnL is positive (price recovered past TP).
   - Grid lifecycle: Inactive → Deploying → PartiallyFilled → Closing → Closed.

### Test 2: Stop Loss Hit
**Goal:** Grid fills, price continues down, stop loss triggers.
1. Use a date range with a sustained downtrend.
2. **Expected:**
   - Grid fills at limit prices.
   - Candle closes below `avgEntry × (1 − stopLoss%)`.
   - Market sell order emitted (not limit).
   - Trade log shows negative PnL.
   - Verify the loss amount: `(exitPrice − avgEntry) × size − fees`.

### Test 3: No Setup → No Orders
**Goal:** Verify the engine doesn't deploy when higher-timeframe candles are missing.
1. Remove 1h or 4h data from DB.
2. **Expected:** `StrategyEngine.EvaluateAsync` returns `SetupDetected = false`.
3. No signals, no trades, equity stays flat at initial capital.

### Test 4: Grid Spacing Accuracy
**Goal:** Verify order prices are mathematically correct.
1. Set anchor price = $100,000, gridLevels=5, gridSpacing=0.5%.
2. **Expected order prices:**
   - Level 1: $100,000 × 0.995 = $99,500
   - Level 2: $100,000 × 0.990 = $99,000
   - Level 3: $100,000 × 0.985 = $98,500
   - Level 4: $100,000 × 0.980 = $98,000
   - Level 5: $100,000 × 0.975 = $97,500
3. Inspect the open orders on `SimulatedExecutionEngine` after deployment.

### Test 5: Fee Accounting
**Goal:** Verify fees are correctly deducted.
1. Run a single-cycle backtest with known prices.
2. Manually calculate expected fees: `size × fillPrice × makerFeeRate` per fill.
3. **Expected:** `BacktestResult.TotalFeesPaid` matches your manual calculation.
4. Check that `TotalPnL` is *after* fees (fees are already in RealisedPnL).

### Test 6: No Look-Ahead Bias
**Goal:** Verify HTF candles don't leak future data.
1. At a 15m candle with timestamp `T`, the latest 1h candle must have
   `closeTime = (timestamp + 3600000) ≤ T`.
2. Log the 1h candle used at each step and verify manually.

### Test 7: Multiple Grid Cycles
**Goal:** Verify the engine correctly resets and deploys a new grid after completion.
1. Use a long date range with multiple price dips and recoveries.
2. **Expected:**
   - `BacktestResult.GridCycles > 1`.
   - Each cycle has its own `GridCycleId`.
   - Lifecycle resets: Closed → Inactive → Deploying → ...

### Test 8: Equity Curve Sanity
**Goal:** Verify equity tracking is consistent.
1. First snapshot should be close to `InitialCapital`.
2. Final snapshot should equal `InitialCapital + TotalPnL + UnrealisedPnL`.
3. No negative equity (unless leverage allows it).
4. Drawdown reported matches the worst peak-to-trough in the equity series.

### Test 9: Edge Case — Price Gaps
**Goal:** A candle gaps through multiple grid levels at once.
1. If a candle has low=$97,000 and grid levels are at $99,500 down to $97,500:
   all levels above the low should fill.
2. **Expected:** Multiple fills on the same candle, all at their limit prices (not gap price).

### Test 10: Edge Case — Zero Trades
**Goal:** Price never reaches any grid level.
1. Use a date range where price only goes up.
2. **Expected:** Grid deployed but never fills. TotalTrades=0, TotalPnL=0, equity flat.

### Test 11: Determinism
**Goal:** Same inputs produce identical outputs.
1. Run the same backtest config twice.
2. **Expected:** Byte-identical `BacktestResult` — same PnL, same trade count, same equity curve.

---

## 11. Known Simplifications in v1

| Simplification | Impact | Future Plan |
|---|---|---|
| `GridStrategyEngine` always returns `SetupDetected = true` when HTF context exists | Every 15m candle can trigger a new grid (if no position open) | Add indicator-based filters (EMA cross, RSI, etc.) |
| No partial take-profit — entire position exits at once | Misses scaling-out opportunities | Add tiered TP levels |
| No hedge logic implemented | Breakdown scenarios not protected | Add short-hedge signals |
| Fill at exact limit price (no candle-walk simulation) | Slightly optimistic fills on volatile candles | Consider VWAP-based fill estimation |
| Buys processed before sells per candle | Prevents same-candle entry+exit but may miss intra-candle nuance | Acceptable for 15m granularity |
| Slippage rate defaults to 0 | Results may look better than reality | Set non-zero slippage for realistic tests |
| Trade log FIFO pairing is one-to-one | When multiple grid levels fill, only the first entry gets paired with TP in the log; remaining entries show as open. Total `RealisedPnL` on the position is still correct. | Split TP fill across all open entries proportionally |
| Partial-fill TP waits for candle close | Intrabar spikes above TP do not exit unless the candle also closes above the trigger | Add optional resting TP orders for partial fills if intrabar precision becomes important |

---

## 12. How to Gain Confidence the Algorithm Is Right

1. **Unit test each component in isolation** — GridController, SimulatedExecutionEngine,
   BacktestMetricsCalculator each have their own test suites.
2. **Trace a single cycle by hand** — Pick a 2-day window, manually calculate expected grid
   levels, expected fills, expected PnL to 4 decimal places, and compare with backtest output.
3. **Invariant checks:**
   - `FinalEquity = InitialCapital + TotalPnL + UnrealisedPnL`
   - `WinningTrades + LosingTrades ≤ TotalTrades` (some trades might have PnL=0)
   - `TotalFeesPaid > 0` whenever `TotalTrades > 0`
   - Every grid cycle has a unique `GridCycleId`
4. **Determinism** — Run twice, get the same result.
5. **Boundary tests** — Zero trades, single trade, all levels filled, stop loss on first candle.
6. **Compare against a known strategy** — Run on a period where you manually calculated the
   expected outcome and verify the backtest matches.
