# Backtesting Date Ranges

## Available Data

| Symbol | Source | Intervals | Earliest | Latest |
|--------|--------|-----------|----------|--------|
| BTC | Binance | 15m, 1h, 4h | 2019-09-08 | 2026-03-28 |
| BTC | Binance | mark-15m, mark-1h, mark-4h | 2019-09-08 | 2026-03-28 |
| BTC | Hyperliquid | 15m, 1h, 4h | ~2026 | 2026-03-28 |

## Recommended Test Periods

### Bull Periods (Long strategies)

| Period | BTC Range | Character |
|--------|-----------|-----------|
| Oct 1 – Nov 30, 2024 | ~$60k → $99k | Pre-ATH rally, clean sustained uptrend |
| Jan 1 – Mar 31, 2024 | ~$42k → $73k | ETF-driven rally |
| Oct 1 – Dec 31, 2023 | ~$27k → $44k | Strong recovery rally |
| Jan 1 – Mar 31, 2023 | ~$16k → $28k | Bear market bottom reversal |
| Jul 1 – Nov 10, 2021 | ~$30k → $69k | Second leg of 2021 bull run |

### Bear Periods (Short strategies / trend filter validation)

| Period | BTC Range | Character |
|--------|-----------|-----------|
| Feb 1 – Mar 31, 2026 | ~$79k → $65k | Sustained downtrend, good for validating long trend filters block entries |
| May 1 – Jun 30, 2022 | ~$38k → $19k | Luna/3AC crash, steep drawdown |
| Nov 1 – Dec 31, 2022 | ~$21k → $16k | FTX collapse, capitulation phase |
| May 1 – Jul 20, 2021 | ~$58k → $30k | China mining ban crash |

### Ranging / Choppy Periods (Grid strategies)

| Period | BTC Range | Character |
|--------|-----------|-----------|
| Mar 1 – Sep 30, 2023 | ~$23k → $27k | Tight range, low volatility consolidation |
| Aug 1 – Sep 30, 2024 | ~$58k → $64k | Pre-breakout consolidation |
| Jul 1 – Sep 30, 2022 | ~$19k → $20k | Post-crash accumulation range |

## Usage Notes

- A 200 EMA on 1h chart = ~8 days lookback. In sustained trends it lags significantly.
- EMA cross (50/200) is more responsive than price-above-EMA for trend gating.
- Always test across both bull and bear periods to avoid overfitting.
- The backtest warmup period consumes ~200 candles, so start dates should allow for this.
