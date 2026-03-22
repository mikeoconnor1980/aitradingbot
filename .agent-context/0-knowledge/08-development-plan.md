# Development Plan

This project should be built using a safety-first sequence.

The goal is not to expose strategy CRUD or a polished dashboard as early as possible.
The goal is to prove that the trading system is deterministic, testable, recoverable,
and safe before it is allowed to place live orders.

The recommended build order is therefore:

1. prove the strategy in backtesting
2. prove the runtime in paper trading
3. prove recovery and safety controls
4. enable limited live execution
5. expand API and UI after the core loop is trustworthy

---

# Delivery Principles

- shared core pipeline for live and backtest
- strategies execute only on confirmed candle closes
- all orders must pass through the RiskEngine
- exchange state must be recoverable after restart or disconnect
- live trading is enabled only after paper-trading sign-off
- product UX should follow Backtest → Paper → Live

---

# Suggested v1 Scope

Keep v1 intentionally narrow:

- one exchange: Hyperliquid
- one symbol: BTC perpetual
- one strategy: GridStrategy
- one active strategy per user
- candle-based execution only
- no advanced optimisation or multi-strategy portfolio logic

This keeps the first implementation focused on correctness rather than breadth.

---

# Immediate Implementation Track (Lean v1)

For the first real build, treat the following as the lean v1 track:

1. Phase 1 — Solution Foundation
2. Phase 2 — Historical Data and Market Context
3. Phase 3 — Deterministic Backtester
4. Phase 4 — Paper Trading Runtime
5. Phase 5 — Exchange Integration, Reconciliation, and Recovery
6. Phase 6 — Safety Controls and Observability
7. Phase 7 — Controlled Live Rollout
8. Phase 8 — Minimal API and Essential UI

Explicitly defer Phase 9 until the core trading loop has been proven stable.

This means v1 should not try to deliver the full product surface.
It should deliver the smallest system that can:

- test the strategy honestly
- run safely on live market data
- recover cleanly from failures
- stop trading quickly when needed
- expose only the minimum controls and visibility required to operate it

---

# Phase 1 — Solution Foundation

Set up the solution structure and shared contracts used by both backtesting and live trading.

Main deliverables:

- .NET solution and projects created
- core domain models and interfaces added
- StrategyEngine, RiskEngine, PositionManager, and ExecutionEngine interfaces defined
- Signal contracts defined and persisted consistently
- configuration loading approach agreed

Exit criteria:

- project structure matches the intended architecture
- core interfaces compile cleanly
- no live-execution code is required to run the shared pipeline

---

# Phase 2 — Historical Data and Market Context

Build the historical data layer needed for deterministic backtesting.

Main deliverables:

- candle storage implemented for 4H, 1H, and 15m data
- historical data ingestion pipeline created
- market context builder created for indicators and higher-timeframe inputs
- local storage configured for repeatable backtest runs

Exit criteria:

- historical BTC data can be loaded reliably for defined date ranges
- MarketContext can be built deterministically from stored candles
- the same data set produces the same derived context repeatedly

---

# Phase 3 — Deterministic Backtester

Implement the shared strategy pipeline in replay mode before any live execution exists.

Main deliverables:

- ReplayEngine and ReplayClock implemented
- SimulatedExecutionEngine implemented
- GridStrategy, GridController, RiskEngine, and PositionManager integrated in backtest mode
- fees and slippage assumptions modelled
- backtest results and metrics recorded
- strategy version comparison supported

Exit criteria:

- backtests run end-to-end without touching live exchange code
- repeated runs over the same data produce identical outputs
- results include PnL, drawdown, and signal/execution traces
- the strategy can be rejected or revised based on evidence before live work proceeds

---

# Phase 4 — Paper Trading Runtime

Run the same trading pipeline on live market data with simulated execution.

Main deliverables:

- Hyperliquid market data connection implemented
- CandleClock and StrategyScheduler implemented
- live MarketStateStore updates working
- paper trading mode added using live market data and simulated fills
- per-user execution checkpoints persisted
- run history and paper-trade metrics recorded

Exit criteria:

- the strategy executes exactly once per closed candle in paper mode
- restarts do not cause duplicate signal generation
- paper-trade burn-in can run continuously for a defined period

---

# Phase 5 — Exchange Integration, Reconciliation, and Recovery

Only after paper trading is stable should the system integrate with live order submission.

Main deliverables:

- Hyperliquid order placement and cancellation implemented
- client order IDs and idempotency protections added
- persisted order journal added
- startup reconciliation implemented for open orders and positions
- partial-fill handling implemented
- stuck-order, orphan-position, and rejection handling implemented
- worker restart recovery path tested

Exit criteria:

- live and local state can be reconciled deterministically
- restart mid-grid does not create duplicate orders
- exchange rejection and partial-fill scenarios are handled correctly

---

# Phase 6 — Safety Controls and Observability

Add the controls required before any real user capital is exposed.

Main deliverables:

- emergency flatten implemented
- per-user kill switch implemented
- global kill switch implemented
- circuit breaker rules implemented
- structured logging and audit trail added
- alerts added for exchange failures, worker failures, and circuit-breaker events
- admin-visible system health and incident status added

Exit criteria:

- trading can be stopped quickly at user and platform level
- safety events are logged with reason and actor
- operational failures are visible without inspecting raw logs manually

---

# Phase 7 — Controlled Live Rollout

Enable live execution in a constrained rollout only after prior phases are signed off.

Main deliverables:

- live execution mode enabled behind explicit controls
- rollout policy defined for small-size live testing
- subscription gating and live-trading eligibility rules enforced
- operating procedures defined for incidents, restart, and manual intervention

Exit criteria:

- live mode is enabled only for approved test users or internal accounts
- system behaviour has passed backtest, paper-trading, and reconciliation checks
- kill switch and emergency flatten have been tested in realistic scenarios

---

# Phase 8 — Minimal API and Essential UI

Expose only the surfaces needed to configure, observe, and control the system safely.

Main deliverables:

- strategy configuration endpoints
- backtest endpoints
- bot status and safety-control endpoints
- minimal Angular screens for:
	- exchange connection
	- strategy configuration
	- backtest results
	- dashboard status
	- admin health and error status

Exit criteria:

- users can configure a strategy, run a backtest, and see runtime state
- admins can observe health and use core control functions
- the UI does not expose flows that bypass paper-trading or safety gates

---

# Phase 9 — Product Expansion

Once the trading core is trustworthy, expand the wider product surface.

Possible deliverables:

- richer order and position history
- strategy history and comparison UI
- signals explorer
- subscription and billing flows
- advanced admin tooling
- decision explanations
- replay debugger
- stress testing tools

These features should build on the proven core, not precede it.

---

# Promotion Gates

The system should not advance phases based only on feature completion.
It should advance when the previous risk has been retired.

Required gates:

- Backtest gate: strategy is reproducible and performance is understood
- Paper-trading gate: runtime is stable on live data without real orders
- Recovery gate: reconciliation and restart behaviour are verified
- Safety gate: kill switch, flatten, and circuit breaker are operational
- Live gate: rollout is explicitly approved and limited in scope

---

# Summary

This sequence deliberately delays broad UI and CRUD expansion.

That is intentional.

For a trading platform, the correct order is:

Backtest  
→ Paper Trade  
→ Reconcile and Recover  
→ Control Failure  
→ Go Live  
→ Expand Product

That build order gives the project the best chance of becoming safe and trustworthy,
rather than just feature-complete.