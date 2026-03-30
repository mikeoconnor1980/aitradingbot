<!-- markdownlint-disable-file -->
# Release Changes: SL/TP Modal - Liquidation Price, Live Price & Distance to Liquidation

**Related Plan**: 20260330-sltp-modal-liquidation-live-price-plan.instructions.md
**Implementation Date**: 2026-03-30

## Summary

Implements SL/TP modal enhancements for live price, liquidation reference data, and submission-blocking stop loss validation.

## Changes

### Added

<!-- Phase 1: Modal Logic — Live Price, Liquidation Display & Validation -->
- frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.spec.ts: Added comprehensive modal tests for live price updates, display logic, validation, submit behavior, and short/long position handling.

### Modified

<!-- Phase 1: Modal Logic — Live Price, Liquidation Display & Validation -->
- frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.ts: Injected SignalR live-price updates, added liquidation distance and live-price class helpers, converted liquidation logic into form validation, and corrected position-side detection.
- frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.html: Expanded the modal reference-data grid, removed the old warning block, and disabled Confirm when the form is invalid.
- frontend/trading-ui/src/app/features/dashboard/positions-table/set-sltp-modal/set-sltp.modal.component.scss: Added muted/profit/loss styles for the new reference rows and removed obsolete liquidation warning styling.
- frontend/trading-ui/src/app/features/backtesting/grid-cycle-viewer/grid-cycle-viewer.component.ts: Removed an unused import to restore a clean Angular lint run for the frontend validation task.

### Removed

<!-- Phase 1: Modal Logic — Live Price, Liquidation Display & Validation -->
- None

## Test Results

<!-- Phase 1: Modal Logic — Live Price, Liquidation Display & Validation -->
- SetSlTpModalComponent: 15/15 passed
- Angular frontend test suite: 91/91 passed
- Architecture Tests: Not applicable — frontend-only phase
- Angular build: passed
- Angular lint: passed

## Issues

<!-- Phase 1: Modal Logic — Live Price, Liquidation Display & Validation -->
- The new short-position tests exposed an existing logic defect in the modal: `_isLongPosition()` treated any positive size as long even when `side` was `"Short"`. Resolved by making side authoritative and only falling back to size when side is not recognized.
- `ng build` completed successfully but reported existing budget warnings; these did not block the build.
- Angular lint initially failed on an unrelated unused import in `grid-cycle-viewer.component.ts`; this was trivial and safe to fix, and lint passed after removing it.

## Design Decisions

<!-- Phase 1: Modal Logic — Live Price, Liquidation Display & Validation -->
- Used `SignalRService.priceUpdate$` with `takeUntilDestroyed(this._destroyRef)` to match the existing infinite-observable subscription pattern used elsewhere in the frontend.
- Seeded `livePrice` from `position.markPrice` so the modal has stable initial reference data before the first SignalR update arrives.
- Moved liquidation checks into the stop-loss validator rather than leaving them as display-only logic so the Confirm button state is driven by form validity, which is the safer source of truth.
- Kept `_isLongPosition()` backward-compatible by falling back to `size > 0` only if `side` is not explicitly `"Long"` or `"Short"`.
- Applied the smallest safe fix needed to unblock the validation task by removing only the unused import that Angular lint flagged.

## Review Hints

- Review the modal’s short-position behavior closely, especially the interaction between `side`, live price color logic, and liquidation validation.
- Review whether the existing Angular bundle budget warnings should be handled in a separate optimization pass; they do not block this implementation.

## Release Summary

Implemented the SL/TP modal enhancements to surface liquidation and live-price context, enforce stop-loss validity against liquidation boundaries, and refresh the modal in real time from SignalR. Added comprehensive modal tests and applied one minimal unrelated lint fix so frontend build, tests, and lint now complete successfully.
