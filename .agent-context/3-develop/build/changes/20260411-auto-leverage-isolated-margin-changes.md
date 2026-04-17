<!-- markdownlint-disable-file -->
# Release Changes: Auto-Leverage & Isolated Margin Enforcement

**Related Plan**: 20260411-auto-leverage-isolated-margin-plan.instructions.md
**Implementation Date**: 2026-04-11

## Summary

Implements auto-derived leverage for risk-based sizing, isolated margin enforcement, leverage application in live and simulated execution flows, and backtest liquidation handling.

## Changes

### Added

<!-- Phase 1: Domain Model, Leverage Calculator & Defaults -->
- src/TradePilot.Application/Trading/Services/LeverageCalculator.cs: Added the pure leverage calculation utility with clamp and fallback behavior.
- tests/TradePilot.Application.Tests/Trading/Services/LeverageCalculatorTests.cs: Added MSTest coverage for leverage calculation, clamping, and maintenance-margin derivation.

### Modified

<!-- Phase 1: Domain Model, Leverage Calculator & Defaults -->
- src/TradePilot.Application/StrategyAuthoring/Models/RiskConfig.cs: Added the AutoLeverage flag to the risk configuration record.
- src/TradePilot.Application/Agent/Models/OrderCommandPayload.cs: Changed SetLeveragePayload.IsCross default to false.
- src/TradePilot.Api/Models/SetLeverageRequest.cs: Changed SetLeverageRequest.IsCross default to false.
- src/TradePilot.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs: Added auto-leverage warning and error rules on top of the existing risk-based validation.
- src/TradePilot.Api/Models/RunBacktestRequest.cs: Added AutoLeverage to the backtest risk DTO.
- src/TradePilot.Api/Controllers/BacktestsController.cs: Mapped RiskConfigRequest.AutoLeverage into the domain RiskConfig.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs: Added regression tests for auto-leverage warning and error behavior.
- tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs: Added controller mapping coverage for AutoLeverage.

<!-- Phase 2: Execution Engine SetLeverage -->
- src/TradePilot.Application/Abstractions/Services/IExecutionEngine.cs: Added the SetLeverageAsync contract to the execution boundary interface.
- src/TradePilot.Infrastructure/Services/LiveExecutionEngine.cs: Added signed updateLeverage submission, asset metadata caching with max leverage, and leverage clamping with warning logs.
- src/TradePilot.Api/Services/HyperliquidExecutionEngine.cs: Added SetLeverageAsync delegation to IHyperliquidOrderService with correct isolated-to-cross inversion.
- src/TradePilot.Application/Backtesting/Services/SimulatedExecutionEngine.cs: Added leverage tracking per asset and exposed recorded leverage state for later backtest liquidation work.
- src/TradePilot.Worker/Services/AgentCheckInService.cs: Replaced the SetLeverage stub with a real IExecutionEngine call and success logging.
- tests/TradePilot.Infrastructure.Tests/Services/LiveExecutionEngineTests.cs: Added verification for updateLeverage payload shape, max leverage clamping, and warning logging.
- tests/TradePilot.Application.Tests/Backtesting/Services/SimulatedExecutionEngineTests.cs: Added leverage recording coverage for the simulated engine.
- tests/TradePilot.Api.Tests/Services/HyperliquidExecutionEngineTests.cs: Added delegation coverage for SetLeverageAsync.

<!-- Phase 3: Grid Pipeline Integration -->
- src/TradePilot.Application/Trading/Models/MarketContext.cs: Added nullable MaxLeverage to the runtime market context.
- src/TradePilot.Application/Trading/Services/LiveMarketContextBuilder.cs: Populated MaxLeverage from Hyperliquid meta data with in-memory caching for live contexts.
- src/TradePilot.Application/Trading/Services/BacktestMarketContextBuilder.cs: Populated MaxLeverage with the conservative fallback for backtest contexts.
- src/TradePilot.Application/Trading/Services/GridController.cs: Computed leverage during deploy processing and added leverage plus isIsolated to the DeployGrid signal.
- src/TradePilot.Application/Trading/Services/LivePositionManager.cs: Applied exchange leverage before placing ladder orders and added tolerant signal-parameter parsing.
- src/TradePilot.Api/Services/IHyperliquidOrderService.cs: Changed UpdateLeverageAsync default isCross value to false.
- src/TradePilot.Api/Services/HyperliquidOrderService.cs: Changed UpdateLeverageAsync implementation default isCross value to false.
- src/TradePilot.Worker/Program.cs: Passed IHyperliquidRestClient into the live market-context builder registration.
- tests/TradePilot.Application.Tests/Trading/Services/GridControllerTests.cs: Added coverage for auto leverage, manual leverage fallback, AutoLeverage ignore behavior outside risk-based sizing, and isolated-margin signaling.
- tests/TradePilot.Application.Tests/Trading/Services/LivePositionManagerTests.cs: Added coverage for SetLeverageAsync ordering and backward-compatible behavior when leverage is missing.

<!-- Phase 4: Backtest Liquidation Simulation -->
- src/TradePilot.Application/Backtesting/Models/CancellationReason.cs: Added a dedicated liquidation close reason for backtest exit classification.
- src/TradePilot.Application/Backtesting/Models/SimulatedOrder.cs: Added trigger-order metadata so the simulated engine can model stop-loss and take-profit trigger behavior.
- src/TradePilot.Application/Backtesting/Models/SimulatedPosition.cs: Added leverage, margin-used, and liquidation-price state to the simulated position snapshot.
- src/TradePilot.Application/Backtesting/Services/SimulatedExecutionEngine.cs: Implemented trigger-order execution, leverage context tracking, margin calculation, liquidation-price calculation, and liquidation force-close behavior.
- src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs: Classified liquidation exits distinctly in grid-cycle tracking instead of folding them into stop-loss handling.
- tests/TradePilot.Application.Tests/Backtesting/Services/SimulatedExecutionEngineTests.cs: Added margin-tracking, long-stop-loss, long-liquidation, leverage-one, and short-liquidation coverage.

### Removed

## Test Results

<!-- Phase 1: Domain Model, Leverage Calculator & Defaults -->
- Targeted LeverageCalculatorTests and BusinessRuleValidatorTests: 26/26 passed.
- Targeted BacktestsControllerTests mapping scope: 2/2 passed.
- TradePilot.Application.Tests: 443/443 passed.
- TradePilot.Api.Tests: 205/205 passed.
- dotnet build TradePilot.sln: PASSED.
- Architecture Tests: FAILED - no dedicated architecture tests found in the repository.

<!-- Phase 2: Execution Engine SetLeverage -->
- LiveExecutionEngineTests: 10/10 passed.
- SimulatedExecutionEngineTests: 11/11 passed.
- HyperliquidExecutionEngineTests: 10/10 passed.
- TradePilot.Domain.Tests: PASSED.
- TradePilot.Application.Tests: PASSED.
- TradePilot.AI.Tests: PASSED.
- TradePilot.Infrastructure.Tests: PASSED.
- TradePilot.Api.Tests: 206/206 passed.
- Architecture Tests: FAILED - no dedicated architecture test project or architecture test classes were found in the repository.

<!-- Phase 3: Grid Pipeline Integration -->
- GridControllerTests + LivePositionManagerTests: 58/58 passed.
- TradePilot.Application.Tests: 454/454 passed.
- Architecture Tests: FAILED - no dedicated architecture test project or architecture test classes were found.
- dotnet build TradePilot.sln: PASSED.

<!-- Phase 4: Backtest Liquidation Simulation -->
- SimulatedExecutionEngineTests: 22/22 passed.
- TradePilot.Application.Tests: 459/459 passed.
- Solution Build: PASSED.
- Solution Tests: 987/987 passed.
- Architecture Tests: FAILED - no dedicated architecture test project or architecture-focused test classes were found under tests/.

## Issues

<!-- Phase 1: Domain Model, Leverage Calculator & Defaults -->
- The dedicated runTests tool did not discover the MSTest files directly, so verification was completed with dotnet test at project scope and targeted filters.
- No dedicated architecture test project or architecture test classes were found under tests/, so that portion of Task 1.7 could not be executed.
- Pre-existing warnings remain during build and test, including NU1901 and NU1902 package vulnerability warnings in Infrastructure projects and a duplicate using warning in LiveMarketContextBuilder.cs.

<!-- Phase 2: Execution Engine SetLeverage -->
- The dedicated test runner surfaced generic project-build failures for the infrastructure scope; direct dotnet test output showed the underlying tests were passing.
- A lingering testhost process locked tests/TradePilot.Api.Tests output assemblies and caused transient MSBuild copy failures during solution build and broad test runs; resolved by terminating stale testhost processes and rerunning build/test with no-build where appropriate.
- No architecture test project or architecture-focused test classes were present, so that verification step could not be executed beyond confirming absence.

<!-- Phase 3: Grid Pipeline Integration -->
- A stale testhost process locked tests/TradePilot.Api.Tests output assemblies and caused the first solution build to fail with MSBuild copy errors; this was resolved by terminating testhost and rerunning the build.
- Existing package vulnerability warnings remain in Infrastructure test/build output (NU1901, NU1902), and an existing ASP.NET forwarded headers deprecation warning remains during solution build.

<!-- Phase 4: Backtest Liquidation Simulation -->
- No architecture test project or architecture test classes exist in the repository, so that verification step could only be completed by confirming their absence.
- Existing build warnings remain unchanged: NU1901 and NU1902 package vulnerability warnings in Infrastructure projects, plus the existing ASPDEPR005 forwarded-headers deprecation warning in the API project.

## Design Decisions

<!-- Phase 1: Domain Model, Leverage Calculator & Defaults -->
- Added an API controller regression test for AutoLeverage mapping because this phase changed DTO and controller mapping and needed direct verification.
- Preserved the existing risk-based sizing validation and layered the new auto-leverage rules on top to minimize churn in BusinessRuleValidator.

<!-- Phase 2: Execution Engine SetLeverage -->
- Treated the existing workspace implementation as the Phase 2 baseline because the current code already matched the phase details; focused on validation and end-state verification rather than re-editing matching code.
- Used no-build reruns for API tests after successful compilation to avoid false negatives caused by MSBuild output locking, while still validating the actual produced binaries.

<!-- Phase 3: Grid Pipeline Integration -->
- Live market-context enrichment uses IHyperliquidRestClient directly with a local cache inside LiveMarketContextBuilder to avoid introducing an Application-to-Api dependency on the API-layer metadata cache.
- Backtest market contexts default MaxLeverage to LeverageCalculator.FallbackMaxLeverage because this phase did not introduce a backtest-config input for asset leverage metadata.
- LivePositionManager accepts leverage values from multiple boxed runtime types (int, long, decimal, double, string) to preserve compatibility with existing signal serialization and deserialization paths.

<!-- Phase 4: Backtest Liquidation Simulation -->
- Added real trigger-order simulation inside SimulatedExecutionEngine because liquidation ordering depends on intrabar stop-loss evaluation, and the prior simulated trigger methods were no-ops.
- Added a concrete max-leverage setter on SimulatedExecutionEngine instead of widening the execution-engine interface, keeping the extra liquidation-math input localized to backtest simulation and tests.
- Used the existing close-reason pipeline by extending CancellationReason with LiquidationTriggered so liquidation exits flow through the same trade-log and cycle-tracking path.

## Review Hints

<!-- Phase 1: Domain Model, Leverage Calculator & Defaults -->
- Review src/TradePilot.Application/Trading/Services/LeverageCalculator.cs for the fallback, clamping, and formula alignment with the plan.
- Review src/TradePilot.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs for the interaction between RiskBased validation and the new AutoLeverage warning and error rules.
- Review src/TradePilot.Api/Controllers/BacktestsController.cs and src/TradePilot.Api/Models/RunBacktestRequest.cs to confirm AutoLeverage is preserved through backtest request mapping.

<!-- Phase 2: Execution Engine SetLeverage -->
- Review src/TradePilot.Infrastructure/Services/LiveExecutionEngine.cs for the asset metadata cache shape, NormalizeCoin handling, and leverage clamp path.
- Review src/TradePilot.Worker/Services/AgentCheckInService.cs for the SetLeverage command flow and the IsCross to isIsolated inversion.
- Review tests/TradePilot.Infrastructure.Tests/Services/LiveExecutionEngineTests.cs because that file carries the main acceptance proof for signed updateLeverage payload generation and clamp behavior.

<!-- Phase 3: Grid Pipeline Integration -->
- Review src/TradePilot.Application/Trading/Services/LiveMarketContextBuilder.cs for the live metadata-cache behavior and the synchronous Build path calling the shared async metadata loader.
- Review src/TradePilot.Application/Trading/Services/GridController.cs for the exact leverage-selection rules between auto and manual modes.
- Review src/TradePilot.Application/Trading/Services/LivePositionManager.cs to confirm leverage is set after order cancellation and before any grid-order placement.

<!-- Phase 4: Backtest Liquidation Simulation -->
- Review src/TradePilot.Application/Backtesting/Services/SimulatedExecutionEngine.cs for the gap-handling rule that lets stop-loss fill first unless price has already opened beyond the stop and reached liquidation in the same candle.
- Review src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs to confirm liquidation exits should be reported as Liquidation at the grid-cycle level rather than StopLoss.

## Release Summary

Implemented all four phases of auto leverage and isolated margin enforcement across configuration, validation, live execution, grid pipeline integration, and backtest liquidation handling. Verification completed with targeted and suite-level test runs up to 987/987 passing, while the repeated architecture-test steps were satisfied by confirming that no dedicated architecture test project exists in this repository.
