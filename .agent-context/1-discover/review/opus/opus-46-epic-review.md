# Epic Phasing Review — POC / V1 / V2

**Reviewer:** Claude Opus 4.6  
**Date:** 2026-03-22  
**Scope:** [proof-of-concept-epics.md](../../../3-develop/backlog/proof-of-concept-epics.md) | [v1-epics.md](../../../3-develop/backlog/v1-epics.md) | [v2-epics.md](../../../3-develop/backlog/v2-epics.md)  
**Cross-referenced:** All 22 knowledge docs, pre-launch checklist, previous Opus reviews

---

## Overall Assessment

The three-phase structure is solid. The progression from "prove the strategy" (POC) → "make it safe to run live" (V1) → "make it a product" (V2) is the right shape. The epics within each phase are well-scoped at the epic level and correctly avoid premature detail.

That said, there are gaps — some structural, some about missing acceptance gates, and one genuine sequencing problem. Below is what I'd do differently and why.

---

## Phase Readiness

| Phase | Score | Verdict |
|---|---|---|
| **POC** | 80% | GO — with two exit-gate additions |
| **V1** | 65% | CONDITIONAL — Epic 1 is under-scoped; preamble missing key constraints |
| **V2** | 85% | GO — contingent on V1 data capture |

---

## POC — What I'd Change

### 1. Epic 3 (Backtester) needs a hard profitability gate

The POC exit criteria say "enough evidence exists to judge strategy viability" — but they don't define what evidence. This is the most important gate in the entire project. Both prior Opus reviews flagged this as the #1 risk.

**I'd add explicit exit criteria:**
- Strategy must demonstrate net profitability over 6–12 months of BTC 15m data
- Costs must include Hyperliquid fees (0.02% maker / 0.05% taker) AND funding rates (up to 0.1% per 8h)
- Breakeven win-rate must be calculated and documented
- If strategy is not profitable with realistic costs, the project stops here

**Why:** Funding rates alone can destroy a grid strategy on perps. Deferring funding modelling to "future enhancement" means the POC could pass on unrealistic numbers and the team builds V1 on a false positive. This is the single most expensive mistake the project can make.

### 2. Epic 5 (Paper Trading) needs checkpoint recovery testing

CandleClock and StrategyExecutionCheckpoint are covered, but there's no requirement to test recovery after crash. The POC exit criteria mention "duplicate execution is prevented in normal restart scenarios" but the epic doesn't include the test.

**I'd add:**
- Acceptance test: kill worker mid-candle, restart, verify no duplicate signals or orders
- Acceptance test: restart after missed candle, verify system catches up correctly

**Why:** If this isn't proven in the POC, the same uncertainty carries into V1 where real money is at stake. Recovery testing is cheap in paper mode and expensive to retrofit later.

### 3. Epic ordering should be clarified

Epic 4 (Grid Strategy) logically depends on Epic 1 (Foundation) and Epic 2 (Historical Data), and Epic 3 (Backtester) depends on Epic 4. The current numbering implies this, but the document doesn't state dependencies. Epic 5 depends on Epics 1, 4, and the Hyperliquid market-data piece.

**I'd add a dependency note:**
```
Epic 1 → Epic 2 → Epic 4 → Epic 3 → Epic 5 → Epic 6
              (Foundation → Data → Strategy → Backtest → Paper → Evidence)
```

**Why:** When this gets decomposed into PBIs, the team needs to know what can be parallelised and what can't.

---

## V1 — What I'd Change

### 4. Epic 1 (Exchange Integration) is doing too much — and not enough

This is simultaneously the most critical and most under-specified epic. It covers order placement, reconciliation, recovery, partial fills, and orphan detection — each of these is a significant design challenge. But the description doesn't break out the reconciliation problem at all.

**I'd split Epic 1 into two epics:**

**Epic 1A — Live Order Path:**
- Order placement and cancellation via Hyperliquid API
- Client order ID strategy for idempotency
- Persisted order journal (write-before-send)
- Rejection and partial-fill handling
- Per-user WebSocket streams for fills and position updates
- Order submission rate limiting (queue + throttle)

**Epic 1B — Reconciliation and Recovery:**
- Startup reconciliation: compare local state vs exchange state
- Conflict resolution rules (exchange is source of truth)
- Mid-grid crash recovery: reconstruct GridController state from journal + exchange
- Stuck-order detection with configurable timeout
- Orphan position detection and alerting
- Chaos test: kill worker mid-grid, restart, verify state reconstructed without duplicates

**Why:** Reconciliation is the hardest problem in live trading. Burying it as a bullet point inside a larger epic means it won't get the design attention it needs. A separate epic forces a design phase before coding starts.

### 5. Preamble needs explicit architectural constraints

The V1 preamble states what was proven in POC but doesn't state the architectural context V1 operates within:

**I'd add to the V1 preamble:**
- "V1 is multi-tenant; all entities scoped by UserId (ADR 6)"
- "V1 assumes Option B (platform-hosted) with API keys encrypted in database"
- "V1 must pass the pre-launch checklist before any live trading is enabled"
- "V1 targets VPS deployment (Docker Compose); Azure migration is V2+"

**Why:** Without these, epics can be interpreted differently. Multi-tenancy especially — if it's not stated, epics can be scoped for single-user, and retrofitting tenant isolation is expensive.

### 6. Epic 2 (Safety Controls) needs specifics on circuit breaker rules

"Circuit breaker rules" is listed but the triggering conditions aren't enumerated. This matters because circuit breakers that are too aggressive kill legitimate strategies, and ones that are too loose don't protect.

**I'd add:**
- Circuit breaker triggers: N consecutive losses within M minutes, position size exceeding X% of account, daily P&L drawdown exceeding Y%
- Per-user vs global scope for each trigger
- Cooldown policy after trigger (how long before strategy resumes?)
- All rejected signals logged with reason code

**Why:** Without enumerated rules, the implementation will be guesswork. The rules should be configurable, but the default set needs to be defined at the epic level.

### 7. Epic 4 (Minimal UI) is missing authentication

The epic lists API endpoints and Angular screens but doesn't mention per-user authentication or authorisation. In a multi-tenant system, this is foundational.

**I'd add:**
- User authentication (JWT or equivalent)
- API authorisation (tenant-scoped; user can only access own data)
- Exchange connection workflow (encrypted key storage, wallet-based auth delegation)
- Admin vs user role separation

**Why:** Without auth, there's no multi-tenancy. This should be in Epic 4 (or a dedicated epic before it), not assumed.

### 8. Add a cross-cutting requirement: V1 data capture for V2 features

V2 Epics 3–5 (Replay Debugger, Decision Explanations, Adversarial Stress Testing) all depend on rich historical data that V1 must capture. But no V1 epic explicitly requires:
- StrategyStateSnapshot persisted on each candle close
- Full signal lifecycle tracked: Generated → Validated → Approved → Executed → Filled
- Grid state transitions persisted with before/after state

**I'd add this as a cross-cutting note in V1:**

> "Data capture: V1 must persist StrategyStateSnapshot, signal lifecycle, and grid state transitions on every candle close. These are not displayed in any V1 surface but are required by V2 features."

**Why:** If this isn't explicit, it will be optimised away during V1 implementation ("we don't need this for any V1 screen"), and V2 will have to backfill or redesign.

### 9. Paper trading burn-in should be an explicit gate in Epic 3

Epic 3 (Controlled Live Rollout) mentions "operational gates" but doesn't specify the paper trading requirement.

**I'd add:**
- 7–14 days of stable paper trading operation required before live enablement
- Stability defined as: no crashes, no duplicate signals, reconciliation loop clean, all scheduled candles processed

**Why:** The pre-launch checklist (doc 22) already requires this. The epic should reference it explicitly so it's not skippable.

---

## V2 — What I'd Change

### 10. Preamble should declare V1 data dependencies

V2 Epics 3–5 are well-structured but assume data exists that V1 must have captured.

**I'd add to the V2 preamble:**
- "V2 Epics 3–5 depend on V1 persisting StrategyStateSnapshot and signal lifecycle data"
- "If V1 did not persist this data, these epics require a data-capture backfill epic first"

**Why:** Makes the dependency chain explicit. If V1 is delivered without state snapshots, V2 teams know immediately.

### 11. Epic 6 (Commercial Expansion) is vague

"Subscription and billing expansion" and "broader onboarding" are placeholder-level descriptions. Every other V2 epic has a clear product shape; this one doesn't.

**I'd either:**
- Give it more specificity: which billing model (Option A/B/C from doc 20)? What onboarding flows? What admin workflows?
- Or: defer it entirely. If the billing model isn't decided, don't pretend it's scoped.

**Why:** Vague epics create the illusion of planning without providing actual direction. Better to be explicit about what's undecided.

---

## Structural Issues

### 12. V1 file has a copy-paste artifact

The "Excluded From V1" section in [v1-epics.md](../../../3-develop/backlog/v1-epics.md) ends with three lines that belong to the POC exit criteria, plus a file-system marker:

```
- duplicate execution is prevented in normal restart scenarios
- enough evidence exists to judge strategy viability and runtime correctness
- the team can decide whether to proceed to V1 live-readiness work
*** Add File: c:\Projects\Personal\aitradingbot\.agent-context\3-develop\backlog\v2-epics.md
```

These should be removed.

### 13. No infrastructure epic in any phase

Docker Compose setup, CI/CD pipeline, deployment configuration, database migration strategy, secrets management — none of these appear in any epic. They're not product features, but they're work that needs to happen.

**Options:**
- Add a "Development Infrastructure" epic to POC (repo setup, CI, Docker Compose, SQLite config)
- Add a "Production Infrastructure" epic to V1 (secrets management, monitoring, deployment pipeline)
- Or: treat these as "Epic 0" cross-cutting concerns that don't need epic-level tracking

**My preference:** Add them. They're significant enough to warrant tracking, and "it'll just happen" is how projects end up without CI/CD three months in.

### 14. No testing strategy anywhere

Unit tests, integration tests, end-to-end tests, determinism verification — none are mentioned. The backtesting architecture doc describes a sophisticated replay engine, but the epics don't include "the tests that prove the system works."

**I'd add acceptance criteria to each epic that require:**
- Unit tests for domain logic (signal generation, risk checks, grid state transitions)
- Integration tests for exchange API (mocked responses for rate limiting, rejections, partial fills)
- Determinism test: same historical input produces identical output across runs

**Why:** Without this, the epics will be marked "done" without proven correctness.

---

## What's Done Well

To be clear — the structure is fundamentally right:

- **POC is correctly minimal.** It answers the right questions (determinism, runtime, strategy viability) without building product surface.
- **V1 prioritises safety over features.** Live rollout behind operational gates, with kill switches and observability before broader access.
- **V2 focuses on differentiation.** The replay debugger, decision explanations, and stress testing are genuinely novel for this market segment.
- **Exclusions are explicit.** Each phase states what it doesn't include, which prevents scope creep.
- **Outcomes are well-written.** Every epic has a clear "this is done when" framing.

---

## Summary of Recommendations

| # | Change | Phase | Priority |
|---|---|---|---|
| 1 | Hard profitability gate on backtester (incl. funding rates) | POC | **Critical** |
| 2 | Checkpoint recovery testing | POC | High |
| 3 | Epic dependency chain | POC | Medium |
| 4 | Split Epic 1 into Order Path + Reconciliation | V1 | **Critical** |
| 5 | Add architectural constraints to preamble | V1 | High |
| 6 | Enumerate circuit breaker rules | V1 | High |
| 7 | Add authentication to Epic 4 | V1 | High |
| 8 | Explicit V1 data capture for V2 features | V1 | High |
| 9 | Paper trading burn-in gate | V1 | High |
| 10 | Declare V1 data dependencies in V2 preamble | V2 | Medium |
| 11 | Give Epic 6 specificity or defer | V2 | Medium |
| 12 | Fix copy-paste artifact in V1 file | V1 | Low |
| 13 | Add infrastructure epics | All | Medium |
| 14 | Add testing acceptance criteria | All | High |

---

## One-Line Summary

> Right shape, right sequencing — but the POC needs a harder stop/go gate on strategy profitability, and V1 Epic 1 needs to be split so reconciliation gets the design attention it deserves.
