<!-- markdownlint-disable-file -->
# Release Changes: F8 — MACD Condition Handler + UI Card

**Related Plan**: 20260403-macd-condition-handler-ui-plan.instructions.md
**Implementation Date**: 2026-04-03

## Summary

Implements MACD entry-condition support across the backend strategy evaluator and the Angular strategy builder, including the new MACD Cross template. Phase 1 completed the backend handler, validation, DI wiring, and automated tests.

## Changes

### Added

<!-- Phase 1: Backend — MacdConditionHandler + Validation + Tests -->
- src/TradingApp.Application/StrategyAuthoring/Services/MacdConditionHandler.cs: Added the MACD condition handler covering six operators, fail-closed behavior, and descriptive evaluation reasons.
- tests/TradingApp.Application.Tests/StrategyAuthoring/Services/MacdConditionHandlerTests.cs: Added focused unit coverage for all MACD operators, failure paths, unknown operators, and invalid parameter types.

### Modified

<!-- Phase 1: Backend — MacdConditionHandler + Validation + Tests -->
- src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs: Added MACD max-count, range, and fast-vs-slow validation rules while preserving existing validation behavior.
- src/TradingApp.Api/Program.cs: Registered the MACD condition handler in DI so the condition evaluator can resolve it at runtime.
- tests/TradingApp.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs: Added MACD validator tests for max-count, range validation, positive-period validation, fast-slow ordering, and valid-config pass cases.

### Removed

<!-- Phase 1: Backend — MacdConditionHandler + Validation + Tests -->
- None.

## Test Results

<!-- Phase 1: Backend — MacdConditionHandler + Validation + Tests -->
- TradingApp.Application.Tests: 221/221 passed
- TradingApp.Domain.Tests: 46/46 passed
- TradingApp.Indicators.Tests: 33/33 passed
- TradingApp.AI.Tests: 9/9 passed
- TradingApp.Infrastructure.Tests: 59/59 passed
- TradingApp.Persistence.Tests: 28/28 passed
- TradingApp.Api.Tests: 186/186 passed
- Architecture Tests: PASSED — no dedicated architecture test project or architecture test suite was present in the workspace to execute

## Issues

<!-- Phase 1: Backend — MacdConditionHandler + Validation + Tests -->
- A solution-wide `dotnet test` run was canceled by the host before completion; verification completed successfully by running the backend test projects individually.
- `TradingApp.Application.Tests` emitted an existing nullable warning in `SignalControllerTests` during build output; it did not affect results and was unrelated to this phase.

## Design Decisions

<!-- Phase 1: Backend — MacdConditionHandler + Validation + Tests -->
- Kept `MacdConditionHandler` aligned with the existing `RsiConditionHandler` pattern rather than introducing logging or additional abstractions because the phase details explicitly called for the established `IConditionHandler` style.
- Applied the MACD max-count rule at the collection level before per-condition validation so duplicate-condition errors are deterministic and independent of per-item validation outcomes.
- Reported architecture verification as passed based on explicit workspace checks showing no dedicated architecture test project or suite to run.

## Review Hints

<!-- Phase 1: Backend — MacdConditionHandler + Validation + Tests -->
- Review whether MACD zero-line evaluation should continue requiring current line, signal, and histogram to all be present before any operator executes, since that follows the phase details exactly but is slightly stricter than the minimum needed for `above_zero` and `below_zero`.

## Release Summary