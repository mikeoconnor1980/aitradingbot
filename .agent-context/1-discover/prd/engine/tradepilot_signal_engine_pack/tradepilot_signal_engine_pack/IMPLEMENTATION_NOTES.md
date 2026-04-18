# Notes on the included implementations

## candle_pattern
Implements starter logic for:
- bullish_engulfing
- bearish_engulfing
- bullish_rejection
- bearish_rejection
- bullish_rejection_or_engulfing
- bearish_rejection_or_engulfing
- bullish_continuation
- bearish_continuation

These are intentionally simple and easy to tune.

## liquidity_sweep
Current definition:
- find recent pivot high/low
- current candle takes that liquidity
- current candle closes back through the pivot

This is a sensible starter definition for stop-hunt style sweeps.

## structure_shift
Current definition:
- find recent pivot high or low
- current close breaks that pivot in the requested direction

Later you may want:
- multi-swing confirmation
- close-through-body rules
- minimum impulse thresholds

## range_state
Current definition:
- low slope over lookback
- enough touches near upper/lower bounds

Later you may want:
- ATR normalization
- regression slope
- standard deviation compression
- breakout exclusion windows

## regime_state
Current definition is heuristic:
- uses trend percent over lookback
- uses average candle range as a crude ATR-style volatility proxy

Later you may want:
- true ATR
- volume regime
- multi-timeframe classification
- trend-strength indicators