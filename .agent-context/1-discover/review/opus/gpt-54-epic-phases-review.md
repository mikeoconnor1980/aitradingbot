# Epic Phase Review

**Reviewer:** GPT-5.4  
**Date:** 2026-03-22  
**Scope:** Review of POC, V1, and V2 epic sequencing only

---

## Overall Verdict

The three-phase structure is directionally strong.

The biggest improvement over many early trading-product plans is that it separates:

- proof of deterministic strategy/runtime behaviour
- live-readiness and operational safety
- later product depth and differentiation

That is the right shape.

What I would change is not the broad three-phase model. I would tighten the promotion gates between phases, pull some safety and validation concerns earlier, and reduce ambiguity around whether V1 is an internal live release or a real customer-facing SaaS release.

---

## Primary Findings

### 1. V1 has a source-of-truth problem in the document itself

The V1 epic file currently contains a pasted patch artifact and duplicated lines from the POC exit criteria.

Why this matters:

- roadmap docs need to be clean if they are going to drive PBIs and implementation order
- accidental artifact text in a planning file makes later decomposition less reliable
- it is an early signal that backlog documents need lightweight editorial discipline

This should be cleaned before using the V1 file as a planning source.

### 2. The POC should have a harder go/no-go gate for strategy viability

The POC correctly focuses on deterministic replay, paper trading, and evidence collection.

What is still missing is an explicit statement of what counts as "promising enough" to justify V1.

Why this matters:

- without quantitative thresholds, phase promotion becomes subjective
- trading projects often continue because the architecture is elegant, not because the strategy is good enough
- a deterministic backtester without harsh viability gates can still validate a weak idea

I would add explicit promotion metrics such as:

- acceptable maximum drawdown range
- minimum trade count before judging viability
- minimum profit-factor or expectancy threshold
- slippage, fee, and funding assumptions used in the decision
- required paper-trading burn-in duration before V1

### 3. Exchange-state correctness is still introduced a bit too late

V1 Epic 1 introduces reconciliation, recovery, and idempotency in the right spirit.

I would still pull the design of those mechanisms earlier, even if the full live order path remains in V1.

Why this matters:

- live trading failures usually come from state divergence, not from indicator bugs
- recovery design influences the shape of the order journal, checkpoints, execution events, and runtime state model
- if reconciliation is only detailed after the paper-trading runtime is built, some core interfaces may already be wrong

I would keep live execution in V1, but I would define the reconciliation model during late POC or as a design gate before V1 implementation begins.

### 4. V1 needs a sharper product boundary

The V1 backlog mixes two possible interpretations:

- V1 as a controlled internal or limited live rollout
- V1 as the first real customer-facing SaaS release

Those are materially different products.

Why this matters:

- a restricted internal rollout can defer billing, polished onboarding, and broader admin tooling
- a real paying-customer V1 cannot reasonably defer the minimum viable auth, connection, support, and operational audit surfaces
- unclear release intent causes scope drift and underbuilt operational features

I would choose one of these explicitly:

1. **Internal-live V1**: one exchange, one strategy, restricted users, minimal UI, no meaningful commercial expansion yet.
2. **Customer-live V1**: minimum viable auth, key connection workflow, tenant isolation verification, subscription enforcement, and supportable audit surfaces must move into V1.

### 5. V2 is strong conceptually, but the order inside V2 can be improved

The V2 themes are good: richer history, research UX, replay debugging, explainability, stress testing, and commercial expansion.

I would change the order slightly.

Why this matters:

- replay debugger and explanation quality depend on strong event history and state capture
- adversarial and stress-testing capability improves strategy quality earlier than polished explanation UX
- commercial expansion should happen only after the operating model is supportable for the intended customer type

My preferred V2 order would be:

1. richer history and state capture
2. adversarial and stress-testing tooling
3. strategy research and comparison UX
4. replay debugger
5. decision transparency and explanations
6. commercial expansion

That sequence builds the underlying evidence model before the more presentation-heavy features.

---

## What Looks Strong

### 1. The phase boundaries are mostly correct

POC proves the engine and runtime.

V1 proves safe live operability.

V2 expands into differentiation and product depth.

That is a much better progression than trying to build a full SaaS shell around an unproven trading core.

### 2. The POC is correctly biased toward deterministic behaviour

The inclusion of:

- shared contracts
- historical data
- deterministic backtesting
- GridController and runtime state model
- CandleClock and StrategyScheduler
- paper trading

is exactly the right center of gravity for this project.

### 3. V1 recognizes that live trading is primarily an operations problem

Exchange integration, recovery, kill switches, auditability, and controlled rollout are the right concerns to elevate.

That matches the actual failure modes of trading systems.

### 4. V2 is focused on differentiation rather than premature breadth

Replay debugger, explanation layers, resilience testing, and comparison tooling are much more defensible differentiators than generic dashboard polish.

---

## What I Would Do Differently

### 1. Add explicit phase promotion gates

I would add a short "promotion gate" section at the end of each phase document.

Suggested examples:

**POC -> V1 gate**

- deterministic replay confirmed on repeated runs
- paper mode confirmed once-per-candle under restart scenarios
- viability thresholds met under conservative fee, slippage, and funding assumptions
- order journal and recovery design agreed before live execution work starts

**V1 -> V2 gate**

- startup reconciliation proven in repeated failure drills
- emergency flatten and kill switches tested
- live rollout completed for restricted users without unresolved state divergence
- minimal operator workflow works without manual database intervention

Why:

- the roadmap becomes decision-oriented rather than just scope-oriented
- phase changes stop being emotional or schedule-driven

### 2. Pull validation realism earlier into the POC

The POC already includes fees and slippage assumptions, which is good.

I would go further and make conservative execution modelling part of the POC definition itself.

Specifically:

- funding assumptions
- maker vs taker assumptions where relevant
- partial-fill approximation rules
- sensitivity testing around slippage bands

Why:

- a grid strategy can look good under naive fills and collapse under realistic frictions
- this project should reject weak strategies early rather than operationalizing them elegantly

### 3. Collapse part of V1 into a single safety-governed live-readiness stream

V1 Epics 2, 3, and 5 are all good, but they overlap heavily.

I would consider grouping them under one larger theme operationally:

- live safety controls
- observability
- rollout governance
- operational runbooks

Why:

- these concerns are interdependent and usually need to be designed together
- it reduces the chance of building live enablement toggles before the alerting/runbook path is actually usable

This does not require fewer capabilities. It just makes the dependency chain clearer.

### 4. Be more ruthless about V1 surface area

For V1, I would explicitly constrain the system to:

- one exchange
- one symbol or very small symbol set
- one strategy family
- operator-first API and UI
- minimal customer surface only if external users are truly in scope

Why:

- every extra screen or settings surface increases the chance that product work outruns trading safety work
- the critical learning in V1 is whether the live loop is trustworthy, not whether the product feels complete

### 5. Separate "evidence capture" from "explanations"

The roadmap already implies this, but I would make it explicit.

First capture:

- raw signal events
- state transitions
- order intents
- execution outcomes
- checkpoint/recovery events

Then build:

- replay debugger
- decision summaries
- AI-assisted explanations

Why:

- explanation quality is only as good as the underlying event model
- otherwise you risk generating polished narratives over incomplete truth

---

## Recommended Revised Phase Emphasis

### POC

Keep as-is structurally, but add:

- explicit viability thresholds
- conservative execution assumptions including funding
- recovery and order-journal design as a prerequisite for V1

### V1

Keep the live-capable focus, but sharpen it around:

- exchange truth vs local truth reconciliation
- failure drills and restart drills
- emergency controls
- operator-first workflows
- explicit statement of whether this is internal-live or customer-live

### V2

Keep the differentiation themes, but prioritize:

- richer history and stress tooling before polished explanation surfaces
- commercial expansion only after supportability and operational confidence are real

---

## Bottom Line

Yes, I would do a few things differently, but they are mostly sequencing and governance changes rather than a rewrite of the roadmap.

The core three-phase shape is good.

The main changes I would make are:

1. define hard promotion gates between phases
2. make strategy viability more quantitative in the POC
3. bring reconciliation design forward before live execution work starts
4. decide whether V1 is internal-live or customer-live
5. build V2 on top of captured evidence, not just polished UX

That produces a roadmap that is more likely to reject weak strategy ideas early, survive real exchange behaviour later, and only then expand into a product with stronger differentiation.