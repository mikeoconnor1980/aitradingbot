# V1 Epics

**Purpose:** Epics for the first live-capable version after the Proof Of Concept is complete.  
**Scope:** Epics only. No PBIs yet.  
**Status:** Proposed  
**Date:** 2026-03-22

---

## Assumption

V1 assumes the Proof Of Concept has already demonstrated:

- deterministic backtesting
- live market-data handling
- CandleClock scheduling
- paper trading
- stable strategy/runtime behaviour worth taking further

V1 is therefore focused on live-readiness rather than first proof.

---

## Epic 1 — Exchange Integration, Reconciliation, and Recovery

Implement the live order path only after paper trading is stable, with recovery and state-correction as first-class concerns.

Includes:

- order placement and cancellation
- client order IDs and idempotency protections
- persisted order journal
- startup reconciliation
- partial-fill handling
- rejection handling
- orphan and stuck-order detection

Outcome:

- the system can recover after restarts, reconnects, and ambiguous exchange outcomes without duplicating risk

---

## Epic 2 — Safety Controls and Operational Observability

Add the controls and visibility needed before any meaningful live rollout.

Includes:

- emergency flatten
- per-user kill switch
- global kill switch
- circuit breaker rules
- structured logging
- audit trail
- health monitoring and alerting

Outcome:

- the platform can detect failures, explain what happened, and stop trading quickly when necessary

---

## Epic 3 — Controlled Live Rollout

Enable live execution behind explicit operational gates and rollout limits.

Includes:

- live mode enablement controls
- restricted rollout policy
- operational runbooks
- subscription and eligibility checks for live trading
- live sign-off criteria

Outcome:

- live trading is introduced as a controlled promotion step, not as the default next action

---

## Epic 4 — Minimal Operator and User Surfaces

Expose only the API and UI surfaces needed to configure, observe, and control the system safely in v1.

Includes:

- strategy configuration endpoints
- backtest endpoints
- status and safety-control endpoints
- essential Angular screens for exchange connection, strategy config, backtest, dashboard status, and admin health

Outcome:

- users and operators can run the system without the UI outrunning the safety model

---

## Epic 5 — Live Operations Readiness

Establish the operating discipline needed to run the first live version safely.

Includes:

- runbooks for restart, incident response, and manual intervention
- sign-off checklist for live enablement
- restricted-user or internal-first rollout approach
- production health review and go-live criteria

Outcome:

- V1 is not just feature-complete, but operationally ready to be used under controlled conditions

---

## Excluded From V1

These areas are intentionally excluded from V1 and moved to V2:

- richer order and position history
- strategy comparison UX
- signals explorer
- replay debugger UI
- advanced decision explanations
- adversarial stress-testing product features
- broader commercial and admin product expansion
- duplicate execution is prevented in normal restart scenarios
- enough evidence exists to judge strategy viability and runtime correctness
- the team can decide whether to proceed to V1 live-readiness work
*** Add File: c:\Projects\Personal\aitradingbot\.agent-context\3-develop\backlog\v2-epics.md
# V2 Epics

**Purpose:** Epics for product expansion after V1 has proven the core live trading loop is safe and usable.  
**Scope:** Epics only. No PBIs yet.  
**Status:** Proposed  
**Date:** 2026-03-22

---

## Assumption

V2 assumes V1 has already delivered:

- a live-capable trading core
- reconciliation and recovery controls
- safety controls and observability
- a minimal but functional operator and user surface

V2 is therefore focused on product depth, research tooling, and differentiation.

---

## Epic 1 — Rich Trading History and Operational UX

Expand the user and operator surfaces beyond the minimal V1 views.

Includes:

- richer order and position history
- improved signals exploration
- better run history and state visibility
- deeper admin operational workflows

Outcome:

- users and operators have a fuller understanding of system behaviour over time

---

## Epic 2 — Strategy Research and Comparison UX

Add product support for comparing strategy versions and reviewing historical performance in more depth.

Includes:

- strategy comparison workflows
- richer version history
- performance comparison views
- stronger research and tuning support

Outcome:

- the product becomes meaningfully better for strategy iteration rather than just operation

---

## Epic 3 — Replay Debugger

Implement the replay-debugger experience built on the state and signal history already captured by the platform.

Includes:

- step-through replay
- state inspection
- signal and transition tracing
- branch or replay-session support where justified

Outcome:

- complex strategy behaviour becomes inspectable and explainable in a way that differentiates the platform

---

## Epic 4 — Decision Transparency and Explanations

Improve user understanding of why the system behaved as it did.

Includes:

- clearer signal explanations
- decision summaries
- operator-facing reasoning trails
- optional AI-assisted explanation layers where appropriate

Outcome:

- users gain trust through better visibility into system decisions rather than opaque automation

---

## Epic 5 — Adversarial and Stress Testing Tooling

Turn the platform into a stronger research and resilience tool, not only a live trading operator.

Includes:

- stress-testing scenarios
- adversarial market-condition replay
- parameter and behaviour resilience analysis
- failure-oriented research workflows

Outcome:

- strategy robustness can be explored before exposing new ideas to live conditions

---

## Epic 6 — Commercial and Product Expansion

Broaden the product beyond the minimum live-capable core.

Includes:

- subscription and billing expansion
- broader onboarding and account workflows
- richer admin and support tooling
- commercial product polish

Outcome:

- the platform matures from a safe trading core into a fuller SaaS product