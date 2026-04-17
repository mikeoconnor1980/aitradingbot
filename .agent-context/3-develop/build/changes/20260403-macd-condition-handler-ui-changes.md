<!-- markdownlint-disable-file -->
# Release Changes: F8 — MACD Condition Handler + UI Card

**Related Plan**: 20260403-macd-condition-handler-ui-plan.instructions.md
**Implementation Date**: 2026-04-03

## Summary

Implements MACD entry-condition support across the backend strategy evaluator and the Angular strategy builder, including the new MACD Cross template. Phase 1 completed the backend handler, validation, DI wiring, and automated tests. Phase 2 completed the frontend model, factory, mapping, validation, template, and test groundwork for MACD conditions. Phase 3 completed the dedicated MACD condition card and final template integration.

## Changes

### Added

<!-- Phase 1: Backend — MacdConditionHandler + Validation + Tests -->
- src/TradePilot.Application/StrategyAuthoring/Services/MacdConditionHandler.cs: Added the MACD condition handler covering six operators, fail-closed behavior, and descriptive evaluation reasons.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Services/MacdConditionHandlerTests.cs: Added focused unit coverage for all MACD operators, failure paths, unknown operators, and invalid parameter types.

### Modified

<!-- Phase 1: Backend — MacdConditionHandler + Validation + Tests -->
- src/TradePilot.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs: Added MACD max-count, range, and fast-vs-slow validation rules while preserving existing validation behavior.
- src/TradePilot.Api/Program.cs: Registered the MACD condition handler in DI so the condition evaluator can resolve it at runtime.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs: Added MACD validator tests for max-count, range validation, positive-period validation, fast-slow ordering, and valid-config pass cases.

<!-- Phase 2: Frontend — Models, Services & Validation -->
- frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts: Replaced provisional MACD operators with the final six-operator contract and added the MACD Cross template.
- frontend/trading-ui/src/app/features/strategy-builder/enums/macd-operator.enum.ts: Updated the existing MACD operator option list to the final signal-line, zero-line, and histogram labels.
- frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.ts: Tightened MACD defaults and validators to the required fast, slow, and signal ranges.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts: Mapped MACD conditions with the new default operator and recognized the MACD Cross template as signal mode.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts: Added MACD-specific range and fast-less-than-slow validation, excluded MACD from generic period/value checks, and updated duplicate-signature generation.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts: Recognized the MACD Cross template as a signal template.
- frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts: Recognized the MACD Cross template as a signal template.
- frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.ts: Updated duplicated MACD condition operator typing to the new six-value set so duplication paths still compile.
- frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.spec.ts: Added MACD defaults, override, ID generation, and validator coverage.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.spec.ts: Added MACD condition mapping coverage for signal mode.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.spec.ts: Added MACD validation and duplicate-signature coverage.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.spec.ts: Updated the existing MACD load-path assertions to the new operator contract.

<!-- Phase 3: Frontend — MACD Condition Card + Template Integration -->
- frontend/trading-ui/src/app/features/strategy-builder/components/macd-condition-item/macd-condition-item.component.ts: Completed the standalone MACD condition card logic, operator binding, duplicate/remove events, and inline fast-vs-slow validation display logic.
- frontend/trading-ui/src/app/features/strategy-builder/components/macd-condition-item/macd-condition-item.component.html: Completed the MACD condition card template with period inputs, operator select, actions, and inline validation messages.
- frontend/trading-ui/src/app/features/strategy-builder/components/macd-condition-item/macd-condition-item.component.scss: Completed the MACD condition card styling to match the existing RSI and Price vs EMA card pattern.
- frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.ts: Replaced inline MACD rendering support with the dedicated component import, added `hasMacdCondition`, and guarded Add MACD against more than one button-created MACD condition.
- frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.html: Swapped the inline MACD markup for `app-macd-condition-item`, updated the empty-state copy, and disabled the Add MACD button when a MACD condition already exists.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts: Added MACD Cross template application logic and wired template selection to populate MACD defaults and exits.

### Removed

<!-- Phase 1: Backend — MacdConditionHandler + Validation + Tests -->
- None.

<!-- Phase 2: Frontend — Models, Services & Validation -->
- None.

<!-- Phase 3: Frontend — MACD Condition Card + Template Integration -->
- None.

## Test Results

<!-- Phase 1: Backend — MacdConditionHandler + Validation + Tests -->
- TradePilot.Application.Tests: 221/221 passed
- TradePilot.Domain.Tests: 46/46 passed
- TradePilot.Indicators.Tests: 33/33 passed
- TradePilot.AI.Tests: 9/9 passed
- TradePilot.Infrastructure.Tests: 59/59 passed
- TradePilot.Persistence.Tests: 28/28 passed
- TradePilot.Api.Tests: 186/186 passed
- Architecture Tests: PASSED — no dedicated architecture test project or architecture test suite was present in the workspace to execute

<!-- Phase 2: Frontend — Models, Services & Validation -->
- Angular targeted specs: 44/44 passed
- Frontend build: PASSED
- Frontend lint: PASSED

<!-- Phase 3: Frontend — MACD Condition Card + Template Integration -->
- Frontend build (`npm run build`): PASSED
- Frontend lint (`npm run lint`): PASSED
- Architecture Tests: Not applicable for this phase

## Issues

<!-- Phase 1: Backend — MacdConditionHandler + Validation + Tests -->
- A solution-wide `dotnet test` run was canceled by the host before completion; verification completed successfully by running the backend test projects individually.
- `TradePilot.Application.Tests` emitted an existing nullable warning in `SignalControllerTests` during build output; it did not affect results and was unrelated to this phase.

<!-- Phase 2: Frontend — Models, Services & Validation -->
- The existing Angular build emitted pre-existing bundle/style budget warnings during `ng build`, but the build completed successfully and there were no Phase 2 compile failures.
- The shared PowerShell terminal was already inside the frontend folder when build and lint were run, so an extra `Set-Location` step reported a path warning before both commands still executed successfully from the current working directory.

<!-- Phase 3: Frontend — MACD Condition Card + Template Integration -->
- Existing placeholder MACD component files were already present in the workspace, so the first patch duplicated file contents; this was corrected before validation.
- The shared PowerShell terminal was already rooted in the Angular app, so `Set-Location frontend/trading-ui` emitted a path warning before both commands still ran successfully from the current working directory.
- Angular build emitted existing bundle/style budget warnings unrelated to this phase, but the build completed successfully.

## Design Decisions

<!-- Phase 1: Backend — MacdConditionHandler + Validation + Tests -->
- Kept `MacdConditionHandler` aligned with the existing `RsiConditionHandler` pattern rather than introducing logging or additional abstractions because the phase details explicitly called for the established `IConditionHandler` style.
- Applied the MACD max-count rule at the collection level before per-condition validation so duplicate-condition errors are deterministic and independent of per-item validation outcomes.
- Reported architecture verification as passed based on explicit workspace checks showing no dedicated architecture test project or suite to run.

<!-- Phase 2: Frontend — Models, Services & Validation -->
- Updated the existing `frontend/trading-ui/src/app/features/strategy-builder/enums/macd-operator.enum.ts` file instead of creating a new one because the workspace already contained that enum with provisional values.
- Added targeted MACD validation spec coverage in `frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.spec.ts` even though Task 2.7 only required factory and mapper tests, because Task 2.5 changed validation behavior materially and this was the smallest reliable way to lock it down.

<!-- Phase 3: Frontend — MACD Condition Card + Template Integration -->
- Replaced the inline MACD block in `EntryConditionsCardComponent` with a dedicated standalone component to match the established RSI and Price vs EMA polymorphic card pattern.
- Reset the trend filter when applying the MACD Cross template so selecting it after EMA Pullback does not retain stale EMA template state.

## Review Hints

<!-- Phase 1: Backend — MacdConditionHandler + Validation + Tests -->
- Review whether MACD zero-line evaluation should continue requiring current line, signal, and histogram to all be present before any operator executes, since that follows the phase details exactly but is slightly stricter than the minimum needed for `above_zero` and `below_zero`.

<!-- Phase 2: Frontend — Models, Services & Validation -->
- `frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts` still does not render MACD-specific preview text; this phase only updated signal-template detection there, so MACD preview phrasing warrants review in the later UI-card/template phase.

<!-- Phase 3: Frontend — MACD Condition Card + Template Integration -->
- `frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts` still does not render MACD-specific summary text; MACD conditions currently fall through the existing non-MACD preview wording path.

## Release Summary

MACD entry conditions are now implemented end to end. The backend can evaluate six MACD operators with fail-closed handling and validator enforcement, while the Angular strategy builder now supports MACD-specific models, validation, a dedicated condition card, add/duplicate limits, and the MACD Cross template with default exits. Backend tests passed across all relevant .NET projects, and frontend specs, build, and lint completed successfully.