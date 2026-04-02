# PBI Specification: F9 — Natural Language Strategy Interpreter

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Reference:** [17-llm-context-sentiment-architecture.md](../../0-knowledge/17-llm-context-sentiment-architecture.md)
**Implementation Phase:** 2 (Natural Language Authoring)
**Risk Level:** High
**Depends On:** F1 (Extensible Strategy Schema), F5 (Condition Evaluator)

---

## Summary

Enable traders to describe strategies in plain English. An LLM interprets the text and maps it directly to the canonical `StrategyConfig` schema — no AST intermediate layer.

The interpreter produces a `StrategyIntentDto` containing the mapped config, a confidence score, and a list of assumptions. The user reviews and edits before saving (edit flow in F10).

### User Story

> As a **trader**, I want to **describe my strategy in plain text** so that **the system can generate a valid strategy configuration I can review and adjust**.

---

## Requirements

### Functional Requirements

#### Interpreter Service

- [ ] `IStrategyInterpreter` service with `InterpretAsync(string userText, CancellationToken)` returning `StrategyIntentDto`
- [ ] `StrategyIntentDto` contains: `StrategyConfig config`, `decimal confidence`, `List<Assumption> assumptions`, `string? clarificationNeeded`
- [ ] Maps directly to canonical `StrategyConfig` — no AST intermediate representation
- [ ] Uses LLM integration from existing `17-llm-context-sentiment-architecture.md` infrastructure
- [ ] Prompt engineering: system prompt includes the schema definition, valid condition types, operator enums, and example configs
- [ ] Confidence calculation: based on how many fields the LLM populated vs. how many were ambiguous

#### Interpretation Rules

- [ ] Only generates conditions for **registered handler types** (RSI, Price vs EMA, MACD, Grid)
- [ ] If the described strategy references unsupported indicators, sets `clarificationNeeded` with explanation
- [ ] Recognises `strategyMode` from context: "grid" keywords (grid, levels, spacing) → grid mode; signal keywords (when, cross, above, condition) → signal mode
- [ ] Defaults to reasonable values where unspecified (e.g. RSI period 14, TP 2%, SL 1.5%) and records each default as an `Assumption`

#### API Endpoint

- [ ] `POST /api/strategies/interpret` — accepts `{ text: string }`, returns `StrategyIntentDto`
- [ ] Rate limited: max 10 requests per minute per user
- [ ] Input sanitized — no prompt injection via user text reaching LLM (instruction hierarchy, input/output separation)
- [ ] Maximum input length: 500 characters

#### Non-Functional

- [ ] LLM call timeout: 30 seconds
- [ ] Graceful fallback: if LLM is unavailable, return error with `clarificationNeeded = "Service temporarily unavailable"`
- [ ] Logging: log user text (sanitized), confidence, and processing time for quality improvement
- [ ] No PII in logs — user text may contain asset names but no personal data

---

## Acceptance Criteria

- [ ] **Given** "Buy ETH when RSI drops below 30 with 2% take profit", **When** interpreted, **Then** returns signal mode config with RSI condition (period 14, operator lt, threshold 30) and TP 2%
- [ ] **Given** "Set up a 5-level grid on BTC with 0.5% spacing", **When** interpreted, **Then** returns grid mode config with gridLevels 5, gridSpacing 0.5
- [ ] **Given** ambiguous text "trade BTC", **When** interpreted, **Then** confidence < 0.5 and `clarificationNeeded` populated
- [ ] **Given** text referencing unsupported indicator "Ichimoku cloud", **When** interpreted, **Then** `clarificationNeeded` explains this condition type is not yet supported
- [ ] **Given** 11th request within 1 minute, **When** submitted, **Then** HTTP 429 returned
- [ ] **Given** LLM unavailable, **When** interpreted, **Then** appropriate error response, no unhandled exception

### Security Considerations

- [ ] User text is placed in a **data** section of the prompt, never as system instructions
- [ ] LLM output is validated against schema before returning — malformed JSON rejected
- [ ] Rate limiting prevents abuse of LLM API costs

### Release Notes Information

- **Heading**: Natural Language Strategy Description
- **Release Note Summary**: Describe your strategy in plain English and the system generates a configuration you can review. Supports all available condition types.
- **Breaking Change**: No
