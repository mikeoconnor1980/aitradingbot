# Phase 6 — Strategy Explainability

TradePilot records normalized `StrategyEvaluation` and `RuleEvaluation` rows from the actual strategy scheduler path. The Analyst reads these facts through Application queries; it does not reconstruct historical strategy decisions from current candles or market analysis.

## Decision and rejection semantics

- The final decision reflects the existing runtime result: no signal (`NoTrade` or `Hold`), approved entry (`EnterLong`/`EnterShort`), exit, or `RejectedByRisk`.
- The primary rejection reason is the first failed blocking rule in recorded evaluation order.
- All failed blocking rules are retained. Summary aggregation counts each retained failed blocking rule.
- Signal-mode entry conditions retain the engine's existing full-evaluation semantics.
- Trend, grid, and DCA gates retain their existing short-circuit semantics. Rules after a blocking short circuit are absent rather than fabricated, and `EvaluationShortCircuited` is true.
- Informational failures do not become primary rejection reasons and are not included in blocking-rule frequency counts.

## Identity and historical context

Live evaluations retain the existing strategy GUID and integer strategy version sent to the Worker. A SHA-256 configuration identity is also stored so evaluations remain distinguishable when an identity is unavailable or configuration provenance needs verification. Context is intentionally compact: symbol, timeframe, trigger-candle timestamp, reference price, and regime. Raw candle histories are not duplicated.

## Volume and retention

Each trigger-candle evaluation creates one evaluation row and a small number of normalized rule rows. Common history queries are indexed by strategy/symbol/time and rule summaries by stable rule ID. Summary queries aggregate in the database and do not load evaluation histories into memory.

No archival or deletion policy is introduced in Phase 6. Production operators should monitor row growth by trigger frequency and enabled-strategy count before choosing a retention window. Any later retention job must preserve evaluations referenced by trades or journal entries.
