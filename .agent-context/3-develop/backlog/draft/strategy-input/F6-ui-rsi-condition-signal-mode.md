# PBI Specification: F6 — UI: RSI Condition Card + Signal Mode

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Reference:** [strategy-builder-ui-detailed.md](../../1-discover/prd/strategy-builder-ui-detailed.md)
**Implementation Phase:** 1b (First Signal Condition)
**Risk Level:** Low
**Depends On:** F2 (Builder UI — Grid), F5 (Condition Evaluator + RSI Handler)

---

## Summary

Enable signal mode in the Strategy Builder UI. Unlock the entry conditions card and deliver the RSI condition item component from the UI spec. The "EMA Pullback" and "RSI Reversal" templates remain "coming soon" (EMA handler doesn't exist yet), but users can create custom signal strategies with RSI conditions.

### User Story

> As a **trader**, I want to **add RSI conditions to a strategy using the visual builder** so that **I can create signal-based entry rules without writing JSON**.

---

## Requirements

### Functional Requirements

- [ ] Template selector: add a "Custom Signal" template that sets `strategyMode = "signal"` with an empty conditions array
- [ ] When `strategyMode = "signal"`: grid config card hidden, entry conditions card enabled, trend filter card remains disabled (no evaluator yet — F7)
- [ ] Entry conditions card: "Add RSI" button adds an RSI condition item to the FormArray
- [ ] RSI condition item (from UI spec): Period (number, default 14), Operator (dropdown: lt, lte, gt, gte, cross_above, cross_below), Value (number, 0–100, default 40)
- [ ] Common condition shell: enabled checkbox, label input, duplicate button, remove button
- [ ] Condition factory creates RSI FormGroup with validators (period > 0, value 0–100)
- [ ] Preview summary generates signal-mode text: "Enter a long trade on BTC-USD 15m when RSI(14) is below 40."
- [ ] Validation: at least one entry condition required for signal mode
- [ ] Mapper sets `strategyMode = "signal"`, `grid = null`, populates `entryConditions` array

### Non-Functional Requirements

- [ ] Adding a new condition type (EMA, MACD) later requires: new item component + factory entry + "Add" button — no changes to the conditions card orchestration

---

## Acceptance Criteria

- [ ] **Given** "Custom Signal" template, **When** selected, **Then** grid card hidden, entry conditions card enabled
- [ ] **Given** "Add RSI" clicked, **When** condition added, **Then** RSI fields (period, operator, value) displayed with defaults
- [ ] **Given** RSI value = 150, **When** form validates, **Then** error: "RSI value must be between 0 and 100"
- [ ] **Given** signal strategy with one RSI condition, **When** saved, **Then** canonical JSON has `strategyMode = "signal"`, `grid = null`, `entryConditions` with RSI entry
- [ ] **Given** preview card, **When** RSI condition configured, **Then** shows "when RSI(14) is below 40"
- [ ] **Given** signal mode with no conditions, **When** validated, **Then** error: "At least one entry condition required"

### Release Notes Information

- **Heading**: RSI Condition in Strategy Builder
- **Release Note Summary**: Create signal-based strategies with RSI entry conditions in the visual strategy builder.
- **Breaking Change**: No
