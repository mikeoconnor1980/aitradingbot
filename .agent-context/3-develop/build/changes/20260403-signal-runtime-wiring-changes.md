<!-- markdownlint-disable-file -->
# Release Changes: F6.75 - Signal Runtime Wiring + Execution Path

**Related Plan**: 20260403-signal-runtime-wiring-plan.instructions.md
**Implementation Date**: 2026-04-03

## Summary

Implements signal-mode runtime wiring through scheduler, execution, and backtest paths, including signal-controller branching, backtest trade pairing, and end-to-end signal-mode regression coverage.

## Changes

### Added

<!-- Phase 1: Indicator Context Wiring in StrategyScheduler -->
- None.

<!-- Phase 2: Signal Controller and Execution Branch -->
- src/TradingApp.Application/Abstractions/Services/ISignalController.cs: Defines the signal-mode controller contract parallel to the grid controller contract.
- src/TradingApp.Application/Trading/Services/SignalController.cs: Implements signal-mode entry and exit signal emission.
- tests/TradingApp.Application.Tests/Trading/Services/SignalControllerTests.cs: Covers signal controller entry, stop-loss, take-profit, and no-op paths.

<!-- Phase 3: Backtest Signal Execution and Trade Pairing -->
- tests/TradingApp.Application.Tests/Trading/Services/BacktestPositionManagerTests.cs: Adds focused tests for signal-mode OpenPosition handling in the backtest position manager.

### Modified

<!-- Phase 1: Indicator Context Wiring in StrategyScheduler -->
- src/TradingApp.Application/Scheduling/StrategyScheduler.cs: Extracted signal-mode indicator requirements and switched scheduler context creation to the 4-argument market-context builder overload.
- tests/TradingApp.Application.Tests/Scheduling/StrategySchedulerTests.cs: Updated builder mocking to the 4-argument overload and added signal-mode coverage for indicator requirement forwarding, grid-mode null requirements, and populated indicator context.
- tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs: Updated market-context builder mocks to cover both 3-argument warmup usage and 4-argument scheduler usage after the scheduler wiring change.

<!-- Phase 2: Signal Controller and Execution Branch -->
- src/TradingApp.Application/Scheduling/StrategyScheduler.cs: Routes signal-mode evaluations through ISignalController while preserving grid-mode flow.
- src/TradingApp.Api/Program.cs: Registers ISignalController with SignalController in DI.
- tests/TradingApp.Application.Tests/Scheduling/StrategySchedulerTests.cs: Verifies signal-mode uses ISignalController and grid-mode still uses IGridController.

<!-- Phase 3: Backtest Signal Execution and Trade Pairing -->
- src/TradingApp.Application/Trading/Models/TradeType.cs: Added the SignalEntry trade type for signal-mode entries.
- src/TradingApp.Application/Trading/Services/BacktestPositionManager.cs: Added OpenPosition handling that places market buys as SignalEntry trades.
- src/TradingApp.Application/Trading/Services/SignalController.cs: Added a stable signal-mode cycle id to entry and exit signals so backtest pairing is consistent.
- src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs: Injects ISignalController, wires it into StrategyScheduler, pairs SignalEntry with TakeProfit, and keeps signal-mode exits out of grid-cycle accounting.
- tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs: Updated runner construction for ISignalController and added trade-pairing coverage for SignalEntry.
- tests/TradingApp.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs: Upgraded the fixture to the composite runtime path and added passing and non-passing signal-mode backtest coverage.

### Removed

<!-- Phase 1: Indicator Context Wiring in StrategyScheduler -->
- None.

<!-- Phase 2: Signal Controller and Execution Branch -->
- None.

<!-- Phase 3: Backtest Signal Execution and Trade Pairing -->
- None.

## Test Results

<!-- Phase 1: Indicator Context Wiring in StrategyScheduler -->
- StrategySchedulerTests: 7/7 passed
- CompositeStrategyEngineTests: 4/4 passed
- ConditionEvaluatorTests: 10/10 passed
- RsiConditionHandlerTests: 12/12 passed
- IndicatorExtractorTests: 4/4 passed
- GridControllerTests: 12/12 passed
- BacktestRunnerTests: 11/11 passed
- Architecture Tests: NOT RUN

<!-- Phase 2: Signal Controller and Execution Branch -->
- SignalControllerTests: 12/12 passed
- StrategySchedulerTests: 18/18 passed
- TradingApp.Application.Tests: 151/151 passed
- Architecture Tests: NOT RUN

<!-- Phase 3: Backtest Signal Execution and Trade Pairing -->
- BacktestPositionManagerTests + BacktestRunnerTests + RealBacktestRunnerTests: 34/34 passed
- TradingApp.Application.Tests: 156/156 passed
- TradingApp.Api.Tests: 182/182 passed
- Solution test suite: 504/504 passed
- Architecture Tests: NOT RUN

## Issues

<!-- Phase 1: Indicator Context Wiring in StrategyScheduler -->
- New Moq verifications initially used C# pattern matching inside expression trees, which caused CS8122 build errors. Replaced them with simple boolean predicates.
- BacktestRunnerTests initially failed with a null MarketContext during warmup audit logging because BacktestRunner still uses the 3-argument Build overload there. Added both 3-argument and 4-argument mock setups to the test fixture.

<!-- Phase 2: Signal Controller and Execution Branch -->
- No code defects were found during verification. The Phase 2 implementation was already present in the workspace, so the remaining work in this run was verification against the phase details and regression testing.

<!-- Phase 3: Backtest Signal Execution and Trade Pairing -->
- Signal-mode exits initially marked the grid lifecycle as closed, which caused a false grid-cycle count in the real backtest test. Resolved by treating signal-cycle TakeProfit fills as non-grid lifecycle events.
- The host runTests full-suite result reported failures inconsistent with direct project execution. Verified end-state with direct dotnet test runs on the application project, API project, and full solution, all of which passed.

## Design Decisions

<!-- Phase 1: Indicator Context Wiring in StrategyScheduler -->
- Kept the runtime change limited to StrategyScheduler and did not modify BacktestRunner production code, because Phase 1 only requires scheduler indicator wiring.
- Updated BacktestRunnerTests even though they were not a direct Phase 1 code target, because the plan explicitly noted that the scheduler overload change would otherwise break that fixture.

<!-- Phase 2: Signal Controller and Execution Branch -->
- No additional code edits were required because the existing workspace implementation already matched the Phase 2 detail file and passed the required targeted and project-wide application tests.

<!-- Phase 3: Backtest Signal Execution and Trade Pairing -->
- Added a synthetic signal cycle id of signal to SignalController entry and exit signals so SignalEntry and TakeProfit orders pair deterministically in backtests without depending on grid state.
- Kept signal-mode fills out of grid lifecycle and grid-cycle audit counting so signal backtests do not inflate grid metrics.

## Review Hints

<!-- Phase 1: Indicator Context Wiring in StrategyScheduler -->
- Review the new signal-mode scheduler tests to confirm the asserted indicator requirement shape matches the intended extractor contract for RSI and remains stable if additional condition types are added in later phases.

<!-- Phase 2: Signal Controller and Execution Branch -->
- Review src/TradingApp.Application/Scheduling/StrategyScheduler.cs to confirm the intentional fallback behavior: signal-mode only branches to ISignalController when one is supplied, otherwise grid controller remains the default path.

<!-- Phase 3: Backtest Signal Execution and Trade Pairing -->
- Review src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs to confirm the intentional separation between signal-mode trade pairing and grid-cycle metrics.
- Review tests/TradingApp.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs for the RSI candle sequence used to prove the full signal-mode backtest path end to end.

## Release Summary

Completed all 3 phases and 17 tasks in the signal runtime wiring plan.

- Added signal-mode scheduler execution branching through ISignalController without disturbing the grid controller path.
- Enabled signal-mode backtests to open trades via OpenPosition, pair SignalEntry with TakeProfit, and avoid contaminating grid lifecycle metrics.
- Verified the final state with targeted backtest tests, full application tests, API tests, and a full solution test suite run.