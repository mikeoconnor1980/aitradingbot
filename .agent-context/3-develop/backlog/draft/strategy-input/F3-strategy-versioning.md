# PBI Specification: F3 — Strategy Versioning & Revision History

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
**Last Updated:** 2026-04-02
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Implementation Phase:** 1a (Post-UI)
**Risk Level:** Low
**Depends On:** F1 (Schema), F2 (Strategy Builder UI)

---

## Summary

Every strategy save creates a new revision with source metadata (save origin, optional user label, and auto-generated change summary). Users can view paginated revision history, deep-diff between any two versions, and restore a previous revision as the active version. All data is tenant-scoped by UserId.

### User Story

> As a **trader**, I want to **see what changed between versions of my strategy** so that **I can track parameter tuning and correlate changes with performance**.

### Business Value

Strategy tuning is iterative. Without revision history, traders lose track of what changed and when, making it impossible to correlate parameter adjustments with live or backtest performance. Versioning gives traders confidence to experiment knowing they can inspect and revert changes.

---

## Requirements

### Functional Requirements

- [ ] Each save creates a `StrategyRevision` (auto-incrementing `RevisionNumber` per strategy) with: full canonical JSON snapshot, source (e.g. `UI`, `API`, `Import`), optional user-provided label, auto-generated change summary (computed by comparing current vs previous JSON snapshot), timestamp, userId
- [ ] Only the latest revision is "active" (used by the strategy engine)
- [ ] The first save for a strategy creates revision 1 with change summary "Initial version"
- [ ] `GET /api/strategies/{id}/versions` — paginated revision list metadata (revision number, source, label, change summary, timestamp); supports `page` and `pageSize` query parameters
- [ ] `GET /api/strategies/{id}/versions/{rev}` — full JSON snapshot for a specific revision
- [ ] `GET /api/strategies/{id}/diff?from={a}&to={b}` — structured deep diff (nested field-level changes with JSON path, old value, new value)
- [ ] `POST /api/strategies/{id}/versions/{rev}/restore` — restores a previous revision as the new active version (creates a new revision from the old snapshot with source `Restore` and label referencing the restored revision number)
- [ ] Restore is blocked while the strategy is actively running; API returns 409 Conflict with a message to pause the strategy first
- [ ] Revision storage is unlimited (pruning deferred to a future PBI)
- [ ] Frontend inline revision history panel on the strategy detail page with: revision list, side-by-side or inline diff view, restore button

### Non-Functional Requirements

- [ ] Revision list endpoint responds within 200ms for up to 100 revisions per strategy
- [ ] Diff computation is performed on-demand (not pre-computed) and responds within 500ms
- [ ] All revision endpoints are tenant-scoped — users can only access revisions for their own strategies
- [ ] JSON snapshots are stored as-is (no compression) for simplicity; revisit if storage becomes a concern

---

## User Flow

### Happy Path

1. Trader opens a strategy in the Strategy Builder UI
2. Trader modifies grid spacing and saves
3. System creates a new revision with incremented number, captures save origin (`UI`), auto-generates change summary (e.g., "gridConfig.spacing: 0.5 → 0.8")
4. Trader opens the revision history panel on the strategy detail page
5. Trader sees a paginated list of revisions with timestamps, labels, and change summaries
6. Trader selects two revisions and views a deep diff showing nested field-level changes
7. Trader decides to restore an earlier revision, clicks "Restore"
8. System verifies the strategy is not actively running, creates a new revision from the old snapshot, and sets it as active

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Restore attempted while strategy is running | 409 Conflict — "Pause the strategy before restoring a revision" |
| Revision number does not exist | 404 Not Found |
| Strategy belongs to another user | 404 Not Found (do not leak existence) |
| `from` or `to` revision not found for diff | 404 Not Found |
| `from` equals `to` in diff request | 400 Bad Request — "Cannot diff a revision with itself" |

---

## Acceptance Criteria

- [ ] **Given** a strategy with no revisions, **When** the trader saves for the first time, **Then** revision 1 is created with change summary "Initial version"
- [ ] **Given** a strategy with revision 1 (spacing=0.5), **When** the trader saves with spacing=0.8, **Then** revision 2 is created with change summary listing "gridConfig.spacing: 0.5 → 0.8"
- [ ] **Given** a strategy with 3 revisions, **When** the trader requests `GET /versions?page=1&pageSize=2`, **Then** 2 revision metadata items are returned with pagination info
- [ ] **Given** revision 2, **When** the trader requests `GET /versions/2`, **Then** the full JSON snapshot for revision 2 is returned
- [ ] **Given** revisions 1 and 3 with different grid spacing and exit config, **When** diff requested from=1&to=3, **Then** nested field-level changes are listed with JSON paths, old values, and new values
- [ ] **Given** a diff request where from=2 and to=2, **When** submitted, **Then** 400 Bad Request is returned
- [ ] **Given** a strategy that is paused, **When** the trader restores revision 1, **Then** a new revision N+1 is created from revision 1's snapshot with source "Restore" and label "Restored from revision 1"
- [ ] **Given** a strategy that is actively running, **When** the trader attempts to restore a revision, **Then** 409 Conflict is returned
- [ ] **Given** revision history UI, **When** two revisions are selected, **Then** the diff panel highlights changed fields with old/new values
- [ ] **Given** a user, **When** they request revisions for another user's strategy, **Then** 404 Not Found is returned

### Release Notes Information

- **Heading**: Strategy Versioning & Revision History
- **Release note type**: Feature
- **Release Note Summary**: Strategy edits now create versioned revisions. View change history, compare any two versions with a deep diff, and restore previous configurations.
- **Release Notes Audience**: Product
- **Breaking Change**: No

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/strategies/{id}/versions?page=1&pageSize=20` | Paginated revision list (metadata only) |
| GET | `/api/strategies/{id}/versions/{rev}` | Full JSON snapshot for a revision |
| GET | `/api/strategies/{id}/diff?from={a}&to={b}` | Deep structured diff between two revisions |
| POST | `/api/strategies/{id}/versions/{rev}/restore` | Restore a previous revision as the new active version |

### Integration Events

None — versioning is an internal concern with no cross-boundary side effects.

### Jobs

None.

---

## Out of Scope

- Revision pruning / retention policies (deferred)
- Linking revisions to backtest runs (separate feature)
- Admin cross-tenant revision access
- LLM-generated change summaries
- Compression of stored JSON snapshots
