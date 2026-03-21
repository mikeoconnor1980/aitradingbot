# Innovative Features

Three differentiating features that leverage the platform's architectural strengths
— deterministic candle-close execution, shared backtest/live pipeline, signal contracts,
and LLM context integration — to deliver capabilities no retail trading platform offers.

Parent: [0-knowledge](../)

---

## Documents

| # | Feature | Description |
|---|---|---|
| 1 | [Strategy Replay Debugger](strategy-replay-debugger.md) | Step-through replay of strategy execution with full state inspection, conditional breakpoints, counterfactual branching ("what if"), and side-by-side branch comparison. |
| 2 | [Natural-Language Decision Explanations](natural-language-decision-explanations.md) | Two-tier explanation engine (template-based + LLM-enhanced) that narrates every signal, every non-action, every risk rejection, and every near-miss in plain English. |
| 3 | [Adversarial Stress Testing](adversarial-stress-testing.md) | LLM-assisted synthetic scenario generation — flash crashes, funding spikes, liquidity gaps — to validate strategy resilience and risk engine parameters before risking capital. |

---

## How They Connect

```
Adversarial Stress Testing
  generates synthetic candles
    │
    ▼
Strategy Replay Debugger
  step-through replay with breakpoints + counterfactual branching
    │
    ▼
Natural-Language Decision Explanations
  explains every decision at every candle in both replay and stress tests
```

All three features reuse the same core pipeline:
`StrategyEngine → GridController → RiskEngine → SimulatedExecutionEngine`

The shared foundation is the **StrategyStateSnapshot** — a full serialisation of
engine state captured at every candle close during backtest, replay, or stress test
execution.

---

## Competitive Moat

These features are difficult for competitors to replicate because they require:

1. **Deterministic execution** — strategies must fire on confirmed candle closes
   to be reproducible (most platforms use continuous polling)
2. **Backtest-live code parity** — the same engine must run in both modes
   (most platforms have separate backtest and live codebases)
3. **Typed signal contracts** — every decision must produce a structured,
   persisted signal (most platforms emit raw orders with no audit trail)
4. **State machine lifecycle** — the grid controller's 8-state machine must be
   serialisable (most grid bots are stateless per-tick)

---

## Dependencies on Core Architecture

| Core Doc | What These Features Use From It |
|---|---|
| Trading Strategy (01) | Multi-timeframe indicator logic, pullback detection |
| Grid Controller (15) | 8-state lifecycle, signal emission, grid planning |
| Signal Contracts (16) | Typed signals as the "instruction trace" for replay |
| LLM Context (17) | Sentiment/regime/event-risk for explanations + scenario generation |
| Backtesting Architecture (18) | ReplayClock, SimulatedExecutionEngine, HistoricalDataProvider |
| Scheduling Architecture (19) | CandleClock, StrategyScheduler, deterministic timing |
