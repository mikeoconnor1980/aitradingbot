# Adversarial Stress Testing

**Series: Innovative Features (3 of 3)**
See also: [Strategy Replay Debugger](strategy-replay-debugger.md) |
[Natural-Language Decision Explanations](natural-language-decision-explanations.md)

Parent: [0-knowledge](../) | [TOC](README.md)

> *"Don't wait for a crash to find out if your strategy survives one."*

---

## Overview

Adversarial Stress Testing uses the LLM to generate **synthetic worst-case market
scenarios** — flash crashes, liquidity gaps, funding rate spikes, cascading
liquidations, black swan events — and runs your strategy through them using the
existing backtesting pipeline.

The goal is not to predict these events, but to answer: **"If this happened,
would my strategy survive, and how badly would it hurt?"**

No retail trading platform offers this. Institutional risk teams build ad-hoc
stress tests manually. This feature automates scenario generation using AI and
makes institutional-grade resilience testing accessible to every user.

---

## The Problem This Solves

### The Backtest Survivorship Problem

Backtesting on historical data has a fundamental flaw: it only tests scenarios
that already happened. If BTC has never flash-crashed 15% in 5 minutes during
your test window, your backtest will never reveal that your strategy blows up
in that scenario.

Historical backtests answer: *"How would my strategy have performed in the past?"*
Stress tests answer: *"How would my strategy perform in a future that hasn't
happened yet — but could?"*

### The Confidence Gap

Traders often ask:
- "What's the worst that could happen?"
- "Am I risking ruin if there's a black swan?"
- "Is my hedge fast enough for a real crash?"
- "At what point does my risk engine actually stop me?"

Without stress testing, these are unanswerable. With it, they become concrete,
quantified, and debuggable.

### The Risk Engine Validation Problem

The RiskEngine has thresholds — max exposure 25%, daily loss 2%, leverage 5x.
But are these thresholds correct? Do they actually prevent catastrophic loss in
extreme conditions? Stress testing is the only way to validate risk parameters
against realistic adversarial conditions before they're tested by real markets.

---

## How It Works: End-to-End Flow

```
1. User selects a stress test scenario (preset or custom)
           │
2. ScenarioGenerator creates synthetic OHLCV candle data
           │
3. Synthetic candles are injected into HistoricalDataProvider
           │
4. Standard backtest pipeline runs:
   CandleClock → StrategyScheduler → StrategyEngine → GridController
   → RiskEngine → SimulatedExecutionEngine
           │
5. StrategyStateSnapshots captured at every candle ([Replay Debugger](strategy-replay-debugger.md))
           │
6. StressTestReportBuilder analyses results
           │
7. [DecisionExplainer](natural-language-decision-explanations.md) narrates what happened and why
           │
8. User reviews results in [Replay Debugger](strategy-replay-debugger.md)
```

The key insight: **the entire backtesting pipeline is reused unchanged.** The
only difference is the data source — synthetic candles instead of historical
candles. The strategy, grid controller, risk engine, and execution engine don't
know or care that the data is synthetic.

---

## Scenario Types

### Category 1: Market Shock Scenarios

Rapid, extreme price movements that test grid exposure and hedge response.

| Scenario | Description | What It Tests |
|---|---|---|
| **Flash Crash** | Price drops 10-20% in 5-15 minutes, then partially recovers | Grid fill cascade, hedge trigger speed, max drawdown |
| **Flash Rally** | Price spikes 10-15% in 5-15 minutes | Missed entry, TP execution on rapid move, FOMO resistance |
| **Cascading Liquidations** | Staircase drops — 3% → pause → 5% → pause → 8% | Multi-level grid fill, progressive hedge adjustment |
| **V-Shaped Recovery** | 12% crash followed by 10% recovery within 2 hours | Whether hedge closes in time to capture the recovery |
| **Slow Bleed** | Price declines 1% per day for 14 days, no sharp moves | Funding rate accumulation, strategy mode transitions, patience |

### Category 2: Volatility Regime Scenarios

Changes in market character that test strategy adaptability.

| Scenario | Description | What It Tests |
|---|---|---|
| **Volatility Expansion** | Quiet market (0.3% daily range) → explosive (5%+ daily range) | Grid spacing adequacy, risk engine response time |
| **Volatility Compression** | High-vol market → dead market (0.1% daily range) | Grid TP reachability, position hold time, funding cost |
| **Whipsaw** | Rapid alternation between +2% and -2% moves every 2-4 candles | False signal frequency, grid deploy/cancel churn |
| **Trend Reversal** | 30-day uptrend → sudden reversal into downtrend | Trend filter response lag, strategy mode transition timing |

### Category 3: Exchange-Specific Scenarios

Conditions unique to perpetual futures and Hyperliquid.

| Scenario | Description | What It Tests |
|---|---|---|
| **Funding Rate Spike** | Funding rate jumps to 0.3%+ per 8 hours for 3 days | Cost of holding positions, P&L erosion on grid holds |
| **Negative Funding Flip** | Funding flips from +0.1% to -0.15% (shorts pay) | Hedge cost inversion, hold-time sensitivity |
| **Liquidity Gap** | Order book thins; fills slip 0.5-2% from limit price | Effective grid spacing, true cost per trade |
| **Exchange Downtime** | No fills for 30-60 minutes during active grid | State recovery, orphaned order handling |

### Category 4: Compound Scenarios

Multiple adversarial conditions combined — the most realistic stress tests.

| Scenario | Description | What It Tests |
|---|---|---|
| **Black Monday** | Flash crash + funding spike + liquidity gap simultaneously | System survival under maximum adversity |
| **FOMC Surprise** | Volatility compression → explosive expansion + trend reversal | LLM event risk detection, mode transition, grid behaviour |
| **Cascade Contagion** | Slow bleed + cascading liquidations + exchange lag | Cumulative risk, daily loss limit effectiveness |
| **Bull Trap** | V-shaped recovery that fails → second leg down deeper | Hedge close timing, re-entry risk, false recovery detection |

---

## Scenario Generation Architecture

### Component: `ScenarioGenerator`

The ScenarioGenerator produces synthetic OHLCV candles that conform to a scenario
definition. It operates in two modes:

#### Mode 1: Parametric Generation

Creates candles from mathematical models with configurable parameters.

```
ScenarioDefinition
├── Name                  (string — e.g., "Flash Crash 15%")
├── Description           (string)
├── BasePrice             (decimal — starting price, e.g., 67000)
├── Duration              (TimeSpan — total scenario length)
├── Timeframe             (string — "15m")
├── Phases[]              (array of scenario phases)
│   ├── PhaseName         (string — e.g., "Pre-crash calm")
│   ├── Duration          (TimeSpan)
│   ├── TrendDirection    (enum: Flat | Up | Down)
│   ├── TrendMagnitude    (decimal — % per hour)
│   ├── Volatility        (decimal — % range per candle)
│   ├── VolumeMultiplier  (decimal — 1.0 = normal)
│   └── FundingRate       (decimal? — override if modelling funding)
├── SlippageModel         (decimal — % slippage per fill)
└── FundingSchedule       (array? — 8-hourly funding rates)
```

**Example: Flash Crash 15% in 10 minutes**

```json
{
  "name": "Flash Crash 15%",
  "basePrice": 67000,
  "duration": "04:00:00",
  "timeframe": "15m",
  "phases": [
    {
      "phaseName": "Pre-crash",
      "duration": "02:00:00",
      "trendDirection": "Flat",
      "trendMagnitude": 0.0,
      "volatility": 0.3
    },
    {
      "phaseName": "Crash",
      "duration": "00:15:00",
      "trendDirection": "Down",
      "trendMagnitude": 60.0,
      "volatility": 5.0,
      "volumeMultiplier": 8.0
    },
    {
      "phaseName": "Partial recovery",
      "duration": "00:45:00",
      "trendDirection": "Up",
      "trendMagnitude": 10.0,
      "volatility": 2.0,
      "volumeMultiplier": 3.0
    },
    {
      "phaseName": "Post-crash settling",
      "duration": "01:00:00",
      "trendDirection": "Flat",
      "trendMagnitude": 0.0,
      "volatility": 1.0
    }
  ]
}
```

**Candle generation algorithm:**

For each candle in a phase:
1. Calculate expected close based on trend direction + magnitude
2. Add random noise scaled by volatility parameter
3. Generate open/high/low/close ensuring internal consistency:
   - Open = previous close (± small gap for high-vol phases)
   - High ≥ max(open, close)
   - Low ≤ min(open, close)
   - Volume = base volume × volumeMultiplier × random(0.7, 1.3)
4. Ensure the crash phase hits the target drop (15% from base)
5. Generate higher timeframe candles (1H, 4H) by aggregating 15m candles

#### Mode 2: LLM-Assisted Generation

The LLM generates scenario definitions from natural language descriptions.

**User input:** *"What would happen if there was a Luna/UST-style death spiral
where price drops 40% over 3 days with periodic dead-cat bounces?"*

**LLM generates:** A `ScenarioDefinition` with appropriate phases, magnitudes,
and volatility parameters modelled on the described event.

**Prompt structure:**

```
You are a market scenario modeller. Given the user's description of a market
event, generate a ScenarioDefinition JSON with realistic phases.

Rules:
- Each phase must have: name, duration, trend direction, magnitude, volatility
- Phases must be sequential and cover the full scenario
- Volatility should be realistic for crypto perpetual futures
- Include volume multipliers (crashes have 5-10x normal volume)
- Price movements must be internally consistent (no teleportation)
- If the user mentions funding rates, include a funding schedule

Do NOT predict real future events. Generate a hypothetical scenario based on
the described conditions.

User description: {userInput}

Output format: ScenarioDefinition JSON
```

**Safety rails:**
- LLM generates the scenario definition (JSON), not the candles themselves
- The parametric generator creates candles from the LLM's definition
- All scenarios are clearly labelled as synthetic/hypothetical
- Scenarios cannot be confused with real market data in the system

#### Mode 3: Historical Amplification

Takes a real historical event and amplifies it to create a worse version.

**Example:** Take the March 2020 BTC crash (53% drop over 2 days) and generate:
- "What if it dropped 70% instead of 53%?"
- "What if the recovery took 2 weeks instead of 1?"
- "What if funding rates stayed at 0.5% through the whole event?"

This mode loads real historical candles, identifies the crash phase, and
regenerates it with amplified parameters while keeping the pre-/post-crash
context realistic.

---

## Stress Test Execution

### Running a Stress Test

```
StressTestRunner
├── Input:
│   ├── ScenarioDefinition      (the adversarial scenario)
│   ├── StrategyConfig          (the strategy to test)
│   ├── InitialCapital          (decimal — starting account balance)
│   └── Options
│       ├── CaptureSnapshots    (bool — for replay debugger, default: true)
│       ├── GenerateExplanations (bool — for NL explanations, default: true)
│       └── CompareToHistorical (bool — run same period on real data for reference)
│
├── Process:
│   1. ScenarioGenerator.Generate(scenarioDefinition) → syntheticCandles[]
│   2. Inject syntheticCandles into HistoricalDataProvider
│   3. Run BacktestRunner with standard pipeline
│   4. Capture StrategyStateSnapshots at each candle
│   5. Calculate stress test metrics
│   6. Generate DecisionExplanations for key moments
│   7. Build StressTestReport
│
└── Output:
    └── StressTestReport
```

### Stress Test Report

The report answers the user's core questions:

```
StressTestReport
├── ReportId              (Guid)
├── ScenarioName          (string)
├── StrategyConfigId      (FK)
├── SurvivalResult        (enum: Survived | PartialLoss | Ruin)
├── Metrics
│   ├── MaxDrawdownPercent    (decimal — worst peak-to-trough)
│   ├── MaxDrawdownDollar     (decimal)
│   ├── FinalPnl              (decimal)
│   ├── TimeToMaxDrawdown     (TimeSpan — how fast the worst hit came)
│   ├── RecoveryTime          (TimeSpan? — time from max DD to breakeven, null if no recovery)
│   ├── MaxExposureReached    (decimal — highest exposure %)
│   ├── MaxLeverageReached    (decimal — highest leverage multiple)
│   ├── DailyLossLimitHit     (bool)
│   ├── DailyLossLimitHitAt   (DateTime?)
│   ├── HedgesOpened          (int)
│   ├── HedgeEffectiveness    (decimal — loss prevented by hedges)
│   ├── SignalsBlocked         (int — signals rejected by risk engine)
│   ├── CooldownsTriggered    (int)
│   ├── FundingCostTotal      (decimal? — if funding modelled)
│   └── SlippageCostTotal     (decimal? — if slippage modelled)
├── KeyMoments[]          (array of critical snapshots with explanations)
│   ├── CandleTime        (DateTime)
│   ├── Event             (string — "Grid fully filled", "Hedge opened", "Daily limit hit")
│   └── Explanation       (string — NL explanation from Decision Explanations)
├── RiskGateAnalysis
│   ├── MaxExposureGate   (string — "Triggered at candle #47, prevented $X additional loss")
│   ├── DailyLossGate     (string — "Hit at -1.92%, prevented 3 pending deployments")
│   ├── LeverageGate      (string — "Never triggered — max leverage reached 3.2x of 5x limit")
│   └── CooldownGate      (string — "Activated 2x, total cooldown time: 60 minutes")
├── Recommendations[]     (array of actionable suggestions)
└── CreatedAtUtc          (DateTime)
```

### Survival Classification

| Result | Criteria | Meaning |
|---|---|---|
| **Survived** | Final P&L > -5% of initial capital AND no single-candle loss > 10% | Strategy handled the scenario within acceptable parameters |
| **Partial Loss** | Final P&L between -5% and -25% of initial capital | Strategy was damaged but not destroyed; risk parameters need tightening |
| **Ruin** | Final P&L > -25% of initial capital OR margin call triggered | Strategy cannot survive this scenario; fundamental changes needed |

Thresholds are configurable per user.

---

## Risk Gate Stress Analysis

The most unique aspect of this feature is its ability to **validate the RiskEngine
parameters themselves** — not just the strategy.

### How It Works

For each stress test, the system runs **three parallel executions**:

1. **With current risk config** — normal strategy + risk engine parameters
2. **With risk gates disabled** — same strategy, no risk engine (shows unmitigated loss)
3. **With tightened risk config** — same strategy, risk gates at 50% of current thresholds

This produces a **Risk Gate Effectiveness Analysis**:

```
┌─────────────────────────────────────────────────────────────────┐
│  Scenario: Flash Crash 15%                                      │
├─────────────────────────┬─────────────┬──────────┬──────────────┤
│  Metric                 │  No Risk    │ Current  │ Tightened    │
│                         │  Engine     │ Config   │ Config       │
├─────────────────────────┼─────────────┼──────────┼──────────────┤
│  Max Drawdown           │  -18.4%     │  -8.2%   │  -4.1%       │
│  Final P&L              │  -$1,840    │  -$820   │  -$410       │
│  Hedges Opened          │  0          │  2       │  2           │
│  Signals Blocked        │  0          │  3       │  7           │
│  Grid Cycles Completed  │  4          │  2       │  1           │
│  Recovery Time          │  Never      │  4.5h    │  2.1h        │
├─────────────────────────┼─────────────┼──────────┼──────────────┤
│  ⚠️ Risk Engine Value   │  Baseline   │  +$1,020 │  +$1,430     │
│     (loss prevented)    │             │  saved   │  saved       │
└─────────────────────────┴─────────────┴──────────┴──────────────┘
```

This tells the user: *"Your risk engine saved you $1,020 in this crash scenario.
Tightening it would have saved an additional $410 but at the cost of blocking
5 more grid deployments during normal conditions."*

### Parameter Boundary Discovery

The stress test can run a parameter sweep to find the **exact point where the
strategy breaks down**:

> "At what crash magnitude does your strategy produce a >10% drawdown?"

The system runs the same scenario at increasing magnitudes (5%, 8%, 10%, 12%, 15%,
20% crash) and plots the drawdown curve:

```
Crash Magnitude vs. Max Drawdown:
  5%  crash → -1.2% drawdown  ✅ Safe
  8%  crash → -3.4% drawdown  ✅ Safe
 10%  crash → -5.8% drawdown  ⚠️ Warning
 12%  crash → -8.2% drawdown  ⚠️ Warning
 15%  crash → -14.1% drawdown ❌ Danger
 20%  crash → -22.8% drawdown ❌ Ruin

→ Your strategy breaks down at approximately 13% crash magnitude.
  The risk engine extends this to ~17% before ruin.
```

---

## Scenario Library

### Pre-Built Scenarios

The system ships with a library of pre-built scenarios based on historical crypto
events (but amplified/stylised for stress testing):

| Category | Scenarios |
|---|---|
| **Flash crashes** | 10% in 5min, 15% in 15min, 20% in 30min, 30% in 1h |
| **Prolonged declines** | 5% per day × 7 days, 2% per day × 30 days |
| **Whipsaws** | ±3% every 30min × 4h, ±5% every 1h × 8h |
| **Volatility shifts** | Low→High (0.2%→4%), High→Low (4%→0.2%) |
| **Funding spikes** | 0.3%/8h × 3 days, 0.5%/8h × 1 day |
| **Compound events** | "Black Monday", "Luna Spiral", "FTX Collapse", "FOMC Surprise" |

Users can modify pre-built scenarios (adjust magnitude, duration, add phases)
or create custom scenarios from scratch.

### Custom Scenario Builder

A form-based UI for building scenarios without writing JSON:

```
┌─────────────────────────────────────────────────────────────────┐
│  New Stress Test Scenario                                       │
├─────────────────────────────────────────────────────────────────┤
│  Name: [My Custom Crash Scenario            ]                   │
│  Base Price: [$67,000]  Duration: [4 hours]                     │
│                                                                 │
│  Phases:                                                        │
│  ┌───┬──────────────┬──────────┬───────┬──────────┬──────────┐  │
│  │ # │ Name         │ Duration │ Trend │ Magnitude│ Volatility│  │
│  ├───┼──────────────┼──────────┼───────┼──────────┼──────────┤  │
│  │ 1 │ Calm         │ 1h 30m   │ Flat  │ 0%/h     │ 0.3%     │  │
│  │ 2 │ Drop         │ 15m      │ Down  │ 40%/h    │ 5.0%     │  │
│  │ 3 │ Dead Cat     │ 30m      │ Up    │ 15%/h    │ 3.0%     │  │
│  │ 4 │ Second Leg   │ 45m      │ Down  │ 20%/h    │ 4.0%     │  │
│  │ 5 │ Settle       │ 1h       │ Flat  │ 0%/h     │ 1.5%     │  │
│  └───┴──────────────┴──────────┴───────┴──────────┴──────────┘  │
│  [+ Add Phase]                                                  │
│                                                                 │
│  OR describe in plain English:                                  │
│  [I want to test a scenario where price drops 12% in 10        ]│
│  [minutes, recovers half, then drops another 8% over an hour   ]│
│  [Generate Scenario from Description]                           │
│                                                                 │
│  [Run Stress Test]  [Save to Library]                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## Data Model

```
StressTestScenario
├── ScenarioId            (Guid)
├── Name                  (string)
├── Description           (string)
├── Category              (enum: MarketShock | VolatilityRegime | ExchangeSpecific | Compound)
├── IsPreBuilt            (bool — system-provided vs user-created)
├── CreatedByUserId       (Guid? — null for pre-built)
├── Definition            (JSON — ScenarioDefinition)
├── Tags[]                (string[] — e.g., "flash-crash", "funding", "high-severity")
└── CreatedAtUtc          (DateTime)

StressTestRun
├── RunId                 (Guid)
├── UserId                (FK → User)
├── ScenarioId            (FK → StressTestScenario)
├── StrategyConfigId      (FK → StrategyConfig)
├── InitialCapital        (decimal)
├── SurvivalResult        (enum: Survived | PartialLoss | Ruin)
├── ReportJson            (JSON — StressTestReport)
├── ReplaySessionId       (FK → ReplaySession — for opening in debugger)
├── SyntheticCandleCount  (int)
├── ExecutionTimeMs       (int)
└── CreatedAtUtc          (DateTime)

StressTestComparison
├── ComparisonId          (Guid)
├── UserId                (FK → User)
├── ScenarioId            (FK → StressTestScenario)
├── Runs[]                (array of RunIds — e.g., normal vs no-risk vs tightened)
├── ComparisonReport      (JSON — side-by-side metrics)
└── CreatedAtUtc          (DateTime)
```

---

## Integration with Other Innovative Features

### With [Replay Debugger](strategy-replay-debugger.md)

Every stress test run is automatically a replay session. Users can:
- Step through the synthetic crash candle-by-candle
- Inspect grid state as levels get filled during the crash
- Watch the hedge trigger and the risk engine block further signals
- Fork a branch at the moment before the crash: *"What if my grid spacing
  was wider? Would fewer levels have filled?"*

### With [Decision Explanations](natural-language-decision-explanations.md)

Key moments in the stress test are automatically explained:

> *"At candle #23 (the 3rd candle of the flash crash), all 4 grid levels filled
> within a single 15-minute candle. Price dropped from $67,000 to $57,000. The
> hedge triggered immediately on the next candle at $56,800 (price closed below
> the breakdown threshold of $66,541). The daily loss limit of 2% was reached at
> candle #25, blocking all further grid deployments. The risk engine's cooldown
> activated for 30 minutes."*

### With [Counterfactual Branching](strategy-replay-debugger.md)

After seeing how the strategy performed in the crash, the user can fork the
timeline at any point and test modifications:
- *"What if the hedge percent was 50% instead of 30%?"*
- *"What if max exposure was 15% instead of 25%?"*
- *"What if the risk engine daily loss limit was 1% instead of 2%?"*

Each fork shows the modified outcome against the same synthetic crash data.

---

## New Components

| Component | Layer | Responsibility |
|---|---|---|
| `ScenarioGenerator` | Application | Generates synthetic OHLCV candles from ScenarioDefinition |
| `ParametricCandleBuilder` | Application | Creates individual candles from phase parameters (trend, volatility, volume) |
| `LlmScenarioInterpreter` | Application | Converts natural-language scenario descriptions into ScenarioDefinition JSON |
| `HistoricalAmplifier` | Application | Takes real candle data and amplifies crash/rally magnitudes |
| `StressTestRunner` | Application | Orchestrates scenario generation → backtest execution → report building |
| `StressTestReportBuilder` | Application | Analyses stress test results and produces StressTestReport |
| `RiskGateAnalyser` | Application | Runs parallel executions (with/without/tightened risk) and compares |
| `ParameterBoundaryFinder` | Application | Runs scenarios at increasing magnitudes to find breakpoint |
| `IScenarioStore` | Infrastructure | Persists scenarios and stress test results |

---

## Implementation Phases

### Phase 1 — Parametric Scenario Generation

**Goal:** Generate realistic synthetic OHLCV candle data from scenario definitions.

- Define `ScenarioDefinition` model with phases
- Implement `ParametricCandleBuilder` — generates OHLCV from trend/volatility params
- Implement `ScenarioGenerator` — orchestrates phase-by-phase candle generation
- Generate 15m candles and aggregate to 1H and 4H
- Validate candle consistency (high ≥ open/close, low ≤ open/close)
- Build 6 pre-built scenarios (one per Market Shock type)
- Unit tests: candle consistency, phase transitions, target price achievement

**Depends on:** None (standalone component)

### Phase 2 — Stress Test Execution

**Goal:** Run strategy against synthetic data and produce reports.

- Implement `StressTestRunner` — inject synthetic candles into `HistoricalDataProvider`
- Connect to existing `BacktestRunner` pipeline (reuse everything)
- Implement `StressTestReportBuilder` — survival classification, metrics, key moments
- Persist results via `IScenarioStore`
- Enable snapshot capture for replay debugger integration
- API: `POST /stress-tests` (run), `GET /stress-tests/{id}` (results)

**Depends on:** Phase 1, Backtesting pipeline (../18-backtesting-architecture.md), Snapshot capture ([Replay Debugger](strategy-replay-debugger.md) Phase 1)

### Phase 3 — Risk Gate Analysis

**Goal:** Compare strategy performance with/without risk engine.

- Implement `RiskGateAnalyser` — runs three parallel executions per scenario
- Calculate risk engine value (loss prevented in dollar terms)
- Implement `ParameterBoundaryFinder` — sweep crash magnitude to find breakpoint
- Generate risk gate effectiveness report
- UI: side-by-side comparison table (no-risk vs current vs tightened)

**Depends on:** Phase 2

### Phase 4 — Scenario Library & Custom Builder

**Goal:** UI for browsing, customising, and creating scenarios.

- Build pre-built scenario library (extend to ~20 scenarios across all categories)
- Implement scenario browser UI with category filters and severity tags
- Implement custom scenario builder (form-based phase editor)
- Allow modification of pre-built scenarios (clone + edit)
- Save custom scenarios to library

**Depends on:** Phase 2

### Phase 5 — LLM-Assisted Generation

**Goal:** Create scenarios from natural-language descriptions.

- Implement `LlmScenarioInterpreter` — prompt LLM → validate output → ScenarioDefinition
- Implement `HistoricalAmplifier` — load real crash data, amplify parameters
- Add "Describe in plain English" input to scenario builder UI
- Validate LLM-generated definitions (all phases must be valid, magnitudes realistic)
- Fallback to manual builder if LLM output fails validation

**Depends on:** Phase 4, LLM integration (doc 17)

### Phase 6 — Replay Debugger Integration

**Goal:** Seamlessly open stress test results in the replay debugger.

- Auto-create ReplaySession for each stress test run
- "Open in Debugger" button on stress test report
- Counterfactual branching within stress test scenarios
- NL explanations for key stress test moments (automatic, not on-demand)

**Depends on:** Phase 2, [Replay Debugger](strategy-replay-debugger.md) Phase 2, [Decision Explanations](natural-language-decision-explanations.md) Phase 1

---

## Competitive Analysis

| Dimension | 3Commas | QuantConnect | Bloomberg PORT | This Feature |
|---|---|---|---|---|
| Historical backtesting | ✅ Basic | ✅ Advanced | ✅ Advanced | ✅ (separate feature) |
| Synthetic scenario generation | ❌ | ❌ | ✅ Limited (manual) | ✅ Parametric + LLM-assisted |
| Flash crash simulation | ❌ | ❌ | ✅ Manual definition | ✅ Pre-built + custom + AI-generated |
| Funding rate stress testing | ❌ | ❌ | N/A | ✅ Built for perp-specific scenarios |
| Risk engine validation | ❌ | ❌ | Partial | ✅ With/without/tightened comparison |
| Breakpoint discovery | ❌ | ❌ | ❌ | ✅ "At what magnitude does your strategy break?" |
| NL scenario descriptions | ❌ | ❌ | ❌ | ✅ "Test a Luna-style death spiral" |
| Integrated with step debugger | ❌ | ❌ | ❌ | ✅ Open results in replay debugger |
| Survival classification | ❌ | ❌ | ❌ | ✅ Survived / Partial Loss / Ruin |
| Pre-built scenario library | ❌ | ❌ | ❌ | ✅ 20+ scenarios across 4 categories |

---

## Marketing Positioning

> *"Don't wait for the next crash to find out if your strategy survives.
> Test it against flash crashes, liquidity gaps, and funding spikes —
> before you risk a cent."*

For risk-conscious traders:
> *"Know your strategy's breaking point. Our stress tester finds the exact
> market condition where your strategy fails — so you can fix it first."*

For competitive differentiation:
> *"3Commas can tell you how your bot performed last month. We can tell you
> how it would perform in the next Black Monday."*
