# PBI Specification: F11 — Pine Script Import (Deferred)

**PBI ID:** Draft
**Status:** Draft — Deferred to Phase 3
**Iteration:** Backlog
**Created:** 2026-04-02
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Implementation Phase:** 3 (Pine Script Import — deferred)
**Risk Level:** High
**Depends On:** F1 (Extensible Strategy Schema), F5+ (Condition Handlers)

---

## Summary

Allow traders to import TradingView Pine Script strategies. A Python sidecar service parses the Pine Script, extracts strategy parameters and conditions, and maps them to the canonical `StrategyConfig` schema.

**This PBI is deferred.** Phase 1/2 deliver a fully functional strategy builder with form-based and NL authoring. Pine Script import adds a third input channel for TradingView users.

### Why Deferred

1. Pine Script parsing is complex — requires a Python parser (tree-sitter or custom grammar)
2. Only a subset of Pine Script maps to our condition types
3. Form builder + NL cover the primary use cases
4. Sidecar architecture (Python service) adds deployment complexity

### User Story

> As a **trader who uses TradingView**, I want to **paste my Pine Script strategy** so that **the system extracts the relevant parameters and creates a strategy configuration I can use**.

---

## Requirements (Preliminary)

### Functional Requirements

- [ ] Python sidecar service (`pine-parser`) deployed alongside .NET backend
- [ ] `POST /api/strategies/import/pine` endpoint proxies to sidecar
- [ ] Sidecar parses Pine Script → extracts: indicator calls, crossover/crossunder logic, strategy.entry/exit calls, input parameters
- [ ] Maps extracted data to `StrategyConfig` using same condition type vocabulary (RSI, MACD, price_vs_ema, etc.)
- [ ] Returns `PineImportResult`: `StrategyConfig config`, `List<Warning> warnings`, `List<string> unsupportedFeatures`
- [ ] Unsupported Pine features clearly listed (e.g. custom functions, plotting, alertcondition)

### UI (Placeholder)

- [ ] "Import Pine Script" tab in Strategy Builder — shows "Coming Soon" badge
- [ ] When implemented: textarea for Pine Script, "Import" button, warnings display, loads result into form builder

### Non-Functional

- [ ] Sidecar is stateless, horizontally scalable
- [ ] Input size limit: 10KB Pine Script
- [ ] Timeout: 15 seconds
- [ ] No execution of Pine Script — parse only, never eval

---

## Acceptance Criteria (Draft)

- [ ] **Given** simple Pine Script with `strategy.entry` on RSI crossing below 30, **When** imported, **Then** returns signal mode config with RSI condition
- [ ] **Given** Pine Script with unsupported `plotshape`, **When** imported, **Then** `unsupportedFeatures` includes "plotshape"
- [ ] **Given** Pine Script exceeding 10KB, **When** submitted, **Then** HTTP 413 returned
- [ ] **Given** "Import Pine Script" tab before implementation, **When** clicked, **Then** "Coming Soon" message displayed

### Phase 1 Deliverable

- [ ] "Coming Soon" placeholder in UI only — no sidecar, no parsing

### Release Notes Information

- **Heading**: Pine Script Import (Coming Soon)
- **Release Note Summary**: A future release will support importing TradingView Pine Script strategies. The import tab is visible but not yet active.
- **Breaking Change**: No
