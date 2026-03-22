# Project Review — AI Grid Trading System

**Reviewer:** Claude Opus 4.6  
**Date:** 2026-03-14  
**Status:** Pre-code (documentation only)

---

## Overall Verdict: Strong Foundation, Viable Project

The documentation is genuinely impressive for a pre-code project. The architecture is sound and buildable.

---

## What's Good

### Architecture quality is high

The clean layered pipeline (Strategy → Signals → Risk → Position → Execution) is the right pattern. Separating "intent" (signals) from "action" (execution) is how professional trading systems work.

### CandleClock / deterministic execution

One of the best decisions. Most retail bots fail because they execute on partial candles. Candle-close-only execution with checkpoint deduplication solves a real class of bugs.

### Shared code between live and backtest

Via `IExecutionEngine` is critical and identified early. This is the single biggest architectural mistake most solo trading bot projects make — building live and backtest as separate systems.

### LLM as context-modifier, not decision-maker

The correct boundary. LLM output is too unreliable to generate orders directly but useful for regime classification.

### Risk engine as a gate

Signals must pass through and never bypass the risk engine. This is the right safety model.

---

## What I'd Change

### 1. SQLite is a risk for a live trading system

SQLite works for simplicity, but a concurrent Worker writing fills/positions while the API reads them will hit locking issues under load. Design the persistence layer with a swap in mind. Use WAL mode at minimum and document the upgrade path.

### 2. The strategy edge is thin

A 4H/1H/15m pullback grid on BTC perps is a crowded setup. The 0.8% take-profit target is tight relative to BTC volatility, making the system very sensitive to fees and slippage.

With Hyperliquid maker/taker fees, a 0.8% TP with 4 grid levels means each fill needs to overcome ~0.05–0.1% in fees, eating a meaningful chunk of edge.

With 4 grid fills at 0.35% spacing, the average entry is roughly -0.85% from the first fill. A 0.8% TP from average means price needs to recover to roughly the first fill level. That's viable in a trending market but will bleed in chop. The hedge at 25–50% adds significant cost when it triggers.

**Action:** Model the breakeven win-rate needed before writing code.

### 3. No mention of funding rate exposure

BTC perps on Hyperliquid have material funding rates (can reach 0.1%+ per 8h). The strategy holds positions through grid fills and take-profit — potentially hours to days of funding exposure. This needs to be modelled in backtesting and tracked in the risk engine.

### 4. The config schema is over-designed for v1

7 config sections, strategy versioning, parameter sweeps, and AI sentiment modes are planned before a single line of code exists. Ship v1 with hardcoded or minimal config. Get the pipeline working end-to-end first. Config flexibility is a v2 concern.

### 5. Missing: order reconciliation and state recovery

The docs mention reconnection briefly but don't detail how the system recovers if:

- The worker crashes mid-grid (3 of 4 levels filled)
- An order fills while the worker is offline
- Hyperliquid rejects an order after the signal was "Executed"

This is where most trading bots fail in production. An **order reconciliation loop** that periodically syncs local state against exchange state is needed. Design this before coding the execution engine.

### 6. Missing: kill switch

No mention of an emergency shutdown mechanism. Required:

- A single API endpoint or button that cancels all open orders and flattens all positions immediately
- A circuit breaker that triggers automatically on anomalous behaviour (rapid consecutive losses, unexpected position size, exchange errors)

### 7. The Angular UI is premature

For a single-user personal trading bot, a full Angular application with strategy builder, chart overlays, and backtesting UI is a massive time sink. Start with a terminal dashboard or simple status page. The core value is in the trading engine, not the UI.

### 8. Missing: logging and observability

The docs mention `/data/logs` but don't describe structured logging, alerting, or monitoring. For a system that manages real money:

- Every signal, order, fill, and state transition should be logged with correlation IDs
- Alerts (Telegram, email, etc.) for: errors, unexpected states, daily PnL breaches, exchange connectivity loss
- Strategy execution should produce structured audit trails

### 9. Docker on a VPS — consider the latency tradeoff

Docker adds ~1–2ms overhead. For a candle-close strategy this doesn't matter, but verify that the VPS is geographically close to Hyperliquid's infrastructure. Network latency on order placement matters more than compute latency.

---

## Recommended Build Order

1. **Backtesting engine first** — prove the strategy has edge before building live infra
2. **Core pipeline** (Strategy → Signal → Risk → SimulatedExecution) — shared code
3. **Hyperliquid integration** with reconciliation and kill switch
4. **Worker with CandleClock** — live execution
5. **Minimal API** — status, start/stop, emergency flatten
6. **UI** — only after the bot is running profitably

---

## Bottom Line

The project is well-architected and buildable. The main risks are:

- **Strategy edge** — the grid pullback setup needs rigorous backtesting with realistic fees/funding before committing to the full build
- **Scope creep** — 20 design docs and zero code is a signal to start building the minimum viable version now
- **Production resilience** — order reconciliation, kill switches, and alerting are more important than config flexibility or UI

Start with the backtester, prove the numbers, then build outward.
