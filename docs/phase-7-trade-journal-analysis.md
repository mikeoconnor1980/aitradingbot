# Phase 7 — Trade / Journal Analysis

TradePilot now projects the existing persisted live-fill stream into durable logical trade evidence. `LiveOrder`, `LiveFill`, `GridCycle`, exchange `ClosedPnl`, and Phase 6 `StrategyEvaluation` remain the execution facts; `TradeJournalRecord` is the completed position-lifecycle aggregate used for historical analysis.

## Lifecycle and identity

- A logical trade opens on the first strategy-owned opening fill for a user, strategy, and symbol while no matching journal is open.
- Its stable TradePilot identity is `TradeJournalRecord.Id`, a generated GUID. Exchange order IDs, fill IDs, and grid-cycle IDs remain source evidence but are not the trade identity.
- Further opening fills are scale-ins. Exit fills below cumulative entry quantity produce `PartiallyClosed`; scale-outs remain part of the same logical trade.
- The trade closes when cumulative exit quantity equals cumulative entry quantity. An over-close or exit timestamp before entry is rejected for explicit reconciliation rather than silently changing the logical boundary. The final fill timestamp is the exit time and duration is exactly `ExitTimeUtc - EntryTimeUtc` in milliseconds.
- A closed record rejects later fill mutation. A close without an open record is logged for explicit reconciliation and does not fabricate history.
- Existing rows are not automatically backfilled because `LiveFill` historically lacked reliable strategy/version/evaluation and logical-position linkage.

This boundary matches the current exchange net-position execution model. Concurrent strategies sharing one user and symbol remain a reconciliation edge case because the venue position is netted.

## Journal contract

Each journal preserves strategy GUID/name, integer version, Phase 6 configuration identity, symbol, long/short side, lifecycle, entry/exit timestamps, quantity-weighted prices, cumulative quantities, leverage when known, PnL and costs, duration, excursions, entry/exit evaluation IDs, exit reason, recorded entry regime, timeframe, exchange, and source lifecycle ID. Existing `LiveFill` rows now optionally link to the logical journal and retain grid-cycle, trade-type, and entry/exit role.

The entry evaluation is the persisted Phase 6 evaluation that approved the first opening signal. Later scale-ins do not replace it. A strategy-driven exit links its own evaluation. Exchange protection exits retain stop-loss/take-profit reason without falsely creating an exit evaluation.

## Weighted prices and PnL

For fills with price \(p_i\) and quantity \(q_i\):

\[
\text{weighted price}=\frac{\sum_i p_iq_i}{\sum_i q_i}
\]

Entry and exit legs use the same formula independently.

- `GrossPnl` is the sum of exchange-reported closed PnL for exit fills; TradePilot does not re-infer it from prices.
- `Fees` sums every linked opening, scale-in, partial-exit, and final-exit fill fee.
- `Funding` is nullable. Current account/exchange capabilities do not provide reliable trade-level funding payments, so public funding rates are not substituted.
- `NetPnl = GrossPnl - Fees + (Funding ?? 0)`. It is net of every known cost; `FundingComplete` is false in analytics whenever any selected trade has unavailable funding.

## MFE and MAE

Excursions are finalized at close using existing persisted candles for the entry timeframe and exchange source. Candle timestamps from entry through exit are inclusive; entry and exit fill prices are also candidates. No indicator or market classification is recalculated.

The normalized baseline is the final quantity-weighted entry price and total cumulative entry quantity:

- Long MFE: `max(0, highestPrice - entryPrice) * quantity`.
- Long MAE: `min(0, lowestPrice - entryPrice) * quantity`.
- Short MFE: `max(0, entryPrice - lowestPrice) * quantity`.
- Short MAE: `min(0, entryPrice - highestPrice) * quantity`.
- Percent values divide the per-unit excursion by weighted entry price and multiply by 100.

This provides deterministic candle-granularity evidence. For scale-ins, the normalized amount uses final cumulative size for the full lifetime; it is not a tick-level reconstruction of changing intrabar exposure. Missing candles or calculation failures leave excursion fields null and emit telemetry.

## Application capabilities and analytics

- `GetTradesQuery`: bounded newest-first completed trades with strategy, version, symbol, side, exit-time range, and outcome filters. Analytics use the same exit-time window, so “this week” means PnL realised this week.
- `GetTradeQuery`: one owned journal with linked entry/exit Phase 6 evidence.
- `GetTradeAnalyticsQuery`: database-side aggregate facts.
- `GetStrategyTradeAnalyticsQuery`: comparisons by persisted strategy version and recorded entry regime.

Definitions:

- Winners/losers/breakeven use `NetPnl > 0`, `< 0`, or `= 0`.
- Win rate is winners divided by all completed trades, as a percentage.
- Average win/loss are mean positive/negative `NetPnl`; absent categories return null.
- Average net PnL per trade is total `NetPnl / TradeCount`.
- Profit factor is sum of positive `NetPnl / abs(sum of negative NetPnl)`. A zero-loss denominator returns null plus `ProfitFactorHasZeroLossDenominator=true`; no infinity or NaN is returned.
- Average duration uses persisted duration milliseconds.
- MFE/MAE averages ignore unavailable excursion values and remain null if none exist.
- Best/worst are deterministic by net PnL, then latest exit timestamp.
- Version and regime groups reuse persisted values. `Unavailable` is an explicit group; no current strategy configuration or market state is substituted.

## Analyst integration

The native read-only tools are `get_recent_trades`, `get_trade`, `get_trade_analytics`, and `get_strategy_trade_analytics`. They call Application directly, never MCP. Analyst policy requires server-side aggregates for PnL, win rate, profit factor, costs, duration, excursions, version comparisons, and regime totals.

Phase 7 adds no execution authority, MCP surface, backtest experimentation, or Angular UI. Phase 2 classifications and their documented concerns are unchanged.
