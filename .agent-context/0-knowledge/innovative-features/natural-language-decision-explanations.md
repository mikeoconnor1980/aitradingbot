# Natural-Language Decision Explanations

**Series: Innovative Features (2 of 3)**
See also: [Strategy Replay Debugger](strategy-replay-debugger.md) |
[Adversarial Stress Testing](adversarial-stress-testing.md)

Parent: [0-knowledge](../) | [TOC](README.md)

> *"Your strategy should be able to explain itself."*

---

## Overview

Every candle close produces a decision — deploy a grid, take profit, open a hedge,
do nothing. Today's trading platforms show you *what* the bot did. None explain *why*
in language a human can read.

This feature uses the LLM (already architected as a context provider in doc 17) in a
second role: **post-hoc decision narrator**. Given the full `StrategyStateSnapshot`
captured at any candle ([Replay Debugger](strategy-replay-debugger.md)), the system generates a structured, plain-English
explanation of:

1. **What happened** — which signals fired, which orders executed
2. **Why it happened** — which indicators, thresholds, and context inputs caused it
3. **What almost happened** — decisions that were close to triggering but didn't
4. **What was blocked** — signals the RiskEngine rejected and why

No retail trading bot offers this. Institutional quant shops build internal
"trade rationale" systems, but these are bespoke, inaccessible, and never
user-facing. This makes institutional-grade decision transparency available to
every subscriber.

---

## The Problem This Solves

### For Strategy Builders (the operator / admin)

- "My strategy lost 2% yesterday — was it a bad setup, bad parameters, or
  a market regime the strategy can't handle?"
- "The grid deployed but didn't fill any levels — why not?"
- "The hedge opened when I didn't expect it — what triggered it?"

Without explanations, the answer requires manually cross-referencing candle data,
indicator values, config thresholds, and risk engine state. This is tedious,
error-prone, and impossible for most users.

### For Subscribers

- "I'm paying for this bot — why did it do that?"
- "I want to understand the strategy well enough to trust it with more capital."
- "My friend asked me what the bot does — I can't explain it technically."

Subscribers need confidence, not just results. Natural-language explanations build
trust and reduce churn.

### For Debugging

When paired with the [Strategy Replay Debugger](strategy-replay-debugger.md), explanations at each candle
create a "narrated walkthrough" — the user can step through a session and hear the
bot explain each decision as if presenting to a portfolio manager.

---

## Architecture Position

The DecisionExplainer sits downstream of the snapshot capture pipeline:

```
StrategyEngine + GridController + RiskEngine
  → StrategyStateSnapshot (captured per candle)
    → DecisionExplainer
      → DecisionExplanation (stored, displayed in UI)
```

It reads state. It never writes signals, places orders, or modifies strategy behaviour.

---

## Explanation Types

### Type 1: Signal Explanation

Generated when one or more signals are emitted at a candle close.

**Structure:**

1. **Action taken** — what signal fired and what order was placed
2. **Setup conditions** — which indicators passed their thresholds
3. **Context modifiers** — how LLM sentiment/regime/event-risk influenced the decision
4. **Risk approval** — how the RiskEngine evaluated and approved the signal

**Example: Grid Deployment**

> **Grid deployed at $67,246 with 4 levels.**
>
> The 15-minute candle closed with a 0.62% pullback below the 21-period EMA
> ($67,482 → $67,246). This exceeded the 0.5% pullback threshold in your
> configuration.
>
> The setup was confirmed by:
> - **4H trend:** Bullish — EMA-50 ($67,210) is above EMA-200 ($65,890) and rising
> - **1H bias:** Bullish — RSI(14) at 43.2, above the 40 threshold
> - **VWAP:** Price pulled back to VWAP ($67,350), within the support zone
>
> LLM context rated market sentiment as **neutral-positive** with low event risk.
> Strategy mode remained **Normal** — position sizing at 100%.
>
> Risk engine approved:
> - Exposure: 12.0% (limit: 25.0%) ✅
> - Daily P&L: -0.3% (limit: 2.0%) ✅
> - Leverage: 2.4x (limit: 5.0x) ✅
>
> Grid plan: 4 buy levels at $67,246, $67,011, $66,776, $66,541 with 0.8%
> take-profit target at $67,784.

**Example: Take Profit**

> **Take profit executed at $67,784 (+0.8%).**
>
> Grid Level 1 was filled at $67,246 at 15:45 UTC. The price rallied over the
> next 3 candles. At 16:30 UTC the 15-minute candle high reached $67,801,
> triggering the take-profit sell at $67,784.
>
> Realised P&L: +$107.60 (0.20 BTC × $538 spread)
> Fees: -$2.70 (maker: $1.35 entry + $1.35 exit)
> Net P&L: +$104.90
> Hold time: 45 minutes
>
> Remaining grid levels (L2-L4) were cancelled as the grid lifecycle moved
> to **Closing** → **Closed**.

**Example: Hedge Opened**

> **Hedge short opened at $66,400 (30% of position).**
>
> Price broke below the grid's lowest level ($66,541 — Level 4) and closed
> at $66,400 on the 15-minute candle. This triggered the hedge condition
> defined in your configuration (hedge enabled, 30% of position size).
>
> At this point:
> - Grid lifecycle: **FullyFilled** (all 4 levels filled)
> - Average entry: $66,894
> - Unrealised P&L: -$494 (-0.74%)
> - The hedge short of 0.30 BTC at $66,400 will offset further downside
>
> LLM context had shifted to **bearish** sentiment 2 candles ago, but the
> strategy was already committed to the active grid. The hedge acts as the
> safety mechanism in this scenario.

---

### Type 2: Non-Action Explanation

Generated when the user asks "why didn't the bot do anything?" at a specific candle.
These are often more valuable than action explanations because they demystify inactivity.

**Structure:**

1. **Summary** — no signal was generated
2. **What was evaluated** — which conditions were checked
3. **What failed** — specifically which threshold or gate prevented action
4. **How close it was** — distance from triggering (quantified)

**Example: Setup Almost Triggered**

> **No grid deployed — pullback insufficient.**
>
> The 15-minute candle pulled back 0.38% below EMA-21. Your configuration
> requires a 0.5% pullback to trigger entry. The setup was 0.12% short of
> the threshold.
>
> All other conditions would have passed:
> - 4H trend: Bullish ✅
> - 1H RSI: 47.3 (threshold: 40) ✅
> - Risk engine: Would have approved (exposure at 8%) ✅
>
> **Suggestion:** If you want more frequent entries, consider reducing
> `pullbackPercent` from 0.5% to 0.35%. You can test this with a
> counterfactual fork in the Replay Debugger.

**Example: Risk Engine Blocked**

> **Grid deployment blocked by Risk Engine.**
>
> A valid setup was detected — 15-minute pullback of 0.72% below EMA-21
> with bullish 4H trend and 1H RSI confirmation. However, the Risk Engine
> rejected the `DeployGrid` signal:
>
> - **Daily loss limit reached:** Current daily P&L is -1.92% (limit: 2.0%).
>   The signal would have added exposure, pushing potential daily loss beyond
>   the safety threshold.
> - Cooldown will activate for 30 minutes after the daily limit is hit.
>
> This is a safety mechanism — the strategy correctly identified a valid setup,
> but your risk configuration prevented additional exposure during an
> already-losing session.

**Example: Wrong Regime**

> **No action — bearish macro regime.**
>
> The strategy is currently in **Defensive** mode because:
> - 4H EMA-50 ($67,210) is **below** EMA-200 ($67,450) and declining
> - LLM context: sentiment is "bearish" (confidence: 0.78), macro regime
>   is "RiskOff", event risk is "elevated" (FOMC meeting in 4 hours)
>
> In Defensive mode, new grid deployments are suppressed. The strategy will
> resume Normal mode when the 4H trend turns bullish and LLM event risk
> drops to "low".

---

### Type 3: Session Summary

A narrative summary covering an entire trading session or day.

**Example:**

> **Session Summary: March 19, 2026 (00:00–23:59 UTC)**
>
> **Result:** +$312.40 net P&L across 3 grid cycles
>
> **Morning (00:00–08:00):** BTC traded in a tight range between $67,100 and
> $67,500. No setups detected — 15-minute pullbacks stayed below the 0.5%
> threshold. The strategy correctly waited.
>
> **Midday (08:00–14:00):** Momentum picked up. Grid Cycle 1 deployed at 09:15
> after a 0.68% pullback. L1 and L2 filled within 90 minutes. Take profit hit
> at 10:45 for +$142.30 net. Grid Cycle 2 deployed at 12:30; only L1 filled.
> Take profit at 13:15 for +$98.10 net.
>
> **Afternoon (14:00–20:00):** LLM detected elevated event risk ahead of the
> Fed minutes release at 18:00 UTC. Strategy mode shifted to **Defensive** at
> 14:45. No grids deployed during this window. After the Fed release, volatility
> spiked but sentiment remained bearish.
>
> **Evening (20:00–23:59):** Sentiment recovered. Grid Cycle 3 deployed at 21:00.
> All 4 levels filled by 22:30 as price dipped. Take profit hit at 23:15 for
> +$72.00 net. One hedge was opened at 22:15 when price broke L4, adding -$18.50
> to the cycle but protecting from further downside.
>
> **Risk metrics:** Max exposure reached 18.2% (limit: 25%). Daily P&L never
> exceeded -0.8% loss. Strategy mode was Defensive for 5h15m (22% of session).
>
> **LLM influence:** The Defensive mode prevented 2 grid deployments between
> 15:00 and 17:30. Post-hoc analysis: one would have been profitable (+$85 est.),
> one would have hit the hedge (-$120 est.). Net impact of LLM caution: +$35
> saved.

---

## Explanation Engine Architecture

### Component: `DecisionExplainer`

```
Interface:
  IDecisionExplainer
    ├── ExplainSignal(snapshot, signal) → SignalExplanation
    ├── ExplainNonAction(snapshot) → NonActionExplanation
    ├── ExplainSession(snapshots[]) → SessionSummary
    └── ExplainComparison(branchA, branchB) → ComparisonNarrative
```

### Two-Tier Explanation Strategy

#### Tier 1: Template-Based (Default, Zero Latency)

Structured explanations generated deterministically from snapshot data using
parameterised templates. No LLM call required.

**How it works:**

1. Read the `StrategyStateSnapshot` at the target candle
2. Identify which signals were emitted (or determine it's a non-action candle)
3. Select the appropriate template (DeployGrid, TakeProfit, Hedge, NonAction, etc.)
4. Fill template slots with snapshot values (indicator values, thresholds, risk state)
5. Apply conditional sections (e.g., include LLM context section only if LLM was active)

**Example template (DeployGrid):**

```
Grid deployed at {gridPlan.entryPrice} with {gridPlan.levels} levels.

The {timeframe} candle closed with a {pullbackPercent}% pullback below the
{emaFast}-period EMA ({emaFastValue} → {candleClose}). This
{exceeded|fell short of} the {configPullbackThreshold}% pullback threshold.

{IF trendBullish}
The setup was confirmed by:
- 4H trend: Bullish — EMA-{configEmaSlow} ({emaSlowValue}) above
  EMA-{configEmaTrend} ({emaTrendValue}) and {rising|falling}
{ENDIF}

{IF rsiConfirmed}
- 1H bias: Bullish — RSI({configRsiLength}) at {rsiValue}, above the
  {configRsiThreshold} threshold
{ENDIF}

{IF llmActive}
LLM context rated sentiment as {llmSentiment} with {llmEventRisk} event risk.
Strategy mode: {strategyMode} — position sizing at {sizingPercent}%.
{ENDIF}

Risk engine {approved|rejected}:
- Exposure: {exposurePercent}% (limit: {maxExposure}%) {✅|❌}
- Daily P&L: {dailyPnlPercent}% (limit: {dailyLossLimit}%) {✅|❌}
- Leverage: {leverage}x (limit: {maxLeverage}x) {✅|❌}
```

**Advantages:** Instant, deterministic, no API cost, works offline, testable.

**Limitations:** Formulaic tone; cannot synthesise novel insights across multiple
candles; cannot identify patterns the template designer didn't anticipate.

#### Tier 2: LLM-Enhanced (Optional, Richer Narratives)

Uses the project's LLM integration (doc 17) to generate fluid, contextual
narratives when the user requests deeper insight.

**How it works:**

1. Tier 1 template generates the structured explanation
2. The structured explanation + raw snapshot JSON is sent to the LLM as a prompt
3. The LLM rewrites/enhances the explanation with:
   - More natural phrasing
   - Cross-candle pattern recognition ("this is the 3rd consecutive failed setup")
   - Contextual commentary ("this pullback coincided with the London session open")
   - Actionable suggestions ("consider widening grid spacing during high-volatility
     sessions")
4. The enhanced explanation replaces or supplements the template version

**Prompt structure:**

```
You are a trading strategy analyst. Given the following strategy state snapshot
and template-generated explanation, produce a clear, concise narrative that:

1. Explains the decision in plain English
2. Notes any patterns across recent candles (context window provided)
3. Highlights anything unusual or noteworthy
4. Suggests parameter adjustments if relevant (framed as questions, not directives)

Do NOT recommend specific trades. Do NOT predict future price movement.
Focus on explaining what the strategy did and why, given its configuration.

=== Snapshot Data ===
{snapshotJson}

=== Recent Context (last 10 snapshots) ===
{recentSnapshotsJson}

=== Template Explanation ===
{tier1Explanation}
```

**Safety rails:**
- LLM output is post-processed to strip any content that resembles trade
  recommendations or price predictions
- Output is validated against the snapshot data — claims must be verifiable
  from the state (no hallucinated indicator values)
- If LLM is unavailable, Tier 1 explanation is shown with a note

**Advantages:** Richer, more conversational, can detect patterns templates miss.

**Limitations:** Latency (1-3 seconds), API cost, needs validation layer.

### Explanation Data Model

```
DecisionExplanation
├── ExplanationId         (Guid)
├── SnapshotId            (FK → StrategyStateSnapshot)
├── ExplanationType       (enum: Signal | NonAction | Session | Comparison)
├── Tier                  (enum: Template | LlmEnhanced)
├── SignalType            (string? — e.g., "DeployGrid", "TakeProfit")
├── StructuredData        (JSON — key-value pairs extracted from snapshot)
│   ├── action            (string — what happened)
│   ├── conditions[]      (array — each threshold check with pass/fail)
│   ├── riskChecks[]      (array — each risk gate with pass/fail)
│   ├── llmInfluence      (string? — how LLM context affected the decision)
│   └── nearMisses[]      (array — conditions that almost triggered)
├── TemplateText          (string — Tier 1 explanation)
├── EnhancedText          (string? — Tier 2 LLM explanation, null if not requested)
├── CreatedAtUtc          (DateTime)
└── GenerationTimeMs      (int — how long explanation took to generate)
```

### Near-Miss Detection

One of the most valuable explanation types is "what almost happened." The system
quantifies how close each non-triggered condition was:

```
NearMiss
├── Condition             (string — e.g., "pullbackPercent")
├── CurrentValue          (decimal — e.g., 0.38)
├── ThresholdValue        (decimal — e.g., 0.50)
├── Distance              (decimal — e.g., 0.12)
├── DistancePercent       (decimal — e.g., 24.0%)
├── WouldHaveTriggered    (bool — if this condition alone had passed)
└── Direction             (string — "below threshold" | "above threshold")
```

Near-misses within 25% of the threshold are highlighted in the UI as "close calls."
This helps users understand whether their parameters are too tight or too loose.

---

## UI Integration

### Inline Explanations (Replay Debugger)

In the [Replay Debugger](strategy-replay-debugger.md), each candle has an **Explain** button in the
signals panel. Clicking it generates a Tier 1 explanation instantly, with a
"Enhance with AI" toggle for Tier 2.

### Explanation Feed (Dashboard)

On the main dashboard, a scrollable feed shows explanations for recent signals
and significant non-actions. Each card shows:

```
┌─────────────────────────────────────────────────────────────┐
│  🟢 Grid Deployed                        15:45 UTC  Mar 19 │
│                                                             │
│  Grid deployed at $67,246 with 4 levels after a 0.62%       │
│  pullback below EMA-21. 4H trend bullish, 1H RSI confirmed  │
│  at 43.2. Risk engine approved (12% exposure).               │
│                                                             │
│  [Read More]  [Open in Debugger]  [Enhance with AI]         │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  ⚪ No Action (close call)                16:00 UTC  Mar 19 │
│                                                             │
│  Pullback reached 0.38% — just 0.12% short of the 0.5%     │
│  threshold. All other conditions would have passed.          │
│                                                             │
│  [Read More]  [Open in Debugger]                            │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  🔴 Signal Blocked by Risk Engine         18:30 UTC  Mar 19 │
│                                                             │
│  Valid setup detected but blocked — daily loss at 1.92%     │
│  (limit: 2.0%). 30-minute cooldown activated.                │
│                                                             │
│  [Read More]  [Open in Debugger]                            │
└─────────────────────────────────────────────────────────────┘
```

### Daily Digest Notification

An optional daily email/Telegram message with the Session Summary (Type 3):

> **Daily Strategy Report — March 19, 2026**
>
> Result: +$312.40 across 3 grid cycles.
> Strategy mode was Defensive for 22% of the session (Fed minutes risk).
> LLM caution saved an estimated +$35 net.
> Max exposure: 18.2%. Max drawdown: -0.8%.
>
> [View full report in dashboard]

---

## Integration with Other Innovative Features

### With [Replay Debugger](strategy-replay-debugger.md)

- The "Explain" button in the replay debugger calls `DecisionExplainer.ExplainSignal()`
  or `DecisionExplainer.ExplainNonAction()` for the current snapshot
- When comparing branches, `DecisionExplainer.ExplainComparison()` narrates the
  differences: "Branch B avoided the drawdown at candle #52 because the wider grid
  spacing meant L3 and L4 weren't deployed until..."

### With [Adversarial Stress Testing](adversarial-stress-testing.md)

- After a stress test scenario completes, the session summary explains how the
  strategy behaved under extreme conditions
- Explanations highlight which risk gates activated and why
- Example: "During the simulated flash crash, the hedge opened 2 candles after
  the breakdown. Grid levels L3 and L4 filled during the drop but the hedge
  limited total loss to -$180 vs. -$520 without hedge."

---

## Implementation Phases

### Phase 1 — Template Engine

**Goal:** Generate structured Tier 1 explanations for all signal types.

- Define explanation templates for each signal type (DeployGrid, CancelGrid,
  TakeProfit, OpenHedge, AdjustHedge, CloseHedge, FlattenPosition, Cooldown)
- Define non-action explanation template with near-miss detection
- Implement `TemplateDecisionExplainer : IDecisionExplainer`
- Implement `NearMissDetector` — compares all conditions against thresholds
- Unit tests for every template with sample snapshot data
- Structured data extraction from snapshots

**Depends on:** StrategyStateSnapshot ([Replay Debugger](strategy-replay-debugger.md), Phase 1)

### Phase 2 — UI Integration

**Goal:** Display explanations in the replay debugger and dashboard.

- Add "Explain" button to replay debugger signals panel
- Build explanation card component (Angular)
- Add explanation feed to dashboard
- Style near-miss highlights (amber "close call" badges)
- "Open in Debugger" deep links from explanation cards

**Depends on:** Phase 1, [Replay Debugger](strategy-replay-debugger.md) Phase 2

### Phase 3 — LLM Enhancement

**Goal:** Optional Tier 2 explanations with richer narratives.

- Implement `LlmDecisionExplainer : IDecisionExplainer` (wraps Tier 1 + LLM call)
- Design prompt template with safety rails
- Implement output validation (no hallucinated values, no trade recommendations)
- Add "Enhance with AI" toggle in UI
- Implement caching for LLM explanations (same snapshot → same explanation)
- Fallback to Tier 1 if LLM is unavailable

**Depends on:** Phase 1, LLM integration (doc 17)

### Phase 4 — Session Summaries & Notifications

**Goal:** End-of-day narrative summaries and optional push notifications.

- Implement `SessionSummaryBuilder` — aggregates snapshots into session narrative
- Segment session into time periods (morning/midday/afternoon/evening)
- Calculate LLM influence metrics (how many signals were suppressed, estimated
  P&L impact of caution)
- Implement daily digest notification (email and/or Telegram)
- Store session summaries for historical review

**Depends on:** Phase 1, alerting infrastructure

### Phase 5 — Comparison Narratives

**Goal:** Explain the difference between counterfactual branches in plain English.

- Implement `ComparisonNarrativeBuilder` — takes two branches, identifies
  divergence points, and narrates what changed and why
- Highlight the specific candle(s) where the config change made a material
  difference
- Quantify the impact: "Wider grid spacing would have saved $176 by avoiding
  the L3 fill at candle #52"
- UI: comparison narrative panel in branch comparison view

**Depends on:** Phase 1, Counterfactual Branching (doc 22, Phase 4)

---

## Competitive Analysis

| Dimension | 3Commas | QuantConnect | Cryptohopper | This Feature |
|---|---|---|---|---|
| Trade explanations | ❌ None | ❌ None | ❌ None | ✅ Per-signal natural language |
| Non-action explanations | ❌ | ❌ | ❌ | ✅ "Why nothing happened" with near-miss |
| Risk rejection explanations | ❌ | ❌ | ❌ | ✅ Which gate blocked, by how much |
| Session summaries | Basic stats | Detailed metrics | Basic stats | ✅ Narrative with LLM influence analysis |
| LLM-enhanced narratives | ❌ | ❌ | ❌ | ✅ Optional AI-powered deeper insight |
| Actionable suggestions | ❌ | ❌ | ❌ | ✅ Parameter adjustment recommendations |
| Daily digest notifications | ❌ | ❌ | Basic alerts | ✅ Full narrative digest |

---

## Marketing Positioning

> *"Your strategy explains every decision — and every non-decision — in plain English.
> No more guessing why the bot did or didn't trade."*

For subscriber trust:
> *"Read exactly why your bot traded, in language you understand. Every signal,
> every rejection, every 'close call' — explained and logged."*

For competitive differentiation:
> *"3Commas shows you a P&L number. We show you the reasoning behind every cent."*
