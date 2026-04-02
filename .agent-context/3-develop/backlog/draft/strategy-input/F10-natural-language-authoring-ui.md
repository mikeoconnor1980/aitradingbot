# PBI Specification: F10 — Natural Language Authoring UI

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Reference:** [strategy-builder-ui-detailed.md](../../1-discover/prd/strategy-builder-ui-detailed.md)
**Implementation Phase:** 2 (Natural Language Authoring)
**Risk Level:** Medium
**Depends On:** F9 (NL Interpreter), F2 (Strategy Builder UI)

---

## Summary

Add a natural language input panel to the Strategy Builder UI. The trader types a description, the interpreter (F9) returns a `StrategyIntentDto`, the UI shows assumptions and confidence, then loads the generated config into the existing Strategy Builder form for review and editing.

This is the "describe then edit" flow: NL is the entry point, the form-based builder is the editor.

### User Story

> As a **trader**, I want to **type a strategy description and see the generated configuration in the form builder** so that **I can quickly create strategies and fine-tune the details**.

---

## Requirements

### Functional Requirements

#### NL Input Component

- [ ] Text area component at the top of Strategy Builder (collapsible section)
- [ ] Placeholder: "Describe your strategy in plain English, e.g. 'Buy ETH when RSI drops below 30 with a 2% take profit'"
- [ ] Character counter (max 500)
- [ ] "Generate" button — calls `POST /api/strategies/interpret`
- [ ] Loading spinner during interpretation
- [ ] Error state: shows message if interpreter fails or is rate limited

#### Assumptions Display

- [ ] After generation, show assumptions panel listing each assumption: field name, assumed value, reason
- [ ] Example: "RSI Period — assumed 14 (standard default)"
- [ ] Each assumption has an "Accept" (auto-selected) or "Edit" action
- [ ] "Edit" scrolls to the relevant field in the form below

#### Confidence Badge

- [ ] Confidence score displayed as badge: High (≥ 0.8, green), Medium (0.5–0.79, amber), Low (< 0.5, red)
- [ ] Low confidence shows warning: "The system wasn't confident about this interpretation. Please review carefully."
- [ ] If `clarificationNeeded` is set, display the clarification message prominently

#### Form Population

- [ ] Generated `StrategyConfig` from the interpreter loads into the reactive form model (F2)
- [ ] Strategy mode toggle (grid/signal) set automatically from interpreted config
- [ ] All fields editable — NL is a starting point, not a lock
- [ ] Form validation runs after population; any validation errors highlighted

#### Iteration

- [ ] User can edit text and re-generate — replaces current form values (with confirmation dialog)
- [ ] "Clear" button resets both NL text and form

---

## Acceptance Criteria

- [ ] **Given** user types "Buy ETH when RSI < 30, take profit at 3%", **When** Generate clicked, **Then** form populated with signal mode, RSI condition, TP 3%, and assumptions shown
- [ ] **Given** interpretation returns confidence 0.4, **When** displayed, **Then** red badge and warning message visible
- [ ] **Given** assumption "RSI Period — assumed 14", **When** "Edit" clicked, **Then** view scrolls to RSI period field in the form
- [ ] **Given** form already has values, **When** user re-generates, **Then** confirmation dialog shown before overwriting
- [ ] **Given** interpreter returns error (rate limit), **When** displayed, **Then** error message shown, form unchanged

### Release Notes Information

- **Heading**: Natural Language Strategy Authoring
- **Release Note Summary**: Type a strategy description in plain English to auto-generate a configuration. Review assumptions, adjust in the form builder, then save.
- **Breaking Change**: No
