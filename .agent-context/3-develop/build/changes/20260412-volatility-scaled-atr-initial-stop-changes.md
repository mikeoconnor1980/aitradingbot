<!-- markdownlint-disable-file -->
# Release Changes: Volatility-Scaled Initial Stop Loss (ATR-Based)

**Related Plan**: 20260412-volatility-scaled-atr-initial-stop-plan.instructions.md
**Implementation Date**: 2026-04-13

## Summary

ATR-based initial stop-loss implementation across trading lifecycle, trigger management, and optimizer generation. All four phases are complete, including domain updates, locked ATR exit behavior, fixed exchange stop handling, and optimizer generation support.

## Changes

### Added

### Modified

<!-- Phase 1: Domain Model, Configuration & Validation -->
- src/TradePilot.Application/StrategyAuthoring/Models/ExitRuleType.cs: Added the AtrInitial stop-loss enum member.
- src/TradePilot.Application/StrategyAuthoring/Models/ExitRuleConfig.cs: Added nullable AtrPeriod to support ATR-based stop-loss configuration.
- src/TradePilot.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs: Extended stop-loss validation for AtrInitial multiplier and ATR period rules.
- src/TradePilot.Application/Trading/Models/GridState.cs: Added AtrAtEntry state for locking ATR at entry.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs: Added AtrInitial validation coverage for required multiplier and invalid ATR period.

<!-- Phase 2: SL Distance Resolution & Exit Evaluation -->
- src/TradePilot.Application/Trading/Services/StopLossDistanceResolver.cs: Added AtrInitial stop-loss percent resolution with fixed-percent fallback when ATR is unavailable.
- src/TradePilot.Application/Trading/Services/GridController.cs: Captured AtrAtEntry during deployment, cleared it on reset paths, and evaluated locked ATR initial-stop exits.
- src/TradePilot.Application/Trading/Services/SignalController.cs: Added signal-mode AtrInitial capture and exit evaluation while excluding AtrInitial from fixed-stop matching.
- tests/TradePilot.Application.Tests/Trading/Services/StopLossDistanceResolverTests.cs: Added AtrInitial percentage, fallback, and inverse-volatility sizing coverage.
- tests/TradePilot.Application.Tests/Trading/Services/GridControllerTests.cs: Added AtrInitial deployment, lock, fallback, and trigger behavior coverage.
- tests/TradePilot.Application.Tests/Trading/Services/SignalControllerTests.cs: Added signal-mode AtrInitial capture and exit branch coverage.

<!-- Phase 3: Trigger Order Management -->
- src/TradePilot.Application/Trading/Services/TriggerOrderManager.cs: Added AtrInitial stop-loss pricing anchored to entry and skipped later SL modifications for locked ATR initial stops.
- tests/TradePilot.Application.Tests/Trading/Services/TriggerOrderManagerTests.cs: Added AtrInitial stop-loss calculation and locked-stop update coverage.

<!-- Phase 4: Optimizer Support -->
- src/TradePilot.Application/Optimization/Models/ParameterBounds.cs: Added stop-loss type, ATR multiplier, and ATR period optimizer bounds with defaults that preserve fixed-percent behavior.
- src/TradePilot.Application/Optimization/Services/StrategyConfigGenerator.cs: Added AtrInitial optimizer generation, ATR-aware description formatting, and fast-fail validation for unsupported or incomplete ATR bounds.
- tests/TradePilot.Application.Tests/Optimization/StrategyConfigGeneratorTests.cs: Added optimizer coverage for default fixed-percent behavior, AtrInitial generation, mixed stop-loss generation, and invalid ATR bounds.

### Removed

## Test Results

<!-- Phase 1: Domain Model, Configuration & Validation -->
- BusinessRuleValidatorTests: 25/25 passed
- Architecture Tests: No dedicated architecture-test project or target was found; fallback verification via `dotnet build TradePilot.sln` PASSED

<!-- Phase 2: SL Distance Resolution & Exit Evaluation -->
- TradePilot.Application.Tests filtered run (StopLossDistance, GridController, SignalController, AtrInitial): 56/56 passed
- Solution Build: PASSED
- Architecture Tests: Not run for this phase because the phase details did not require a separate architecture-test target

<!-- Phase 3: Trigger Order Management -->
- TriggerOrderManager filtered tests: 35/35 passed
- Solution Build: PASSED

<!-- Phase 4: Optimizer Support -->
- StrategyConfigGeneratorTests: 49/49 passed
- Solution Build: PASSED
- Full solution test suite: 1109/1109 passed
- Architecture Tests: Not applicable for this phase

## Issues

<!-- Phase 1: Domain Model, Configuration & Validation -->
- Solution build reported pre-existing package vulnerability warnings for Azure.Identity and Microsoft.Identity.Client in infrastructure projects; no phase-specific action required.
- Solution build reported a pre-existing ForwardedHeadersOptions.KnownNetworks deprecation warning in TradePilot.Api; outside this phase scope.

<!-- Phase 2: SL Distance Resolution & Exit Evaluation -->
- Initial targeted build failed because GridControllerTests needed the backtesting models namespace for CancellationReason; fixed in the phase implementation.
- New AtrInitial branches initially produced nullable warnings on AtrAtEntry access; resolved by copying the guarded value into a local before stop-price calculation.
- Solution build reported the same pre-existing NuGet vulnerability warnings in infrastructure projects; no phase-specific action required.

<!-- Phase 3: Trigger Order Management -->
- Solution build reported the same pre-existing NuGet vulnerability warnings in infrastructure projects; no phase-specific action required.

<!-- Phase 4: Optimizer Support -->
- A new optimizer test initially failed to compile because `is null` was used inside an expression-tree lambda; fixed by switching the assertion to `== null`.
- Full solution build reported the same pre-existing NuGet vulnerability warnings in infrastructure projects and the pre-existing ForwardedHeadersOptions.KnownNetworks deprecation warning in the API project; no phase-specific action required.

## Design Decisions

<!-- Phase 1: Domain Model, Configuration & Validation -->
- Reused a shared ATR multiplier validation path for AtrTrailing and AtrInitial to keep stop-loss validation behavior aligned.
- Kept AtrPeriod nullable and additive so existing strategy configurations remain compatible without migration.

<!-- Phase 2: SL Distance Resolution & Exit Evaluation -->
- Captured AtrAtEntry in SignalController open-position flow as well as GridController so AtrInitial works end to end for signal-mode strategies.
- Kept InitialRDollars ownership in GridController only and limited SignalController state handling to AtrAtEntry to preserve existing controller responsibilities.

<!-- Phase 3: Trigger Order Management -->
- Preserved the fixed-percent fallback inside AtrInitial stop-loss price calculation so exchange-native initial stops align with the resolver fallback used in Phase 2.

<!-- Phase 4: Optimizer Support -->
- Added explicit validation for empty StopLossTypes, AtrMultiplierOptions, and AtrPeriodOptions when AtrInitial optimizer generation is enabled so invalid optimizer inputs fail fast.
- Used `SL:ATRx...` in generated descriptions to keep the output ASCII-only while distinguishing ATR-based stops from fixed-percent stops.
- Rejected unsupported optimizer stop-loss types in the generator instead of silently mapping them to fixed-percent behavior.

## Review Hints

<!-- Phase 1: Domain Model, Configuration & Validation -->
- Review the shared ATR multiplier validation wording in BusinessRuleValidator to confirm it is suitably generic for both ATR stop-loss types.
- Verify downstream lifecycle resets clear GridState.AtrAtEntry everywhere InitialRDollars is cleared during Phase 2.

<!-- Phase 2: SL Distance Resolution & Exit Evaluation -->
- Review the signal-mode AtrAtEntry capture in SignalController to confirm that this state extension matches the intended signal runtime model.
- Check the AtrInitial fallback coverage in GridControllerTests to confirm AtrInitial configs carrying a fallback percent no longer match the fixed-stop branch.

<!-- Phase 3: Trigger Order Management -->
- Review the AtrInitial update guard in TriggerOrderManager to confirm the intended behavior is a fixed exchange-native SL after initial placement.

<!-- Phase 4: Optimizer Support -->
- Review the new optimizer stop-loss validation path in StrategyConfigGenerator to confirm fast-fail behavior is the intended contract for incomplete ATR settings.
- Review the generated `SL:ATRx...` description format to confirm it is acceptable for downstream display and debugging.

## Release Summary

Volatility-scaled initial stop loss is fully implemented.

Phase 1 added the `AtrInitial` exit type, nullable `AtrPeriod`, validation coverage, and `GridState.AtrAtEntry` support.

Phase 2 integrated locked ATR-based stop-loss distance resolution, captured entry-time ATR in grid and signal flows, excluded `AtrInitial` from the fixed-stop branch, and added controller and resolver tests.

Phase 3 updated exchange protection order handling so AtrInitial stop prices are anchored to entry, can fall back to fixed percent when ATR is unavailable, and are not modified after initial placement.

Phase 4 extended optimizer bounds and strategy generation to produce `AtrInitial` stop-loss configurations, validate ATR-specific inputs, and emit ATR-aware descriptions.

Verification completed with focused application tests, `StrategyConfigGeneratorTests`, multiple solution builds, and a full solution test run passing at 1109/1109.