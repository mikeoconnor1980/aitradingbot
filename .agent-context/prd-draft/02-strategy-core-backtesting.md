# PRD: Strategy Core + Deterministic Backtesting

**Status:** Draft  
**Priority:** 2 (main epic — highest leverage work post-POC)  
**Date:** 2026-03-26  
**Phases:** Maps to Development Plan Phases 1–3

---

## Summary

Build the shared trading pipeline (domain model, grid strategy, risk engine) and prove it works through deterministic backtesting — before any live execution code is written.

## Why This First

- The POC de-risked exchange connectivity. Now we need to prove the trading logic.
- The architecture requires live and backtest to share the same pipeline. Building backtest-first forces that design.
- GridController's state machine gets exercised on thousands of candles, not just unit tests.
- Strategy parameters can be iterated with evidence before risking capital.

## Scope

### Phase 1 — Solution Foundation

- Domain entities: `Strategy`, `StrategyConfig`, `GridState`, `Signal`, `Order`, `Position`, `Fill`
- Core interfaces: `ITradingStrategy`, `IStrategyEngine`, `IRiskEngine`, `IPositionManager`, `IExecutionEngine`
- `GridController` + `GridPlanner` state machine (lifecycle: Inactive → Planning → Deploying → Active → PartiallyFilled → FullyFilled → Closing → Closed)
- Signal contracts: `DeployGrid`, `CancelGrid`, `TakeProfit`, `OpenHedge`, `AdjustHedge`, `CloseHedge`, `FlattenPosition`, `Cooldown`
- SQLite + EF Core persistence (strategy configs, backtest results)

### Phase 2 — Historical Data and Market Context

- Candle storage for 4H, 1H, 15m BTC data
- Ingestion pipeline from Hyperliquid (candle endpoint already exists in POC)
- `MarketContextBuilder` with indicators: EMA(20/50/200), RSI, VWAP
- Deterministic: same input always produces same context

### Phase 3 — Deterministic Backtester

- `ReplayEngine` + `ReplayClock` stepping through historical candles sequentially
- `SimulatedExecutionEngine` — limit order fills, hedge simulation, fee/slippage modelling
- Reuses **exact same** `GridStrategy` → `GridController` → `RiskEngine` pipeline as live
- Backtest results: PnL, drawdown, signal traces, grid lifecycle events
- Basic UI: configure strategy params, run backtest, view results

## Constraints

- One symbol: BTC perpetual
- One strategy: `GridStrategy`
- One user (single hardcoded identity — multi-tenant comes later)
- Candle-based execution only (confirmed closes)
- No advanced optimisation or multi-strategy portfolio logic

## Key Architecture Decisions

- Strategies execute only on confirmed candle closes (deterministic)
- All orders pass through RiskEngine — strategies never bypass risk checks
- Backtesting reuses live pipeline components (not a separate system)
- Signal contracts define the boundary between strategy logic and execution

## Acceptance Criteria

- [ ] Domain model entities compile and persist to SQLite
- [ ] GridController state machine transitions are tested for all lifecycle states
- [ ] Historical BTC candles can be ingested and stored reliably
- [ ] MarketContext built from stored candles is deterministic (same data → same output)
- [ ] Backtests run end-to-end without touching live exchange code
- [ ] Repeated runs over the same data produce identical outputs
- [ ] Results include PnL, drawdown, and signal/execution traces
- [ ] RiskEngine validates all signals before simulated execution
- [ ] Basic UI allows configuring a strategy, running a backtest, and viewing results

## References

- [01-trading-strategy.md](.agent-context/0-knowledge/01-trading-strategy.md)
- [04-domain-model.md](.agent-context/0-knowledge/04-domain-model.md)
- [14-strategy-runtime-model.md](.agent-context/0-knowledge/14-strategy-runtime-model.md)
- [15-grid-controller.md](.agent-context/0-knowledge/15-grid-controller.md)
- [16-signal-contracts.md](.agent-context/0-knowledge/16-signal-contracts.md)
- [18-backtesting-architecture.md](.agent-context/0-knowledge/18-backtesting-architecture.md)

---

## Notes

<!-- Flesh out further as needed -->
