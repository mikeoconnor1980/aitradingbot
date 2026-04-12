<!-- markdownlint-disable-file -->
# Release Changes: Optimizer Risk Parameter Sweep

**Related Plan**: 20260411-optimizer-risk-parameter-sweep-plan.instructions.md
**Implementation Date**: 2026-04-12

## Summary

Completed optimizer support for RiskBased sizing sweeps and auto-leverage candidate generation. The optimizer can now branch between PercentWallet and RiskBased candidate generation, validate the relevant bounds, describe RiskBased candidates meaningfully, and accept the new mode through the API contract.

## Changes

### Added

<!-- Phase 1: Domain & Optimizer Model Extensions -->
- src/TradingApp.Application/Optimization/Models/PositionSizeMode.cs: Added the optimizer-specific sizing mode enum with `PercentWallet` and `RiskBased` values.

### Modified

<!-- Phase 1: Domain & Optimizer Model Extensions -->
- src/TradingApp.Application/StrategyAuthoring/Models/RiskConfig.cs: Preserved the new risk-based fields and added a safe default initializer for `RiskPerTradePercent` while keeping the property nullable.
- src/TradingApp.Application/Optimization/Models/ParameterBounds.cs: Added sizing-mode, risk-percent-option, and auto-leverage sweep fields for optimizer candidate generation.
- tests/TradingApp.Application.Tests/StrategyAuthoring/Models/StrategyConfigSerializationTests.cs: Added snake_case serialization and round-trip coverage for `RiskBased`, `RiskPerTradePercent`, and `AutoLeverage`.

<!-- Phase 2: Generator Logic, API Wiring & Comprehensive Tests -->
- src/TradingApp.Application/Optimization/Services/StrategyConfigGenerator.cs: Added mode-specific RiskBased generation, bounds validation, and candidate-description formatting for risk-percent and auto-leverage combinations.
- src/TradingApp.Api/Models/RunOptimizationRequest.cs: Added nullable request fields for `PositionSizeMode`, `RiskPerTradePercentOptions`, and `IncludeAutoLeverage`.
- src/TradingApp.Api/Controllers/OptimizationsController.cs: Wired the new request fields into `ParameterBounds` construction and parsing.
- tests/TradingApp.Application.Tests/Optimization/StrategyConfigGeneratorTests.cs: Added acceptance-criteria coverage for RiskBased candidate generation, conditional leverage sweep behavior, bounds validation, and description output.

### Removed

## Test Results

<!-- Phase 1: Domain & Optimizer Model Extensions -->
- StrategyConfigSerializationTests: 8/8 passed.
- Full solution test suite after Phase 1: 989/989 passed.

<!-- Phase 2: Generator Logic, API Wiring & Comprehensive Tests -->
- StrategyConfigGeneratorTests: 22/22 passed.
- Full solution test suite: 1000/1000 passed.

## Issues

<!-- Phase 1: Domain & Optimizer Model Extensions -->
- The dedicated broad `runTests` path produced generic project-build-failed output, so final verification used a solution build plus `dotnet test TradingApp.sln --no-build --verbosity minimal`.
- Existing non-blocking warnings remain during build and test: `NU1901` and `NU1902` in Infrastructure projects, plus `ASPDEPR005` in the API project.

<!-- Phase 2: Generator Logic, API Wiring & Comprehensive Tests -->
- The dedicated full-suite test runner again surfaced generic project-build-failed output despite a successful solution build; verification was completed with `dotnet build TradingApp.sln --no-restore` followed by `dotnet test TradingApp.sln --no-build`.

## Design Decisions

<!-- Phase 1: Domain & Optimizer Model Extensions -->
- Kept `RiskPerTradePercent` nullable but added a default initializer of `0m` so the model stays compatible with the current risk-based runtime and validation code while still giving older serialized configs a stable default.
- Treated `PositionSizeType.RiskBased` as already present in the workspace baseline and validated that end state instead of reapplying the identical enum change.

<!-- Phase 2: Generator Logic, API Wiring & Comprehensive Tests -->
- Treated the current workspace state as the implementation baseline because the phase-required generator and API changes were already present and matched the plan detail requirements; validation focused on confirming behavior and compatibility instead of reapplying identical edits.

## Review Hints

<!-- Phase 1: Domain & Optimizer Model Extensions -->
- Review `RiskConfig.cs` for the nullable-plus-default handling of `RiskPerTradePercent`, because that was the only material divergence from the phase detail snippet.
- Review `StrategyConfigSerializationTests.cs` because those tests are the proof point for the new enum serialization and risk-config round-trip behaviour.

<!-- Phase 2: Generator Logic, API Wiring & Comprehensive Tests -->
- Review `StrategyConfigGeneratorTests.cs` for the AutoLeverage coverage, since that file proves the optimizer correctly skips manual leverage sweeps when leverage is derived at runtime.
- Review `StrategyConfigGenerator.cs` together with `RunOptimizationRequest.cs` and `OptimizationsController.cs` to confirm the new API inputs map directly into the generator’s mode-specific branch logic.

## Release Summary

Implemented the optimizer RiskBased sweep mode end to end. The domain and optimizer models now carry RiskBased sizing metadata, the generator branches between PercentWallet and RiskBased candidate creation, validation enforces RiskBased-specific bounds, descriptions report R% instead of size percentage where appropriate, and the optimization API accepts the new mode, risk-percent options, and auto-leverage flag. Verification passed with targeted generator coverage and a full solution run of 1000/1000 passing tests.
