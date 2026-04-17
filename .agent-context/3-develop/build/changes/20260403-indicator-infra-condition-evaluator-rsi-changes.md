<!-- markdownlint-disable-file -->
# Release Changes: F5 — Indicator Infrastructure & Condition Evaluator (RSI)

**Related Plan**: 20260403-indicator-infra-condition-evaluator-rsi-plan.instructions.md
**Implementation Date**: 2026-04-03

## Summary

Implements indicator lookup infrastructure, RSI condition evaluation, and composite strategy engine routing for signal-mode strategies while preserving the existing grid path.

## Changes

### Added

<!-- Phase 1: Indicator Infrastructure -->
- src/TradePilot.Application/Trading/Models/IndicatorContext.cs: Added dynamic indicator storage with current and previous lookup methods for RSI, EMA, and MACD.
- src/TradePilot.Application/StrategyAuthoring/Models/IndicatorRequirement.cs: Added the model describing indicator computation requirements.
- src/TradePilot.Application/StrategyAuthoring/Services/IndicatorExtractor.cs: Added strategy-config-driven indicator requirement extraction with deduplication.
- tests/TradePilot.Application.Tests/Trading/Models/IndicatorContextTests.cs: Added unit tests for indicator storage and missing-value behavior.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Services/IndicatorExtractorTests.cs: Added unit tests for extraction, disabled conditions, and deduplication.
- tests/TradePilot.Application.Tests/Trading/Services/BacktestMarketContextBuilderIndicatorTests.cs: Added unit tests for dynamic indicator context population in the market context builder.

<!-- Phase 2: Condition Evaluator Engine & RSI Handler -->
- src/TradePilot.Application/StrategyAuthoring/Models/ConditionResult.cs: Added the per-condition evaluation result model.
- src/TradePilot.Application/StrategyAuthoring/Models/ConditionEvaluationResult.cs: Added the aggregate entry-condition evaluation result model.
- src/TradePilot.Application/StrategyAuthoring/Services/IConditionHandler.cs: Added the contract for condition-specific evaluators.
- src/TradePilot.Application/StrategyAuthoring/Services/RsiConditionHandler.cs: Implemented RSI threshold and cross operator evaluation with missing-data handling.
- src/TradePilot.Application/StrategyAuthoring/Services/ConditionEvaluator.cs: Added the evaluator orchestration service and interface for combining condition results with All/Any logic.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Services/RsiConditionHandlerTests.cs: Added unit coverage for all RSI operator paths and edge cases.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Services/ConditionEvaluatorTests.cs: Added unit coverage for evaluator combination logic, skipped unknown types, and missing-context scenarios.

<!-- Phase 3: Strategy Engine Routing & Integration -->
- src/TradePilot.Application/Trading/Services/CompositeStrategyEngine.cs: Added strategy-mode routing between the existing grid engine and the signal condition evaluator.
- tests/TradePilot.Application.Tests/Trading/Services/CompositeStrategyEngineTests.cs: Added coverage for grid routing, signal routing, and invalid config handling.

### Modified

<!-- Phase 1: Indicator Infrastructure -->
- src/TradePilot.Application/Abstractions/Services/IMarketContextBuilder.cs: Added the overload that accepts required indicator definitions while preserving the existing signature.
- src/TradePilot.Application/Trading/Services/BacktestMarketContextBuilder.cs: Added config-driven indicator context creation plus previous RSI and EMA calculations.
- src/TradePilot.Application/Trading/Models/MarketContext.cs: Added the nullable IndicatorContext property for dynamic indicator access.

<!-- Phase 3: Strategy Engine Routing & Integration -->
- src/TradePilot.Api/Program.cs: Rewired DI so IStrategyEngine resolves to CompositeStrategyEngine and registered the condition evaluator and RSI handler.
- src/TradePilot.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs: Removed the obsolete SIGNAL_MODE_NOT_SUPPORTED info message.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/CrossFieldValidatorTests.cs: Updated validation coverage to assert signal mode no longer emits the unsupported info message.

### Removed

## Test Results

<!-- Phase 1: Indicator Infrastructure -->
- dotnet build src/TradePilot.Application/TradePilot.Application.csproj: PASSED
- dotnet build tests/TradePilot.Application.Tests/TradePilot.Application.Tests.csproj: PASSED
- TradePilot.Application.Tests: 199/199 passed
- Architecture Tests: Not applicable — none defined in this phase

<!-- Phase 2: Condition Evaluator Engine & RSI Handler -->
- TradePilot.Application.Tests: 131/131 passed
- Architecture Tests: Not applicable — none defined in this phase

<!-- Phase 3: Strategy Engine Routing & Integration -->
- Targeted Phase 3 verification (CompositeStrategyEngineTests, CrossFieldValidatorTests, GridControllerTests, StrategySchedulerTests, RealBacktestRunnerTests): 50/50 passed
- TradePilot.Application.Tests: 135/135 passed
- TradePilot.Domain.Tests: 46/46 passed
- TradePilot.Infrastructure.Tests: 59/59 passed
- TradePilot.Persistence.Tests: 27/27 passed
- TradePilot.Api.Tests: 176/178 passed
- Architecture Tests: Not applicable — none defined in this phase
- dotnet build TradePilot.sln: PASSED

## Issues

<!-- Phase 1: Indicator Infrastructure -->
- None

<!-- Phase 2: Condition Evaluator Engine & RSI Handler -->
- None

<!-- Phase 3: Strategy Engine Routing & Integration -->
- Full solution verification is blocked by two unrelated API test failures in tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs.
- TradePilot.Api.Tests.Controllers.BacktestsControllerTests.GivenNoStrategyIdAndNoConfig_WhenPostBacktest_ThenReturnsBadRequest fails with KeyNotFoundException while reading a missing response property.
- TradePilot.Api.Tests.Controllers.BacktestsControllerTests.GivenMissingRequiredFields_WhenPostBacktest_ThenReturnsBadRequest fails because the response no longer contains the expected errors payload.
- Those failures are outside this phase's code changes and are already present in the working tree's backtest request/controller path.

## Design Decisions

<!-- Phase 1: Indicator Infrastructure -->
- IndicatorContext uses explicit TryGetValue-based lookup so missing indicators return null rather than a numeric default, which matches the phase success criteria.
- IndicatorExtractor currently covers entry-condition-derived requirements only. Trend filter extraction was intentionally left out for the later phase referenced by the plan.

<!-- Phase 2: Condition Evaluator Engine & RSI Handler -->
- Unknown condition types are logged and included in results as skipped, but excluded from pass/fail aggregation so they do not block known conditions.
- RSI reason strings use invariant numeric formatting to keep test output and diagnostics stable.
- Tests construct candles via the domain factory to stay aligned with the existing entity pattern rather than using object initializers shown in the draft detail snippet.

<!-- Phase 3: Strategy Engine Routing & Integration -->
- CompositeStrategyEngine preserves the existing GridStrategyEngine behavior by delegating grid-mode evaluation directly rather than duplicating any grid logic.
- The composite engine injects GridStrategyEngine as a concrete type so DI routing stays explicit and signal-mode additions remain additive through IConditionHandler registrations.

## Review Hints

<!-- Phase 1: Indicator Infrastructure -->
- Review the previous-candle calculations in src/TradePilot.Application/Trading/Services/BacktestMarketContextBuilder.cs because they are the key behavior added for future cross-detection support.

<!-- Phase 2: Condition Evaluator Engine & RSI Handler -->
- Review src/TradePilot.Application/StrategyAuthoring/Services/ConditionEvaluator.cs for the chosen behavior when all enabled conditions are unknown types, because that behavior follows the phase detail precisely and will affect future handler additions.

<!-- Phase 3: Strategy Engine Routing & Integration -->
- Review src/TradePilot.Application/Trading/Services/CompositeStrategyEngine.cs for the deliberate choice to treat any non-Signal mode as grid-mode delegation, matching the minimal safe routing behavior from the phase detail.
- Review the DI setup in src/TradePilot.Api/Program.cs to confirm future condition handlers can be added only by registering additional IConditionHandler implementations.

## Release Summary