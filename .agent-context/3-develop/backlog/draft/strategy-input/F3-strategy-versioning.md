# PBI Specification: F3 — Strategy Versioning & Revision History

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Implementation Phase:** 1a (Post-UI)
**Risk Level:** Low
**Depends On:** F1 (Schema), F2 (Strategy Builder UI)

---

## Summary

Every strategy save creates a new revision with source metadata. Users can view revision history and diff between any two versions.

### User Story

> As a **trader**, I want to **see what changed between versions of my strategy** so that **I can track parameter tuning and correlate changes with performance**.

---

## Requirements

### Functional Requirements

- [ ] Each save creates a `StrategyRevision` (auto-incrementing per strategy) with full canonical JSON snapshot, source metadata, timestamp, userId
- [ ] Only the latest revision is "active" (used by engine)
- [ ] `GET /api/strategies/{id}/versions` — revision list metadata
- [ ] `GET /api/strategies/{id}/versions/{rev}` — full JSON for a revision
- [ ] `GET /api/strategies/{id}/diff?from={a}&to={b}` — structured diff (changed fields with old/new values)
- [ ] Frontend revision history panel with revision list and diff view

---

## Acceptance Criteria

- [ ] **Given** a save, **When** complete, **Then** new revision created with incremented number
- [ ] **Given** two revisions with different grid spacing, **When** diff requested, **Then** changed field listed
- [ ] **Given** revision history UI, **When** two revisions selected, **Then** diff highlights changes

### Release Notes Information

- **Heading**: Strategy Versioning
- **Release Note Summary**: Strategy edits create versioned revisions with diff comparison.
- **Breaking Change**: No
