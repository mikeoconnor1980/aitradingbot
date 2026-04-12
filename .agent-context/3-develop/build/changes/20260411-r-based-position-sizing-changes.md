<!-- markdownlint-disable-file -->
# Release Changes: R-Based Position Sizing

**Related Plan**: 20260411-r-based-position-sizing-plan.instructions.md
**Implementation Date**: 2026-04-11

## Summary

Implements risk-based position sizing, stop-loss distance resolution, controller integration, validation, API mapping, and backtest verification for R-based trade sizing.

## Changes

### Added

<!-- Phase 1: Domain Model & Core Calculation -->
- tests/TradingApp.Application.Tests/Trading/Services/PositionSizeResolverTests.cs: Added MSTest coverage for PercentWallet, FixedNotional, RiskBased, anti-martingale sizing, and backward-compatible stop-loss handling.

<!-- Phase 2: SL Distance Resolution & Controller Integration -->
- src/TradingApp.Application/Trading/Services/StopLossDistanceResolver.cs: Added a static helper that resolves fixed-percent, ATR-trailing, and grid-breakdown stop-loss distance percentages for sizing.
- tests/TradingApp.Application.Tests/Trading/Services/StopLossDistanceResolverTests.cs: Added MSTest coverage for supported stop-loss resolution paths and unresolved cases.

### Modified

<!-- Phase 1: Domain Model & Core Calculation -->
- src/TradingApp.Application/StrategyAuthoring/Models/PositionSizeType.cs: Added the RiskBased enum member.
- src/TradingApp.Application/StrategyAuthoring/Models/RiskConfig.cs: Added the nullable RiskPerTradePercent property for backward-compatible strategy config serialization.
- src/TradingApp.Application/Trading/Services/PositionSizeResolver.cs: Added optional stopLossPercent support and RiskBased notional calculation using R-based sizing.

<!-- Phase 2: SL Distance Resolution & Controller Integration -->
- src/TradingApp.Application/Trading/Services/GridController.cs: Wired RiskBased sizing through stop-loss resolution, per-level notional division, and zero-size deployment blocking.
- src/TradingApp.Application/Trading/Services/SignalController.cs: Wired RiskBased sizing through stop-loss resolution before open-position sizing.
- tests/TradingApp.Application.Tests/Trading/Services/GridControllerTests.cs: Added RiskBased deployment tests for fixed stop-loss sizing, grid fallback sizing, and unresolvable stop-loss blocking.
- tests/TradingApp.Application.Tests/Trading/Services/SignalControllerTests.cs: Added RiskBased signal-mode sizing and runtime blocking tests and cleaned nullable assertions.

<!-- Phase 3: Validation, API DTO & Backtest Verification -->
- src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs: Added RiskBased validation for RiskPerTradePercent and high-risk warning behavior.
- src/TradingApp.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs: Added cross-field validation requiring stop-loss support for RiskBased sizing with grid fallback handling.
- src/TradingApp.Api/Models/RunBacktestRequest.cs: Added RiskPerTradePercent to the backtest risk DTO.
- src/TradingApp.Api/Controllers/BacktestsController.cs: Mapped RiskPerTradePercent into domain RiskConfig for backtest requests.
- tests/TradingApp.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs: Added coverage for RiskBased validation cases and position-size skip behavior.
- tests/TradingApp.Application.Tests/StrategyAuthoring/Validation/CrossFieldValidatorTests.cs: Added coverage for RiskBased stop-loss requirements and grid breakdown fallback.
- tests/TradingApp.Application.Tests/Trading/Services/PositionSizeResolverTests.cs: Added anti-martingale sequential-loss coverage for RiskBased sizing.
- tests/TradingApp.Api.Tests/Controllers/BacktestsControllerTests.cs: Added controller mapping coverage for RiskPerTradePercent.
- tests/TradingApp.Application.Tests/Trading/Services/LiveRiskEngineTests.cs: Fixed blocking test compilation by replacing invalid init-only assignments with record `with` copies.
- tests/TradingApp.Infrastructure.Tests/Services/LiveExecutionEngineTests.cs: Fixed leverage test fixture metadata so maxLeverage expectations match the implementation.

### Removed

## Test Results

<!-- Phase 1: Domain Model & Core Calculation -->
- tests/TradingApp.Application.Tests/TradingApp.Application.Tests.csproj build: Passed.
- PositionSizeResolver targeted regression plus existing grid percent-wallet sizing test: 14/14 passed.
- TradingApp.Application.Tests full suite: 410/410 passed.

<!-- Phase 2: SL Distance Resolution & Controller Integration -->
- StopLossDistanceResolver, GridController, and SignalController targeted regression scope: 20/20 passed.
- GridController targeted rerun after fallback expectation correction: 32/32 passed.
- tests/TradingApp.Application.Tests/TradingApp.Application.Tests.csproj build: Passed.
- TradingApp.Application.Tests full suite: 423/423 passed.

<!-- Phase 3: Validation, API DTO & Backtest Verification -->
- TradingApp.Application.Tests targeted validation and sizing scope: 62/62 passed.
- BacktestsControllerTests: 32/32 passed.
- LiveExecutionEngineTests: 10/10 passed.
- Full solution test suite: 976/976 passed.
- Solution build: PASSED.

## Issues

<!-- Phase 1: Domain Model & Core Calculation -->
- The dedicated test runner tool did not discover the new test file directly, so verification used dotnet build and dotnet test at the project level.
- Pre-existing warnings remain in src/TradingApp.Application/Trading/Services/LiveMarketContextBuilder.cs and tests/TradingApp.Application.Tests/Trading/Services/SignalControllerTests.cs; they did not block this phase.

<!-- Phase 2: SL Distance Resolution & Controller Integration -->
- The first full-suite run failed on the new grid fallback test because the expected notional used a 5% breakdown threshold while the test still inherited the default 2% value; the test was corrected and rerun.
- A pre-existing duplicate using warning remains in src/TradingApp.Application/Trading/Services/LiveMarketContextBuilder.cs; it does not block build or tests.

<!-- Phase 3: Validation, API DTO & Backtest Verification -->
- The dedicated test runner only reported generic project build failures; direct `dotnet test` output was required to identify the actual blockers.
- A pre-existing compile failure in tests/TradingApp.Application.Tests/Trading/Services/LiveRiskEngineTests.cs used post-construction assignment on an init-only record property; fixed with record `with` expressions.
- A pre-existing failing test in tests/TradingApp.Infrastructure.Tests/Services/LiveExecutionEngineTests.cs seeded exchange metadata without `maxLeverage`, causing fallback clamping and an assertion mismatch; fixed by supplying explicit maxLeverage values in the fixture.
- Non-blocking warnings remain during build/test, including one duplicate using warning in LiveMarketContextBuilder.cs and NU1901/NU1902 package vulnerability warnings in Infrastructure projects.

## Design Decisions

<!-- Phase 1: Domain Model & Core Calculation -->
- Kept ResolveNotional backward-compatible by adding stopLossPercent as an optional parameter so existing call sites remain unchanged until Phase 2.
- Returned zero notional for RiskBased when risk percent or stop-loss percent is absent or non-positive, matching the planned runtime safety model.
- Deferred GridController and SignalController integration to Phase 2 so the calculation layer landed cleanly before end-to-end wiring.

<!-- Phase 2: SL Distance Resolution & Controller Integration -->
- Treated grid breakdown threshold as a valid stop-loss-distance fallback for grid strategies, so runtime blocking only occurs when no fixed, ATR, or grid fallback distance can be resolved.
- Kept SignalController runtime safety on the existing zero-size guard instead of adding a second overlapping guard, centralizing the RiskBased failure mode.
- Left PercentWallet and FixedNotional behavior unchanged by only resolving stop-loss distance for RiskBased sizing.

<!-- Phase 3: Validation, API DTO & Backtest Verification -->
- Treated the existing Phase 3 source and test changes already present in the working tree as the implementation baseline, then completed the phase by validating and fixing only the blockers necessary to satisfy Task 3.6.
- Fixed the blocking tests at the test-fixture level rather than altering production behavior, because the failures were caused by invalid test setup rather than defects in the runtime code.

## Review Hints

<!-- Phase 1: Domain Model & Core Calculation -->
- Review src/TradingApp.Application/Trading/Services/PositionSizeResolver.cs closely for RiskBased guard behavior and backward-compatible optional parameter handling.
- End-to-end RiskBased execution intentionally remains incomplete until Phase 2 wires stop-loss distance into GridController and SignalController.

<!-- Phase 2: SL Distance Resolution & Controller Integration -->
- Review src/TradingApp.Application/Trading/Services/GridController.cs for the RiskBased branch, especially the total-notional-to-per-level conversion and the zero-sized deployment guard.
- Review src/TradingApp.Application/Trading/Services/StopLossDistanceResolver.cs with its tests to confirm resolution precedence and fallback behavior.

<!-- Phase 3: Validation, API DTO & Backtest Verification -->
- Review src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs and src/TradingApp.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs together to confirm save-time behavior for RiskBased sizing matches the planned separation between field validation and cross-field validation.
- Review src/TradingApp.Api/Controllers/BacktestsController.cs and tests/TradingApp.Api.Tests/Controllers/BacktestsControllerTests.cs to confirm RiskPerTradePercent flows unchanged from API DTO to serialized StrategyConfig.
- Review tests/TradingApp.Application.Tests/Trading/Services/PositionSizeResolverTests.cs for the anti-martingale sequential-loss assertions, since that is the acceptance-criteria proof point for backtest sizing behavior.

## Release Summary
- Implemented RiskBased position sizing end to end, including stop-loss-aware notional calculation, controller wiring, save-time validation, API mapping, and anti-martingale verification.
- Completed the remaining Phase 3 work and fixed two unrelated pre-existing test blockers in existing test fixtures so the full solution could validate cleanly.
- Final verification passed with a successful solution build and 976/976 tests passing.