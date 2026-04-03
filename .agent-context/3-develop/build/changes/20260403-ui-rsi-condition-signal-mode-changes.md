<!-- markdownlint-disable-file -->
# Release Changes: F6 — UI: RSI Condition Card + Signal Mode

**Related Plan**: 20260403-ui-rsi-condition-signal-mode-plan.instructions.md
**Implementation Date**: 2026-04-03

## Summary

Implements signal-mode authoring in the strategy builder by adding RSI entry conditions, mapper and validation support, and preview/load behavior.

## Changes

### Added

<!-- Phase 1: Foundation — Models, Enums & Condition Factory -->
- frontend/trading-ui/src/app/features/strategy-builder/enums/rsi-operator.enum.ts: Added RSI operator options plus a display-name helper for all supported backend operators.
- frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.ts: Added a root-scoped factory service that creates validated RSI condition form groups with backend-aligned defaults.

<!-- Phase 2: UI Components — RSI Condition Item & Entry Conditions Card -->
- frontend/trading-ui/src/app/features/strategy-builder/components/rsi-condition-item/rsi-condition-item.component.ts: Added the standalone RSI condition item component with reactive-form bindings and duplicate/remove outputs.
- frontend/trading-ui/src/app/features/strategy-builder/components/rsi-condition-item/rsi-condition-item.component.html: Added the RSI condition item template with enabled toggle, label field, operator dropdown, numeric inputs, and validation messages.
- frontend/trading-ui/src/app/features/strategy-builder/components/rsi-condition-item/rsi-condition-item.component.scss: Added responsive styling for the RSI condition shell and field layout.

<!-- Phase 3: Page Integration — Wiring, Mapper, Validation, Preview & Tests -->
- frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.spec.ts: Added unit coverage for RSI condition defaults, overrides, validators, and generated ids.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.spec.ts: Added unit coverage for grid-mode and signal-mode mapper output.

### Modified

<!-- Phase 1: Foundation — Models, Enums & Condition Factory -->
- frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts: Added signal-mode condition types and widened strategy config fields to support entry logic, entry conditions, and the Custom Signal template.

<!-- Phase 2: UI Components — RSI Condition Item & Entry Conditions Card -->
- frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.ts: Replaced the stub with FormArray-aware add, duplicate, and remove orchestration backed by ConditionFactoryService.
- frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.html: Replaced the placeholder markup with functional condition rendering, empty-state handling, and Add RSI action.
- frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.scss: Updated the card styling to match the active builder card appearance.

<!-- Phase 3: Page Integration — Wiring, Mapper, Validation, Preview & Tests -->
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts: Added signal-mode form wiring, conditions FormArray support, mode switching, and signal-strategy load hydration.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html: Switched card rendering to show grid config only in grid mode and entry conditions only in signal mode.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts: Added signal-mode serialization with strategyMode, entryLogic, and mapped RSI entry conditions.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts: Added signal-mode validation branch and skipped grid validation when signal mode is active.
- frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts: Added signal-mode preview text generation for RSI conditions.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.spec.ts: Extended coverage for signal-mode validation behavior and reused shared base form helpers.
- frontend/trading-ui/src/app/core/services/backtest.service.spec.ts: Updated the stale validateCoverage test call to match the current service signature so the Angular suite can compile and run.

### Removed

## Test Results

<!-- Phase 1: Foundation — Models, Enums & Condition Factory -->
- Angular Build: PASSED
- Angular Lint: PASSED
- Architecture Tests: Not run - not part of this frontend-only phase

<!-- Phase 2: UI Components — RSI Condition Item & Entry Conditions Card -->
- Angular Build: PASSED
- Angular Lint: PASSED
- Architecture Tests: Not run - not part of this frontend-only phase

<!-- Phase 3: Page Integration — Wiring, Mapper, Validation, Preview & Tests -->
- BacktestService spec: 5/5 passed
- Angular Build: PASSED
- Angular Lint: PASSED
- Angular Test Suite: 139/139 passed
- Architecture Tests: Not run - not part of this frontend-only phase

## Issues

<!-- Phase 1: Foundation — Models, Enums & Condition Factory -->
- ng build completed successfully but reported existing Angular bundle/style budget warnings; these did not block the phase.
- An initial lint command reused the shell working directory and printed a benign Set-Location error before linting still passed; lint was re-run cleanly and passed.

<!-- Phase 2: UI Components — RSI Condition Item & Entry Conditions Card -->
- Editor diagnostics briefly reported a standalone component import error for the new entry conditions card, but Angular build completed successfully and confirmed the code compiled correctly.
- Angular build reported existing bundle/style budget warnings unrelated to this phase; they did not block completion.

<!-- Phase 3: Page Integration — Wiring, Mapper, Validation, Preview & Tests -->
- The shared PowerShell session had a reused working directory, so the first test command resolved the frontend path incorrectly. Verification was rerun with an absolute path.
- The frontend test suite was initially blocked by an outdated call in frontend/trading-ui/src/app/core/services/backtest.service.spec.ts that still passed four arguments to validateCoverage after the service API had been reduced to two required arguments plus optional context. This was resolved by updating the spec to use the current signature.
- Angular production build still reports existing bundle/style budget warnings, but the build completes successfully and these warnings did not block Task 3.9.

## Design Decisions

<!-- Phase 1: Foundation — Models, Enums & Condition Factory -->
- Used additive type changes only in frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts to avoid affecting existing grid-mode behavior.
- Kept RSI operator labels ASCII-only in frontend/trading-ui/src/app/features/strategy-builder/enums/rsi-operator.enum.ts to match repo editing constraints while preserving clear dropdown text.
- Typed the condition factory override contract with the existing RsiOperator union so future UI wiring stays aligned with backend-supported operator literals.

<!-- Phase 2: UI Components — RSI Condition Item & Entry Conditions Card -->
- Kept the entry conditions card input optional in Phase 2 so the current builder page can continue rendering before Phase 3 wires in the real FormArray.
- Preserved the existing Available in signal mode fallback when the card is unbound, while enabling full add/duplicate/remove behavior once the FormArray is supplied.
- Ensured duplicated RSI conditions receive a fresh generated id instead of copying the source id.

<!-- Phase 3: Page Integration — Wiring, Mapper, Validation, Preview & Tests -->
- Signal mode disables the existing grid form group instead of removing it so previous grid values remain intact while grid validation is excluded.
- Signal-mode payload mapping hardcodes entryLogic to all exactly as specified for this phase.
- Strategy loading only hydrates RSI conditions into the FormArray and safely ignores future condition types until later phases add corresponding UI handlers.
- Kept the verification blocker fix minimal and local to frontend/trading-ui/src/app/core/services/backtest.service.spec.ts instead of widening the runtime service API back to a deprecated signature.

## Review Hints

<!-- Phase 1: Foundation — Models, Enums & Condition Factory -->
- Review frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.ts for whether generated condition IDs should remain client-local counters or switch to a UUID-style format in a later phase when duplication/load flows are wired.

<!-- Phase 2: UI Components — RSI Condition Item & Entry Conditions Card -->
- Review the unbound fallback behavior in EntryConditionsCardComponent to confirm it is the preferred temporary bridge until Phase 3 page integration is implemented.
- Review the duplication path in EntryConditionsCardComponent to confirm generating a new id on clone matches the expected condition identity behavior.

<!-- Phase 3: Page Integration — Wiring, Mapper, Validation, Preview & Tests -->
- Review the signal-mode template switching in strategy-builder-page.component.ts to confirm preserving disabled grid values is the intended UX.
- Review the signal-strategy load path to confirm skipping unsupported non-RSI condition types is acceptable until future condition cards are implemented.
- Review frontend/trading-ui/src/app/core/services/backtest.service.spec.ts to confirm the validateCoverage contract change is intentionally test-only cleanup and not masking any expected date-range behavior in the service API.

## Release Summary

Delivered signal-mode authoring support in the Angular strategy builder, including the Custom Signal template, RSI condition management UI, mapper and validation support, preview text, strategy reload hydration, and unit coverage for the new signal-mode branches. Final frontend verification passed with Angular build, lint, and 139 of 139 tests green.