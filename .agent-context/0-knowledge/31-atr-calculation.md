# ATR (Average True Range) Calculation

ATR measures per-candle price volatility as a smoothed average. It is the primary
adaptive indicator in both grid and signal strategies, used for trailing stop-loss
placement, exchange-native trigger orders, and regime detection.

## How ATR Works

**True Range** for a single candle is the greatest of three values:

| Calculation | Captures |
|---|---|
| `High − Low` | Normal intra-candle range |
| `\|High − Previous Close\|` | Gap-up from previous candle |
| `\|Low − Previous Close\|` | Gap-down from previous candle |

**ATR** is the smoothed moving average of True Range over *n* periods.

### Seeding (Initial Value)

The first ATR value requires `period + 1` bars:
1. Bar 1 establishes the previous close (no True Range yet)
2. Bars 2 through `period + 1` accumulate True Ranges
3. The **seed** is the simple mean (SMA) of those `period` True Ranges

### Wilder Smoothing (Subsequent Values)

After the seed, each new bar updates ATR using Wilder's exponential smoothing:

```
ATR = ((previous_ATR × (period − 1)) + current_TR) / period
```

This matches TradingView's `ta.atr()` function. Wilder smoothing is equivalent
to an EMA with alpha = 1/period, giving more weight to recent volatility while
decaying older values slowly.

## Key Components

| Component | Path | Purpose |
|---|---|---|
| `IncrementalAtr` | `src/TradingApp.Indicators/Incremental/IncrementalAtr.cs` | O(1) per-bar ATR calculator, used in live and backtest context builders |
| `AtrCalculator` | `src/TradingApp.Indicators/AtrCalculator.cs` | Batch calculator — same Wilder smoothing, used in tests to verify incremental version |
| `LiveMarketContextBuilder` | `src/TradingApp.Application/Trading/Services/LiveMarketContextBuilder.cs` | Feeds `IncrementalAtr(14)` on every candle close, outputs to `IndicatorSnapshot.Atr` |
| `BacktestMarketContextBuilder` | `src/TradingApp.Application/Trading/Services/BacktestMarketContextBuilder.cs` | Same pattern as live — feeds `IncrementalAtr(14)`, identical output |
| `IndicatorSnapshot` | `src/TradingApp.Application/Trading/Models/IndicatorSnapshot.cs` | Carries `Atr` decimal property to strategy evaluation |

## Configuration

| Parameter | Configurable? | Default | Where |
|---|---|---|---|
| ATR period | No — hardcoded | 14 | `new IncrementalAtr(14)` in both context builders |
| ATR multiplier | Yes | 3.0× | `ExitRuleConfig.AtrMultiplier` |
| Trailing stop warmup | Yes | 0 candles | `ExitRuleConfig.TrailingStopWarmup` |
| Exit rule type | Yes | — | `ExitRuleType.AtrTrailing` or `AtrInitial` enum |

The ATR period (14) is fixed across all strategies. The multiplier and warmup
are configurable per-strategy via `StrategyConfig.Exit.StopLoss`.

## How ATR Is Consumed

### 1. App-Side Trailing Stop (GridController / SignalController)

Both controllers share the same logic on every candle close:

1. Track `TrailingStopHighWatermark` = max(previous HWM, current candle high)
2. Skip evaluation during warmup period (`CandlesSinceEntry ≤ TrailingStopWarmup`)
3. Compute trailing stop price: `HWM − (ATR × multiplier)`
4. If `candle.Close ≤ trailingStopPrice` → emit `TakeProfit` signal

For short positions, the logic inverts (low watermark + ATR × multiplier).

### 2. Exchange-Native Trigger Orders (TriggerOrderManager)

When placing the initial exchange-native SL trigger order:

- Uses `CurrentCandle.High` as the reference price (proxy for high watermark at placement time)
- Computes: `triggerPrice = referencePrice − (ATR × multiplier)` (long) or `+ (ATR × multiplier)` (short)
- Trigger orders are updated on every candle close so the SL ratchets with the trailing stop

### 3. Regime Detection (SyntheticRegimeProvider)

ATR feeds into the synthetic regime provider via `_syntheticRegimeProvider.Update(atr)`,
contributing to volatility-based regime classification.

### 4. Locked ATR Stop (AtrInitial)

When `ExitRuleType = AtrInitial`, ATR is captured once at entry time and stored in `GridState.AtrAtEntry`. The stop-loss distance remains fixed for the life of the position:

- **Capture**: At grid deployment or signal entry, `AtrAtEntry = context.Indicators.Atr`
- **Stop price**: `entryPrice − (AtrAtEntry × multiplier)` (long) or `+ (AtrAtEntry × multiplier)` (short)
- **No trailing**: Unlike `AtrTrailing`, the stop does not ratchet with price movement
- **Fallback**: If ATR is null at entry (insufficient data), falls back to `StopLoss.Value` as a fixed percent stop
- **Cleanup**: `AtrAtEntry` is cleared when the grid cycle closes, preventing stale values in subsequent cycles
- **Trigger orders**: `TriggerOrderManager` anchors the exchange-native SL to entry price (not HWM) and skips subsequent SL updates since the stop is locked

## Why ATR Over Fixed Percentage

| Scenario | Fixed 5% SL | ATR × 3 SL |
|---|---|---|
| Low volatility (ATR = 1%) | Wide — wastes profit | Tight at 3% — captures gains |
| High volatility (ATR = 4%) | Tight — stopped by noise | Wide at 12% — survives whipsaws |
| Regime shift | No adaptation | Automatic adjustment |

ATR adapts the stop distance to current market conditions, reducing premature
stop-outs in volatile markets and tightening protection in calm markets.

## Tests

| Test File | Coverage |
|---|---|
| `tests/TradingApp.Indicators.Tests/IncrementalAtrTests.cs` | Matches batch calculator, null for insufficient data, step-by-step verification |
| `tests/TradingApp.Indicators.Tests/AtrCalculatorTests.cs` | Known value checks, edge cases (empty bars, minimal data), series consistency |
