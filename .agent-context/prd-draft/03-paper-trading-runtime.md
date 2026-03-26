# PRD: Paper Trading Runtime

**Status:** Draft  
**Priority:** 3 (next after backtesting proves the pipeline)  
**Date:** 2026-03-26  
**Phase:** Maps to Development Plan Phase 4

---

## Summary

Run the same trading pipeline built in the backtesting epic on live market data with simulated execution. This is the bridge between "strategy works on historical data" and "strategy is safe to trade real capital."

## Why This Follows Backtesting

- The shared pipeline (GridStrategy, GridController, RiskEngine) is already built and tested.
- Paper trading replaces `ReplayClock` → `CandleClock` and `SimulatedExecutionEngine` stays (but uses live prices).
- This proves the strategy behaves correctly on real-time data before any capital is at risk.
- The development plan explicitly requires paper-trading burn-in before live execution.

## Scope

### Live Market Data Connection

- `CandleClock` — triggers strategy execution on confirmed candle closes from Hyperliquid
- `StrategyScheduler` — per-user fan-out (single user for v1)
- Live `MarketStateStore` updates from WebSocket feed

### Paper Trading Mode

- Same `GridStrategy` → `GridController` → `RiskEngine` → `SimulatedExecutionEngine` pipeline
- Simulated fills using live market prices
- Per-user execution checkpoints persisted to SQLite
- Run history and paper-trade metrics recorded

### Worker Service

- Background service (`TradingApp.Worker`) orchestrates strategy execution
- Restart recovery: does not cause duplicate signal generation
- Checkpoint persistence: survives restarts cleanly

### UI

- Paper trading dashboard: active strategy state, simulated positions, signal log
- Start/stop paper trading run
- View paper trade performance metrics alongside backtest results

## Constraints

- One symbol: BTC perpetual
- One strategy: `GridStrategy`
- One user
- No real order placement — all execution is simulated
- Paper mode must be provably distinct from live (no accidental real trades)

## Acceptance Criteria

- [ ] Strategy executes exactly once per confirmed candle close in paper mode
- [ ] Restarts do not cause duplicate signal generation
- [ ] Paper-trade burn-in can run continuously for a defined period
- [ ] Execution checkpoints persist to database and survive worker restart
- [ ] Simulated fills use live market prices (not hardcoded)
- [ ] Paper trade results (PnL, drawdown, signals) are recorded and viewable
- [ ] Clear separation: paper mode cannot accidentally place live orders

## References

- [08-development-plan.md](.agent-context/0-knowledge/08-development-plan.md) — Phase 4
- [19-scheduling-architecture.md](.agent-context/0-knowledge/19-scheduling-architecture.md) — CandleClock, StrategyScheduler

---

## Notes

<!-- Flesh out further as needed -->
