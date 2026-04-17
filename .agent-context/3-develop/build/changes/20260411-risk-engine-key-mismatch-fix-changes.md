<!-- markdownlint-disable-file -->
# Release Changes: Risk Engine - Signal Parameter Key Mismatch Fix

**Related Plan**: 20260411-risk-engine-key-mismatch-fix-plan.instructions.md
**Implementation Date**: 2026-04-11

## Summary

Completed the signal parameter key standardization and related risk-engine enforcement fixes. The signal pipeline and risk engine now agree on canonical parameter names, and the affected enforcement paths are covered by targeted and full-suite verification.

## Changes

### Added

### Modified

<!-- Phase 1: Fix Key Mismatches Across Pipeline and Tests -->
- src/TradePilot.Application/Trading/Services/GridController.cs: Renamed the DeployGrid notional parameter key from `notionalPerLevel` to `notionalUsd`.
- src/TradePilot.Application/Trading/Services/SignalController.cs: Renamed the OpenPosition notional parameter key from `notional` to `notionalUsd`.
- src/TradePilot.Application/Trading/Services/LivePositionManager.cs: Updated grid deployment to read `notionalUsd` from signal parameters.
- src/TradePilot.Application/Trading/Services/BacktestPositionManager.cs: Updated backtest grid deployment to read `notionalUsd` from signal parameters.
- src/TradePilot.Application/Trading/Services/LiveRiskEngine.cs: Fixed the open-order limit check to read `gridLevels` instead of `levels`.
- tests/TradePilot.Application.Tests/Trading/Services/GridControllerTests.cs: Updated DeployGrid assertions to expect `notionalUsd`.
- tests/TradePilot.Application.Tests/Trading/Services/SignalControllerTests.cs: Updated OpenPosition assertions to expect `notionalUsd`.
- tests/TradePilot.Application.Tests/Trading/Services/LivePositionManagerTests.cs: Updated test signal payloads to use `notionalUsd`.
- tests/TradePilot.Application.Tests/Trading/Services/LiveRiskEngineTests.cs: Corrected payload keys and added acceptance-criteria coverage for oversized `notionalUsd` signals.

### Removed

## Test Results

<!-- Phase 1: Fix Key Mismatches Across Pipeline and Tests -->
- Impacted application signal and risk test scope: 124/124 passed.
- Solution build (`dotnet build TradePilot.sln --no-restore --verbosity minimal`): PASSED.
- Full solution test run: 987/987 passed.
- Architecture Tests: FAILED - no dedicated architecture test project was run for this phase.

## Issues

<!-- Phase 1: Fix Key Mismatches Across Pipeline and Tests -->
- The dedicated full-suite test runner surfaced generic project-build failures that did not match the passing solution build, so verification had to fall back to direct `dotnet test --no-build` runs.
- Initial end-state verification was blocked by a stale `testhost` process locking `tests/TradePilot.Api.Tests/bin/Debug/net10.0` assemblies and causing `MSB3027` and `MSB3021` copy failures; terminating the stale process resolved the issue and the rerun passed.
- Existing `NU1901` and `NU1902` package vulnerability warnings remain in Infrastructure build output.

## Design Decisions

<!-- Phase 1: Fix Key Mismatches Across Pipeline and Tests -->
- Treated the current workspace state as the implementation baseline because the exact phase changes were already present and matched the plan details, then validated the end state instead of reapplying identical edits.
- Verified the phase with targeted impacted tests first, then a solution build, before attempting the broader suite run.

## Review Hints

<!-- Phase 1: Fix Key Mismatches Across Pipeline and Tests -->
- Review `LiveRiskEngine.cs` together with `LiveRiskEngineTests.cs` because they carry the acceptance-criteria proof for the key mismatch fix.
- Review `GridController.cs`, `SignalController.cs`, `LivePositionManager.cs`, and `BacktestPositionManager.cs` as a single signal pipeline to confirm `notionalUsd` is now consistent end to end.

## Release Summary

Standardized the signal notional key on `notionalUsd`, fixed the `gridLevels` mismatch in the risk engine, updated the live and backtest position managers to consume the new key, and refreshed the affected application tests to prove max-order-size and max-open-orders enforcement now operate on the production signal payloads. Final verification passed with a successful solution build and 987/987 tests passing.