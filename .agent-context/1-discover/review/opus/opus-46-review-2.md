# Project Review 2 — AI Grid Trading System

**Reviewer:** Claude Opus 4.6  
**Date:** 2026-03-22  
**Status:** Pre-code (documentation + wireframes)  
**Previous review:** [opus-46-summary.md](opus-46-summary.md) (2026-03-14)

---

## Overall Verdict: Significantly Matured, Ready to Build

The project has evolved substantially since the first review 8 days ago. What was a strong but incomplete documentation set is now a comprehensive, interconnected specification covering architecture, domain model, UI, business model, innovative features, and pre-launch safety. The wireframes add a tangible product dimension that was missing before.

The architecture remains sound. Several gaps identified in the first review have been addressed. New additions — particularly the innovative features trilogy, the complete ERD, the pre-launch checklist, and the wireframes — show a project that is thinking about the full product lifecycle, not just the trading engine.

**The project is ready to start building.** The documentation is now at a point of diminishing returns — further design work without code will not reduce risk. The next insight will come from implementation.

---

## What Changed Since Review 1

### Issues Raised in Review 1 — Status

| # | Issue | Status | Notes |
|---|---|---|---|
| 1 | SQLite locking risk | **Partially addressed** | Still SQLite for POC, but phased upgrade to Azure SQL is documented in 03/10. WAL mode not explicitly mentioned. |
| 2 | Thin strategy edge | **Not addressed** | No breakeven modelling, no fee/funding analysis. Still the biggest open risk. |
| 3 | Funding rate exposure | **Partially addressed** | Backtesting doc (18) lists "funding-aware PnL" as a future enhancement. Adversarial stress testing (innovative features) includes funding rate spike scenarios. Not yet modelled in core backtest. |
| 4 | Over-designed config for v1 | **Acknowledged** | Config schema (13) is detailed but well-structured. The schema sections are sensible. Acceptable if implementation starts with a subset. |
| 5 | Missing order reconciliation | **Not addressed** | Still no dedicated reconciliation loop documented. The pre-launch checklist (22) includes "partial fills handled correctly" and "state recovery after restart verified" as checklist items, but no architectural solution is specified. |
| 6 | Missing kill switch | **Addressed** | Pre-launch checklist (22) explicitly requires "kill switch implemented (global stop trading)". High-level architecture diagram includes "Feature Flags / Kill Switches" component. Still needs a design doc. |
| 7 | Angular UI premature | **Doubled down** | 16 screens have been wireframed. The decision to invest in UI design has been made. See UI assessment below. |
| 8 | Missing logging/observability | **Addressed** | Architecture diagram includes Structured Logging, Metrics/Monitoring, Alerts/Notifications, and Audit Trail as first-class components. Pre-launch checklist requires logging, alerting, and decision logs. |
| 9 | Docker latency | **Not applicable** | Candle-close strategy makes this irrelevant. Correctly deprioritised. |

**Score: 4 of 9 issues addressed or partially addressed. 2 critical gaps remain (strategy edge validation, order reconciliation).**

---

### New Additions Since Review 1

| Addition | Assessment |
|---|---|
| **Complete ERD (data-model-erd.md)** | Excellent. 8 domains, ~20 entities, proper relationships. Covers identity, strategy, trading, grid, market data, AI context, backtesting, and operations. This is the most valuable new document. |
| **Innovative Features (3 docs)** | Strategy Replay Debugger, NL Decision Explanations, Adversarial Stress Testing. Ambitious but well-designed. Each leverages existing architecture rather than requiring new infrastructure. |
| **Pre-launch Checklist (22)** | Thorough. Covers security, trading safety, backtest integrity, infrastructure, legal, and operational readiness. Shows production-mindedness. |
| **Business Model Options (20)** | Good analysis of self-hosted vs platform-hosted vs split architecture. The comparison table is particularly useful. Option C (split architecture) is clever — it leverages the signal contract boundary already designed in doc 16. |
| **Business Model Legal (21)** | Placeholder ("testing"). Needs content before any launch decision. |
| **Wireframes (16 screens)** | Professional. Consistent design system (DDX). Covers full user journey: registration, onboarding, dashboard, strategy management, backtest, positions, orders, signals, settings, admin. |
| **High-level Architecture Diagram** | Clean Mermaid flowchart. All major components represented. Properly shows shared vs per-user boundaries. |
| **Trading Cycle Sequence Diagram** | Detailed sequence diagram showing the full candle-close-to-execution flow including risk approval/rejection paths. |

---

## What's Good (Updated)

### 1. The ERD is production-grade

The data model covers the full product surface. Key design decisions that stand out:

- **StrategyStateSnapshot** entity — serialising full engine state per candle is the foundation for the replay debugger, and it's already in the core data model, not bolted on.
- **CounterfactualBranch** with self-referencing ParentBranchId — supports branching chains for the what-if debugger.
- **RiskEvent** as a first-class entity — every risk decision is auditable. This is a regulatory and trust requirement.
- **Signal → Order linkage** — traceability from strategy intent to exchange execution.
- **StrategyExecutionCheckpoint** — deduplication built into the data model.

The entity relationships are correct and well-normalised. No obvious missing foreign keys or orphan risks.

### 2. The innovative features are genuinely differentiating

The Strategy Replay Debugger, NL Decision Explanations, and Adversarial Stress Testing form a coherent product moat. What makes them credible:

- They reuse existing infrastructure (backtest pipeline, signal contracts, state snapshots).
- They're documented with concrete UI details, data flows, and component architecture.
- They address real user needs (understanding *why* a bot did something, testing resilience before risking capital).
- The replay debugger's keyboard controls, breakpoint types, and state inspector panels are specified to a level that could be implemented directly.

These are v2+ features, but documenting them now is correct — they influence v1 data model decisions (StrategyStateSnapshot, CounterfactualBranch).

### 3. The pre-launch checklist shows operational maturity

9 categories, ~50 checklist items covering security, trading safety, backtest integrity, infrastructure, ops readiness, legal, and user trust. This is the kind of document that prevents "we forgot to disable withdrawal permissions" incidents. It should be treated as a gate before any subscriber touches the system.

### 4. Business model analysis is thorough and honest

Option C (split architecture) is a genuinely novel approach for this market. The insight that the signal contract boundary (doc 16) naturally splits at the right point between cloud and agent is architecturally elegant. The comparison table across 14 dimensions is balanced — no option is presented as the obvious winner.

### 5. The wireframes ground the product

16 screens with consistent design language. Notable:
- Empty state dashboard with guided onboarding steps
- Strategy config with JSON preview and grid visualisation
- Backtest screen with equity curve and trade log
- Admin dashboard with per-user bot status
- Mobile-responsive CSS

This transforms the project from "a trading engine with documentation" to "a product with a trading engine."

---

## What Still Needs Work

### 1. CRITICAL: Strategy edge remains unvalidated

This was the #2 issue in review 1 and it's still the biggest risk. No amount of architecture quality matters if the grid pullback strategy doesn't have positive expected value after fees, funding, and slippage.

**What's needed before writing engine code:**
- Model Hyperliquid maker/taker fees (currently ~0.02%/0.05%)
- Calculate breakeven win-rate for 0.8% TP with 4 grid levels at 0.35% spacing
- Model funding cost for average hold times (grid positions may be held hours to days)
- Back-of-envelope: with 0.8% gross TP and ~0.10% round-trip fees, the net TP is ~0.70%. Average entry from 4 grid levels is ~-0.85% from first fill. This needs price to recover nearly to the first fill level. In trending markets, fine. In chop, every grid cycle where TP isn't reached is a loss compounded by funding.

**Recommendation:** Build the backtester first and run it on 6-12 months of BTC 15m candle data with realistic fees before building any live execution code. This was recommendation #1 in review 1 and remains the most important action item.

### 2. CRITICAL: Order reconciliation still undesigned

The system has no documented mechanism for synchronising local state against exchange state. This matters because:
- A fill can occur on Hyperliquid while the worker is restarting
- An order can be rejected after the signal was marked "Executed"
- Network interruption between order placement and acknowledgement creates ambiguous state
- The split architecture (Option C) adds another reconciliation surface

The pre-launch checklist mentions the requirements ("partial fills handled correctly", "state recovery after restart verified") but there's no design for how these are achieved.

**What's needed:**
- A periodic reconciliation loop that compares local Order/Position state against Hyperliquid's reported state
- Conflict resolution rules (exchange is source of truth)
- Idempotency tokens for order submission
- A state recovery procedure that runs on worker startup

### 3. Business model legal doc is empty

Doc 21 contains only "testing". Given that:
- Option B involves holding user trading keys (regulatory implications)
- Option C involves sending trading signals to users (signal service regulation)
- All options involve a paid subscription for trading automation

This needs actual legal analysis. The pre-launch checklist correctly requires Terms of Service, risk disclaimer, privacy policy, and "platform is software, not financial advice" statement. But the legal model analysis that should inform the business model decision is missing.

### 4. Funding rates not modelled in core backtest

The backtesting architecture (doc 18) lists funding-aware PnL as a "future enhancement." For a BTC perpetuals strategy, funding is not optional — it's a material cost that can easily consume the strategy's edge.

Hyperliquid funding rates can reach 0.1%+ per 8h in trending markets. A grid position held for 24 hours during a high-funding period incurs ~0.3% in funding costs — nearly 40% of the 0.8% gross TP target.

**Recommendation:** Include funding rate modelling in v1 of the backtester, not as a future enhancement.

### 5. The innovative features need sequencing discipline

The replay debugger, NL explanations, and adversarial stress testing are excellent features — but they represent months of development work. There's a risk that they distract from the core priority: proving the strategy has edge and getting it running live.

**Recommended sequencing:**
1. v1: Data model includes StrategyStateSnapshot (enables future replay) but no UI
2. v1: Template-based decision explanations (no LLM, just string formatting from signal data)
3. v2: Replay debugger with step-through and state inspection
4. v2: LLM-enhanced explanations
5. v3: Counterfactual branching
6. v3: Adversarial stress testing

### 6. No CI/CD or testing strategy documented

The pre-launch checklist mentions "CI/CD pipeline configured" and "environment separation" but there's no document describing:
- Unit testing strategy for the strategy engine/risk engine
- Integration testing approach for Hyperliquid connectivity
- How backtest determinism is verified (same input → same output)
- Build and deployment pipeline

### 7. Worker scaling strategy is thin

Doc 10 (ADR 10) says "POC phase: single worker iterates over all active users. Production phase: worker scales horizontally or partitions users across instances." For a system that must execute all users' strategies within the 15m candle close window, this needs more thought:
- If there are 100 active subscribers and strategy evaluation takes 200ms each, that's 20 seconds — fine.
- If there are 1000 subscribers, that's 200 seconds — exceeds the 15-minute window.
- Partitioning by user across workers needs coordination to avoid gaps or duplicates.

This is a production-phase concern and not urgent, but the current doc hand-waves it.

---

## Wireframe Assessment

The wireframes are well-executed. Specific observations:

**Strengths:**
- Consistent design system (DDX palette, spacing, typography)
- 16 screens cover the full user journey end-to-end
- Empty states and onboarding flows are designed (not just the happy path)
- Responsive design with mobile breakpoints
- Dark chart area with proper TradingView-style candle rendering
- Admin dashboard separates system health from user management

**Concerns:**
- The UI scope is still large for a pre-revenue product. 16 screens = significant Angular development.
- No wireframe for the replay debugger (the most architecturally interesting feature)
- No wireframe for the kill switch / emergency controls
- The backtest screen is present but doesn't show the stress testing scenarios

**Recommendation:** For v1, implement 5 screens:
1. Dashboard (with chart, positions, orders)
2. Strategy config (create/edit)
3. Exchange connection
4. Settings
5. Admin status page

Add remaining screens iteratively as features are built.

---

## Updated Recommended Build Order

Given the current state of documentation:

1. **Backtester + historical data** — prove the strategy has edge with realistic fees and funding
2. **Core pipeline** (Strategy → Signal → Risk → SimulatedExecution) — the shared code
3. **Data model + EF Core** — implement the ERD from data-model-erd.md
4. **Hyperliquid integration** — market data + order execution with reconciliation loop
5. **Worker with CandleClock** — live execution with checkpoint deduplication
6. **Kill switch + circuit breaker** — before ANY live trading with real funds
7. **Minimal API** — status, start/stop, emergency flatten
8. **5-screen Angular UI** — dashboard, strategy config, exchange connection, settings, admin
9. **Template-based decision explanations** — string-formatted signal narratives
10. **Replay debugger** — step-through with state inspection (v2)

---

## Document Quality Assessment

| Document | Quality | Notes |
|---|---|---|
| 00 Project Overview | Good | Clear, well-structured |
| 01 Trading Strategy | Good | Covers the flow, could use fee/funding analysis |
| 02 Hyperliquid Integration | Good | Multi-tenant connection model well-defined |
| 03 Infrastructure Architecture | Good | Phased deployment is pragmatic |
| 04 Domain Model | Superseded | ERD (data-model-erd.md) is now the authoritative source |
| 05 Feature Specification | Adequate | High-level; wireframes now carry the detail |
| 06 Project Structure | Minimal | Sufficient for now; will grow with code |
| 07 UI Design | Minimal | Wireframes carry the detail now |
| 08 Development Plan | Too thin | 7 phases with one-line descriptions. Needs expansion or replacement with a proper backlog. |
| 09 Charting Library | Minimal | Adequate — TradingView Lightweight Charts is the right choice |
| 10 Architecture Decisions | Good | 10 ADRs, clear reasoning |
| 11 Angular Instructions | Minimal | Sufficient as guardrails |
| 12 Strategy Customisation | Good | Clear user-facing config model |
| 13 Strategy Config Schema | Excellent | Full JSON schema with validation rules |
| 14 Strategy Runtime Model | Good | Clear per-subscriber execution model |
| 15 Grid Controller | Excellent | Clean separation of lifecycle management from strategy logic |
| 16 Signal Contracts | Good | Typed signals with lifecycle states |
| 17 LLM Context Architecture | Good | Correct boundary (context, not decisions) |
| 18 Backtesting Architecture | Good | Missing funding modelling |
| 19 Scheduling Architecture | Excellent | CandleClock + StrategyScheduler with code examples |
| 20 Business Model Options | Excellent | Thorough Option A/B/C analysis |
| 21 Business Model Legal | Empty | Placeholder only |
| 22 Pre-launch Checklist | Excellent | Production-readiness gate |
| ERD (data-model) | Excellent | Complete, well-normalised, proper relationships |
| Innovative Features (3 docs) | Excellent | Ambitious but achievable, well-connected to core architecture |
| High-level Architecture Diagram | Good | All components represented |
| Trading Cycle Sequence | Good | Full execution flow with branching |
| Wireframes (16 screens) | Very Good | Professional, consistent, responsive |

---

## Risk Summary

| Risk | Severity | Mitigation |
|---|---|---|
| Strategy has no proven edge | **Critical** | Build backtester first, model fees + funding |
| No order reconciliation design | **High** | Design reconciliation loop before live trading |
| Legal model undefined | **High** | Complete doc 21 before business model decision |
| Funding rates not in v1 backtest | **Medium** | Include in v1 backtester |
| Scope creep from innovative features | **Medium** | Strict sequencing — core pipeline before replay/NL/stress |
| UI scope vs development capacity | **Medium** | Start with 5 screens, iterate |
| No CI/CD or testing strategy | **Low** | Document before significant codebase grows |
| Worker scaling at N>100 users | **Low** | Production-phase concern, design later |

---

## Bottom Line

The project has matured from "strong architecture documentation" to "comprehensive product specification." The ERD, wireframes, innovative features, and pre-launch checklist fill the gaps that existed 8 days ago.

The two biggest risks remain unchanged: **unvalidated strategy edge** and **no order reconciliation design**. Everything else is in good shape.

**The single most important next step is building the backtester and running it on real historical data with realistic fees and funding.** If the numbers don't work, the entire architecture — however well-designed — is academic. If they do work, the path to a live system is clear and well-documented.

Stop designing. Start building. Start with the backtester.
