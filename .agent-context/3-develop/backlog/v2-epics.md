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
