# Strategy Replay Debugger with Counterfactual Branching

**Series: Innovative Features (1 of 3)**
See also: [Natural-Language Decision Explanations](natural-language-decision-explanations.md) |
[Adversarial Stress Testing](adversarial-stress-testing.md)

Parent: [0-knowledge](../) | [TOC](README.md)

> *"Debug your trading strategy like you debug code."*

---

## Overview

A step-through replay debugger for trading strategy execution that lets users inspect
full internal state at every candle close, fork the timeline with modified parameters,
and compare actual vs. counterfactual outcomes side-by-side.

No retail trading platform offers this capability. Time-travel debugging exists in
software engineering (rr, Replay.io, Elm debugger) but has **never been applied to
trading strategy execution**. The closest analogy is a IDE debugger with breakpoints,
variable inspection, and the ability to change a value mid-run and continue — except
the "program" is your trading strategy and the "variables" are grid levels, indicators,
risk state, and real P&L.

---

## The Problem This Solves

When a trading strategy underperforms, traders have two options today:

1. **Stare at a backtest summary** — total P&L, win rate, max drawdown. But this tells
   you *what* happened, not *why*. You can't see which specific candle caused the
   drawdown, which indicator was off, or whether the grid spacing was too tight at
   that particular market moment.

2. **Re-run the entire backtest** with different parameters and compare totals. But
   this is slow, coarse-grained, and doesn't isolate which change mattered or when.

The Strategy Replay Debugger eliminates both problems by turning backtest analysis
from a black box into an interactive, inspectable, forkable experience.

---

## Why This Project Is Uniquely Positioned

This feature is not bolted on — it falls naturally out of architectural decisions
already made:

| Existing Decision | Why It Enables This Feature |
|---|---|
| Backtest-live code parity (`IExecutionEngine`) | Replay uses the same engine as live — no separate replay infrastructure needed |
| Deterministic candle-close execution (`CandleClock`) | Execution is a discrete, reproducible state machine with one state per candle — perfect for step-through |
| GridController state machine (8 states) | State at any candle is a well-defined enum + data payload — trivially serializable |
| Signal contracts (persisted, typed) | Every decision produces a typed, stored signal — this is the "instruction trace" |
| `StrategyConfig` as JSON | Fork a branch by cloning the JSON and changing one field — no recompilation |
| `RiskEngine` as mandatory gate | Every signal has an approval/rejection record — inspectable at each step |
| `MarketContext` + LLM context | Full decision inputs already aggregated into one object per candle |
| `BacktestMetricsCalculator` | Reused to compute metrics on each counterfactual branch |
| `ReplayClock` (from 18-backtesting-architecture) | Already supports sequential candle playback — extend with pause/step/rewind |

---

## Deep Dive: Step-Through Replay

### Concept

For any completed backtest run (or a past live trading session that captured snapshots),
the user can walk through execution one candle at a time, like stepping through code
in a debugger.

### Controls

| Control | Keyboard | Action |
|---|---|---|
| Play | `Space` | Auto-advance candles at configurable speed (1x, 2x, 5x, 10x) |
| Pause | `Space` | Freeze at current candle |
| Step Forward | `→` | Advance exactly one candle |
| Step Back | `←` | Rewind exactly one candle (from snapshot history) |
| Jump to Start | `Home` | Return to first candle in the replay |
| Jump to End | `End` | Jump to final candle |
| Jump to Signal | `S` | Skip forward to next candle that emitted a signal |
| Jump to Fill | `F` | Skip forward to next candle where an order was filled |
| Set Breakpoint | `B` | Pause when a condition is met (see Conditional Breakpoints below) |

### State Inspector

At each candle, a collapsible sidebar displays the full engine state in grouped panels:

**Indicators Panel**
```
EMA-21:  67,482.30    (rising ↑)
EMA-50:  67,210.15    (rising ↑)
EMA-200: 65,890.00    (rising ↑)
RSI-14:  43.2         (neutral zone)
VWAP:    67,350.00
Trend:   Bullish (4H EMA-50 > EMA-200, rising)
Bias:    Bullish (1H RSI > 40)
```

**Grid State Panel**
```
Lifecycle:  Active
Levels:     4 deployed
  L1: $67,246 — FILLED   (0.20 BTC @ 15:45 UTC)
  L2: $67,011 — OPEN     (limit buy 0.25 BTC)
  L3: $66,776 — OPEN     (limit buy 0.25 BTC)
  L4: $66,541 — OPEN     (limit buy 0.30 BTC)
Avg Entry:  $67,246
TP Target:  $67,784 (0.8%)
Hedge:      Not triggered
```

**Risk Engine Panel**
```
Exposure:       12.0% of 25.0% max     ✅ Approved
Daily P&L:      -0.3% of 2.0% limit    ✅ Within bounds
Leverage:       2.4x of 5.0x max       ✅ Approved
Cooldown:       Inactive
Signals Blocked: 0 this session
```

**Signals Panel**
```
Candle 15:45 UTC — 1 signal emitted:
  DeployGrid {
    symbol: "BTC",
    gridPlan: { levels: 4, entry: 67246, tp: 67784 },
    reason: "15m pullback 0.62% below EMA-21; 4H trend bullish; 1H RSI confirmed"
  }
  Status: Generated → Validated → Approved → Executed
```

**Orders Panel**
```
Placed:    4 limit buys (grid L1-L4)
Filled:    1 (L1 at $67,246)
Cancelled: 0
Rejected:  0
```

**LLM Context Panel**
```
Sentiment:  Neutral-Positive (confidence: 0.72)
Regime:     Trending
Event Risk: Low
Mode:       Normal (100% position sizing)
Summary:    "No significant macro headwinds. BTC trending within established range."
```

### Conditional Breakpoints

Users can set breakpoints that pause replay when conditions are met:

| Breakpoint Type | Example |
|---|---|
| Signal emitted | Pause when any `DeployGrid` signal fires |
| Grid state change | Pause when lifecycle transitions to `FullyFilled` |
| Risk threshold | Pause when exposure exceeds 20% |
| Drawdown threshold | Pause when session drawdown exceeds 1.5% |
| Indicator cross | Pause when EMA-21 crosses below EMA-50 |
| LLM mode change | Pause when strategy mode shifts to `Defensive` |
| Custom expression | Pause when `rsi < 30 AND gridLifecycle == Active` |

Breakpoints are defined in a simple expression language evaluated against the
`StrategyStateSnapshot` at each candle.

### Chart Integration

The TradingView Lightweight Charts display updates in sync with the replay:

- Candle-by-candle rendering (chart builds progressively during play)
- Grid level lines appear/disappear as the GridController deploys/cancels grids
- Fill markers (triangles) appear on filled candles
- Active orders shown as dashed horizontal lines
- TP target shown as green dashed line
- Hedge level shown as red dashed line
- Current candle highlighted with a vertical marker
- Signal annotations appear below relevant candles

---

## Deep Dive: Counterfactual Branching

### Concept

At any paused candle during replay, the user can **fork the timeline** — create a
parallel universe where one or more strategy parameters are different — and watch
both timelines play out over the remaining candles.

This answers the question every trader asks after a bad trade:
*"What if I had done X differently?"*

### How Forking Works

```
Timeline A (actual):       ──────────────●──────────────────────→
                                         │ Fork Point (candle 47)
Timeline B (counterfactual):             └──●──●──●──●──●──●───→
                                            (modified config)
```

1. User pauses replay at candle N
2. User clicks **Fork** — a config editor opens pre-filled with the current
   `StrategyConfig` JSON
3. User modifies one or more parameters (e.g., grid spacing, TP %, hedge %)
4. User clicks **Run Branch**
5. The `CounterfactualRunner`:
   a. Clones the `StrategyStateSnapshot` at candle N (full state: indicators,
      grid state, risk state, position state)
   b. Replaces the `StrategyConfig` with the modified version
   c. Creates a new `ReplayClock` starting at candle N+1
   d. Runs the standard pipeline (StrategyEngine → GridController → RiskEngine →
      SimulatedExecutionEngine) forward through remaining candles
   e. Captures a `StrategyStateSnapshot` at each candle on the new branch
6. The `TimelineDiffEngine` computes metrics for both branches

### What Can Be Modified at a Fork

| Config Section | Forkable Parameters | Example Change |
|---|---|---|
| `grid` | levels, spacing[], sizeDistribution[] | "What if 6 levels instead of 4?" |
| `exit` | takeProfitPercent, trailingStop | "What if 1.2% TP instead of 0.8%?" |
| `hedge` | enabled, percent | "What if hedge was disabled?" |
| `entry` | pullbackPercent, confirmationCandles | "What if I waited for 1.5% pullback?" |
| `risk` | maxExposure, dailyLossLimitPercent, cooldownMinutes | "What if max exposure was 15%?" |
| `trend` | emaFast, emaSlow, emaTrend | "What if I used EMA-10/30 instead of 20/50?" |
| `bias` | rsiLength, rsiThreshold | "What if RSI threshold was 45?" |

### Branch Comparison View

The UI shows branches side-by-side:

```
┌─────────────────────────────────┬─────────────────────────────────┐
│  Branch A: Actual (0.8% TP)     │  Branch B: Fork (1.2% TP)       │
├─────────────────────────────────┼─────────────────────────────────┤
│  Chart with candles + grid      │  Chart with candles + grid       │
│  lines + fill markers           │  lines + fill markers            │
├─────────────────────────────────┼─────────────────────────────────┤
│  P&L: +$342                     │  P&L: +$518          (+$176)     │
│  Trades: 12                     │  Trades: 8           (-4)        │
│  Win Rate: 75%                  │  Win Rate: 87.5%     (+12.5%)    │
│  Max DD: -1.8%                  │  Max DD: -2.1%       (-0.3%)     │
│  Avg Hold: 2.3h                 │  Avg Hold: 4.1h      (+1.8h)    │
│  Hedges: 2                      │  Hedges: 1           (-1)        │
└─────────────────────────────────┴─────────────────────────────────┘
```

### Multi-Branch Comparison

Users can create multiple branches from different fork points or with different
parameter changes. A comparison table summarises all branches:

| Branch | Fork Candle | Change | Total P&L | Win Rate | Max DD | Trades |
|---|---|---|---|---|---|---|
| Actual | — | — | +$342 | 75.0% | -1.8% | 12 |
| Fork A | #47 | TP → 1.2% | +$518 | 87.5% | -2.1% | 8 |
| Fork B | #47 | Spacing × 1.5 | +$289 | 70.0% | -1.4% | 14 |
| Fork C | #23 | Hedge off | +$410 | 72.7% | -3.2% | 11 |

### Nested Forks

Branches can be forked from other branches, creating a tree:

```
Actual ────────────●────────────────────────────→
                   │
Fork A ────────────└──●────────────●────────────→
                      │            │
Fork A-1 ─────────────└────────────└──●─────────→
```

This lets users explore cascading "what if" questions — e.g., "What if I changed
the TP at candle 47, and *then* also changed the hedge at candle 62?"

---

## Architecture

### New Components

| Component | Layer | Responsibility |
|---|---|---|
| `StrategyStateSnapshot` | Domain | Immutable value object capturing full engine state at one candle close |
| `ReplayController` | Application | Orchestrates step / play / pause / rewind / fork; manages replay session state |
| `CounterfactualRunner` | Application | Clones snapshot at fork point, applies config delta, runs parallel replay |
| `TimelineDiffEngine` | Application | Compares two or more branches; computes metric deltas |
| `BreakpointEvaluator` | Application | Evaluates conditional breakpoint expressions against each snapshot |
| `ISnapshotStore` | Infrastructure | Persists and retrieves `StrategyStateSnapshot` records |
| `ReplaySession` | Domain | Tracks one debugger session: the base run, all forks, current position |

### Data Model

```
StrategyStateSnapshot
├── SnapshotId          (Guid)
├── SessionId           (FK → ReplaySession)
├── BranchId            (FK → CounterfactualBranch)
├── SequenceNumber      (int — ordinal within the branch)
├── CandleCloseTimeUtc  (DateTime)
├── Symbol              (string)
├── Timeframe           (string)
├── IndicatorState      (JSON)
│   ├── emaFast         (decimal)
│   ├── emaSlow         (decimal)
│   ├── emaTrend        (decimal)
│   ├── rsi             (decimal)
│   └── vwap            (decimal)
├── GridState           (JSON)
│   ├── lifecycle       (enum: Inactive..Closed)
│   ├── levels[]        (price, size, status per level)
│   ├── avgEntry        (decimal?)
│   └── tpTarget        (decimal?)
├── RiskState           (JSON)
│   ├── exposurePercent (decimal)
│   ├── dailyPnlPercent (decimal)
│   ├── leverage        (decimal)
│   ├── cooldownActive  (bool)
│   └── signalsBlocked  (int)
├── SignalsEmitted      (JSON — array of signal contracts)
├── LlmContext          (JSON — sentiment, regime, event risk)
├── OrderActions        (JSON — placed, filled, cancelled, rejected)
├── PositionState       (JSON — open qty, side, unrealised P&L)
├── ConfigSnapshot      (JSON — active StrategyConfig at this candle)
└── CreatedAtUtc        (DateTime)

CounterfactualBranch
├── BranchId            (Guid)
├── SessionId           (FK → ReplaySession)
├── ParentBranchId      (Guid? — null for the "actual" timeline)
├── ForkSequenceNumber  (int — snapshot sequence where the fork occurred)
├── ConfigDelta         (JSON — only the fields that changed)
├── FullConfig          (JSON — complete merged config for this branch)
├── Metrics             (JSON — computed after branch completes)
│   ├── totalPnl        (decimal)
│   ├── winRate         (decimal)
│   ├── maxDrawdown     (decimal)
│   ├── tradeCount      (int)
│   ├── avgHoldTime     (TimeSpan)
│   └── hedgeCount      (int)
└── CreatedAtUtc        (DateTime)

ReplaySession
├── SessionId           (Guid)
├── UserId              (FK → User)
├── SourceRunId         (FK → StrategyRun or BacktestRun)
├── SourceType          (enum: Backtest | LiveHistory)
├── Symbol              (string)
├── StartTimeUtc        (DateTime)
├── EndTimeUtc          (DateTime)
├── TotalCandles        (int)
├── Branches            (ICollection<CounterfactualBranch>)
├── Breakpoints         (JSON — array of breakpoint definitions)
└── CreatedAtUtc        (DateTime)
```

### Integration with Existing Components

```
Step-Through Replay Flow:
  ISnapshotStore.LoadSnapshots(sessionId, branchId)
    → ReplayController
      → Advance/rewind through snapshot sequence
      → BreakpointEvaluator.Check(snapshot) — pause if triggered
      → Push snapshot to UI via SignalR/WebSocket

Fork Flow (at paused candle):
  ReplayController.Fork(configDelta)
    → Clone StrategyStateSnapshot at current position
    → CounterfactualRunner
      → Merge configDelta into existing StrategyConfig
      → Create new CounterfactualBranch record
      → New ReplayClock (from fork candle forward)
        → StrategyEngine + GridController + RiskEngine + SimulatedExecutionEngine
        → Capture StrategyStateSnapshot per candle on new branch
    → TimelineDiffEngine.Compare(branchA, branchB)
      → Return BranchComparisonResult (metric deltas)

Snapshot Capture Flow (during backtest):
  BacktestRunner pipeline (existing)
    → After each candle: SnapshotCaptureMiddleware
      → Serialize current IndicatorSnapshot, GridState, RiskState,
        SignalsEmitted, LlmContext, OrderActions, PositionState
      → Persist StrategyStateSnapshot via ISnapshotStore
```

### Snapshot Storage Strategy

Snapshots are the largest data volume in this feature. Storage strategy:

- **During backtest** — capture every candle snapshot; store in database
- **During live trading** — capture snapshots only when signals are emitted or
  state transitions occur (to limit volume); full snapshots on demand via
  "record session" toggle
- **Retention** — configurable per user; default 30 days for backtest snapshots,
  90 days for live snapshots
- **Compression** — JSON fields use differential encoding (store delta from
  previous snapshot where possible) to reduce storage by ~60-70%

### Performance Considerations

| Scenario | Volume | Mitigation |
|---|---|---|
| 30-day backtest at 15m candles | ~2,880 snapshots | Paginated loading; lazy-load JSON fields |
| 5 counterfactual branches | ~14,400 total snapshots | Branches share candle data; only fork-forward snapshots stored |
| Real-time fork computation | 1-3 seconds for 30 days | Run on background thread; show progress indicator |

---

## UI Integration

### Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  [Branch: Actual ▾]  [Fork]  [Compare Branches]  [Breakpoints]  │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │  TradingView Chart                                       │    │
│  │  (candles build progressively during play)                │    │
│  │  (grid lines, fill markers, TP/hedge overlays)            │    │
│  └──────────────────────────────────────────────────────────┘    │
│                                                                  │
│  ◄◄  ◄  ▶  ►  ►►   [===●============================] 47/2880  │
│  Home Back Play Step End        Timeline Scrubber                │
│                                                                  │
├──────────────────────────┬───────────────────────────────────────┤
│  State Inspector         │  Signals & Orders                     │
│  ┌────────────────────┐  │  ┌─────────────────────────────────┐  │
│  │ Indicators    [▾]  │  │  │ DeployGrid @ 15:45 UTC          │  │
│  │ Grid State    [▾]  │  │  │ Status: Executed ✅              │  │
│  │ Risk Engine   [▾]  │  │  │ Fill: L1 $67,246 (0.20 BTC)    │  │
│  │ LLM Context   [▾]  │  │  │                                 │  │
│  │ Position      [▾]  │  │  │ [Explain This Decision]         │  │
│  └────────────────────┘  │  └─────────────────────────────────┘  │
└──────────────────────────┴───────────────────────────────────────┘
```

### Interaction Flow

1. User navigates to **Backtest Results** → selects a completed run → clicks **Open in Debugger**
2. Replay session loads; chart shows first candle; inspector shows initial state
3. User presses Play — candles render progressively, inspector updates in real-time
4. User notices drawdown at candle #47 — presses Pause
5. User inspects grid state: L1 filled but price continued dropping through L2, L3
6. User clicks **Fork** — changes grid spacing from [0.35, 0.7, 1.05, 1.4] to [0.5, 1.0, 1.5, 2.0]
7. Fork branch runs in background; progress bar shows completion
8. User clicks **Compare Branches** — side-by-side view shows wider spacing avoided
   the cascading fills, resulting in lower drawdown but fewer trades
9. User clicks **Explain This Decision** on the original branch at candle #47 — sees
   natural-language explanation (see [Decision Explanations](natural-language-decision-explanations.md))

---

## Competitive Analysis

| Dimension | 3Commas | QuantConnect | TradingView Strategy Tester | This Feature |
|---|---|---|---|---|
| Backtest results | Summary only | Detailed metrics | Summary + chart | Candle-level state snapshots |
| Candle-level state inspection | ❌ | ❌ | ❌ | ✅ Full engine state at every candle |
| Step-through replay controls | ❌ | ❌ | ❌ | ✅ Play/pause/step/rewind/jump |
| Conditional breakpoints | ❌ | ❌ | ❌ | ✅ Break on signal, threshold, expression |
| Counterfactual branching | ❌ | ❌ | ❌ | ✅ Fork timeline, modify config, compare |
| Multi-branch comparison | ❌ | ❌ | ❌ | ✅ Side-by-side + metrics table |
| Nested forks | ❌ | ❌ | ❌ | ✅ Fork from forks |

---

## Implementation Phases

### Phase 1 — Snapshot Capture (Foundation)

**Goal:** Capture full engine state at every candle during backtest runs.

- Define `StrategyStateSnapshot` entity and EF Core mapping
- Create `ISnapshotStore` interface with SQLite implementation
- Add `SnapshotCaptureMiddleware` into the backtest pipeline (after each candle)
- Serialize all state (indicators, grid, risk, signals, orders, LLM context)
- Create `ReplaySession` and `CounterfactualBranch` entities (actual branch only)
- Unit tests for snapshot serialization round-trip, storage, retrieval

**Depends on:** Backtesting pipeline (doc 18), domain entities (doc 04)

### Phase 2 — Step-Through Replay

**Goal:** Navigate through captured snapshots with debugger-like controls.

- Implement `ReplayController` with play/pause/step/rewind/jump
- Load snapshots from `ISnapshotStore` for a given session and branch
- Push current snapshot to UI via SignalR WebSocket connection
- Implement timeline scrubber component (Angular)
- Implement state inspector panels (collapsible, grouped by domain)
- Implement progressive chart rendering (TradingView Lightweight Charts)
- "Jump to next signal" and "Jump to next fill" navigation

**Depends on:** Phase 1, Angular UI scaffolding (doc 07), charting (doc 09)

### Phase 3 — Conditional Breakpoints

**Goal:** Pause replay automatically when user-defined conditions are met.

- Define breakpoint expression language (simple DSL or JSON predicate)
- Implement `BreakpointEvaluator` — evaluates expressions against each snapshot
- UI: breakpoint manager panel (add, edit, enable/disable, delete)
- Pre-built breakpoint templates (signal emitted, drawdown threshold, mode change)

**Depends on:** Phase 2

### Phase 4 — Counterfactual Branching

**Goal:** Fork the timeline with modified parameters and compare outcomes.

- Implement `CounterfactualRunner` — clone state, apply config delta, run pipeline
- Implement `TimelineDiffEngine` — compute metric deltas between branches
- UI: Fork button → config editor → run branch → progress indicator
- UI: branch selector dropdown
- UI: side-by-side comparison view (charts + metrics)
- UI: multi-branch comparison table
- Support nested forks (fork from a fork)

**Depends on:** Phase 2, backtesting pipeline components

### Phase 5 — Live Session Recording

**Goal:** Enable replay debugging on live trading sessions, not just backtests.

- Add opt-in "record session" toggle to live worker
- Capture snapshots during live execution (signal candles + periodic full snapshots)
- Allow opening live session recordings in the replay debugger
- Mark recorded sessions in the session list with "Live" badge

**Depends on:** Phase 2, live worker (doc 14)

---

## Cross-References

- **Natural-Language Decision Explanations** — the "Explain This Decision" button
  in the replay debugger invokes the DecisionExplainer system described in
  [Decision Explanations](natural-language-decision-explanations.md)
- **Adversarial Stress Testing** — stress test scenarios can be loaded into the
  replay debugger as synthetic data sources, and their results inspected with the
  same step-through and branching tools described in
  [Adversarial Stress Testing](adversarial-stress-testing.md)
- **Backtesting Architecture** (doc 18) — the replay debugger extends the backtest
  pipeline with snapshot capture; the `SimulatedExecutionEngine` and `ReplayClock`
  are shared
- **Grid Controller** (doc 15) — grid lifecycle state is a primary inspectable in
  the state inspector; state transitions are debuggable step-by-step
- **Signal Contracts** (doc 16) — signals are the "instruction trace" displayed in
  the signals panel and used for "jump to next signal" navigation
