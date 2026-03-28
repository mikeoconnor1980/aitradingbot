<!-- markdownlint-disable-file -->
# Release Changes: F3 — Backtest Replay Engine

**Related Plan**: 20260327-backtest-replay-engine-plan.instructions.md
**Implementation Date**: 2026-03-27

## Summary

Build the backtest replay engine: models, interfaces, scheduling components (CandleClock, StrategyScheduler), SimulatedExecutionEngine, CandleReplayEngine, BacktestMetricsCalculator, and BacktestRunner orchestrator.

## Changes

### Added

<!-- Phase 1: Foundation — Models, Interfaces, and Scheduling -->
- None — Phase 1 implementation already existed in the workspace and required no new files during this run.

<!-- Phase 2: SimulatedExecutionEngine -->
- src/TradingApp.Application/Backtesting/Services/SimulatedExecutionEngine.cs: Added the in-memory execution engine implementing order placement, cancellation, candle-based fills, fee/slippage handling, and position tracking.
- tests/TradingApp.Application.Tests/Backtesting/Services/SimulatedExecutionEngineTests.cs: Added focused MSTest coverage for fills, fees, slippage, fill priority, cancellations, empty-book behavior, and long/short position tracking.

<!-- Phase 3: CandleReplayEngine and BacktestMetricsCalculator -->
- src/TradingApp.Application/Backtesting/Models/ReplayData.cs: Added the replay data container for sorted 15m, 1h, and 4h candles plus the warmup boundary index.
- src/TradingApp.Application/Backtesting/Services/CandleReplayEngine.cs: Added the replay loader with warmup handling, higher-timeframe lookback alignment, and no-lookahead closed-candle selection.
- src/TradingApp.Application/Backtesting/Services/BacktestMetricsCalculator.cs: Added the stateless metrics calculator for summary PnL, drawdown, hold time, fees, hedge count, and final equity.
- tests/TradingApp.Application.Tests/Backtesting/Services/CandleReplayEngineTests.cs: Added MSTest coverage for sorted replay loading, higher-timeframe alignment, missing data handling, and warmup validation.
- tests/TradingApp.Application.Tests/Backtesting/Services/BacktestMetricsCalculatorTests.cs: Added MSTest coverage for aggregate metrics, zero-trade behavior, and max drawdown calculation.

<!-- Phase 4: BacktestRunner Orchestrator -->
- src/TradingApp.Application/Scheduling/StrategyScheduler.cs: Added the scheduler that filters 15m candle-close events and runs the market-context, strategy, grid, risk, and position pipeline in order.
- src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs: Added the backtest orchestrator with config validation, warmup handling, CandleClock integration, fill recording, trade pairing, grid-cycle counting, and equity tracking.
- tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs: Added MSTest coverage for successful orchestration, validation failures, replay-data failures, determinism, and equity-curve initialization.
- tests/TradingApp.Application.Tests/Scheduling/StrategySchedulerTests.cs: Added MSTest coverage for timeframe filtering, pipeline order, and no-signal/no-approved-signal short-circuit behavior.

### Modified

<!-- Phase 1: Foundation — Models, Interfaces, and Scheduling -->
- None — Phase 1 implementation already existed in the workspace and required no file modifications during this run.

<!-- Phase 2: SimulatedExecutionEngine -->
- None.

<!-- Phase 3: CandleReplayEngine and BacktestMetricsCalculator -->
- None.

<!-- Phase 4: BacktestRunner Orchestrator -->
- None.

### Removed

<!-- Phase 1: Foundation — Models, Interfaces, and Scheduling -->
- None — Phase 1 implementation already existed in the workspace and required no file removals during this run.

<!-- Phase 2: SimulatedExecutionEngine -->
- None.

<!-- Phase 3: CandleReplayEngine and BacktestMetricsCalculator -->
- None.

<!-- Phase 4: BacktestRunner Orchestrator -->
- None.

## Test Results

<!-- Phase 1: Foundation — Models, Interfaces, and Scheduling -->
- CandleClockTests: 10/10 passed
- TradingApp.Application.Tests: 5/5 passed
- TradingApp.Domain.Tests: 15/15 passed
- TradingApp.Persistence.Tests: 16/16 passed
- TradingApp.Infrastructure.Tests: 51/51 passed
- TradingApp.Api.Tests: 97/97 passed
- Full solution build: PASSED in Release configuration
- Architecture Tests: Not run — not required by this phase

<!-- Phase 2: SimulatedExecutionEngine -->
- SimulatedExecutionEngineTests: 10/10 passed
- TradingApp.Application.Tests: 15/15 passed
- TradingApp.Domain.Tests: 15/15 passed
- TradingApp.Infrastructure.Tests: 51/51 passed
- TradingApp.Persistence.Tests: 16/16 passed
- TradingApp.Api.Tests: 97/97 passed
- Architecture Tests: Not run — not required by this phase

<!-- Phase 3: CandleReplayEngine and BacktestMetricsCalculator -->
- CandleReplayEngineTests: 6/6 passed
- BacktestMetricsCalculatorTests: 3/3 passed
- TradingApp.Application.Tests: 24/24 passed
- TradingApp.Domain.Tests: 15/15 passed
- TradingApp.Infrastructure.Tests: 51/51 passed
- TradingApp.Persistence.Tests: 16/16 passed
- TradingApp.Api.Tests: 97/97 passed
- Full solution build: PASSED in Release configuration
- Architecture Tests: Not run — not required by this phase

<!-- Phase 4: BacktestRunner Orchestrator -->
- BacktestRunnerTests: 8/8 passed
- StrategySchedulerTests: 4/4 passed
- TradingApp.Application.Tests: 36/36 passed
- TradingApp.Domain.Tests: 15/15 passed
- TradingApp.Persistence.Tests: 16/16 passed
- TradingApp.Infrastructure.Tests: 51/51 passed
- TradingApp.Api.Tests: 97/97 passed
- Full solution build: PASSED in Release configuration
- Architecture Tests: Not run — not required by this phase

## Issues

<!-- Phase 1: Foundation — Models, Interfaces, and Scheduling -->
- A full Debug solution build initially failed because a running TradingApp.Api.exe process was locking API output assemblies. Resolved by validating the required full solution build in Release configuration.
- NU1903 warnings were reported for AutoMapper 12.0.1 due to a known vulnerability. This is pre-existing and unrelated to Phase 1 implementation.

<!-- Phase 2: SimulatedExecutionEngine -->
- The dedicated runTests tool did not discover the new test file by absolute path, so verification switched to dotnet test with a class filter for the focused phase tests.
- NU1903 warnings for AutoMapper 12.0.1 were reported during build. This is pre-existing and unrelated to Phase 2.

<!-- Phase 3: CandleReplayEngine and BacktestMetricsCalculator -->
- The dedicated test runner did not discover the new MSTest files by file path, so focused verification used a filtered dotnet test command instead.
- NU1903 warnings for AutoMapper 12.0.1 were reported during build and test runs. This is pre-existing and unrelated to Phase 3.

<!-- Phase 4: BacktestRunner Orchestrator -->
- The dedicated test runner did not discover the new MSTest files by absolute path, so focused verification used a filtered dotnet test command instead.
- The first focused build failed because src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs was missing the TradingApp.Domain.Entities import for Candle. Resolved by adding the missing using.
- Initial BacktestRunner test fixtures used timestamp 0 for generated candles, but src/TradingApp.Domain/Entities/Candle.cs rejects non-positive timestamps. Resolved by updating the fixtures to use valid positive timestamps.
- NU1903 warnings for AutoMapper 12.0.1 were reported during build and test runs. This is pre-existing and unrelated to Phase 4.

## Design Decisions

<!-- Phase 1: Foundation — Models, Interfaces, and Scheduling -->
- No code patch was required because the Phase 1 source files and CandleClock tests were already present in the workspace and matched the phase details.
- Verification was completed against the existing implementation instead of rewriting already-conformant files.
- The solution build was validated in Release to avoid the environment-specific Debug file lock from the running API process.

<!-- Phase 2: SimulatedExecutionEngine -->
- Fees are deducted immediately into SimulatedPosition.RealisedPnL on every fill so the execution engine state reflects net realised PnL rather than gross trade PnL.
- Unrealised PnL is updated from the processed candle close for the active symbol so the position state is ready for later per-tick equity tracking.
- Full verification used Release configuration because this workspace has a known Debug file-lock issue from a running API process.

<!-- Phase 3: CandleReplayEngine and BacktestMetricsCalculator -->
- Higher-timeframe queries align the warmup start down to the 1h or 4h boundary and then step back one full interval so the first evaluation candle can resolve the latest closed higher-timeframe candle without lookahead bias.
- Higher-timeframe validation checks the first evaluation candle for actual closed 1h and 4h context instead of only checking whether any 1h or 4h rows were returned.
- BacktestMetricsCalculator counts only trades with both ExitTimeUtc and PnL as completed trades, while still including all trade-log fees and hedge-open entries in the aggregate metrics.

<!-- Phase 4: BacktestRunner Orchestrator -->
- The runner creates fresh replay, clock, scheduler, execution, and metrics components per run, while continuing to consume the existing injected pipeline interfaces. This matches the current codebase contracts without inventing new factories outside the phase scope.
- Equity tracking uses the simulated position's realised PnL plus unrealised PnL without subtracting fees a second time, because src/TradingApp.Application/Backtesting/Services/SimulatedExecutionEngine.cs already records realised PnL net of fees.
- Validation requires 15m, 1h, and 4h intervals up front, because the current replay engine and strategy context construction depend on all three timeframes.
- Trade pairing was implemented as FIFO pairing for grid entries to take-profit exits and hedge opens to hedge closes, with gross per-trade PnL stored separately from fees to stay consistent with src/TradingApp.Application/Backtesting/Services/BacktestMetricsCalculator.cs.

## Review Hints

<!-- Phase 1: Foundation — Models, Interfaces, and Scheduling -->
- Review the existing Phase 1 implementation for completeness against the plan, but no deviations were found during verification.
- Consider separately addressing the pre-existing AutoMapper package vulnerability warning.

<!-- Phase 2: SimulatedExecutionEngine -->
- Review the choice to model RealisedPnL as net of fees inside the execution engine, since later metrics code may also aggregate fees separately and should avoid double-counting.
- Review same-candle buy-then-TP behavior against the intended conservative replay semantics, especially if future strategy layers enforce stricter order/position coupling.

<!-- Phase 3: CandleReplayEngine and BacktestMetricsCalculator -->
- Review the higher-timeframe alignment rule in src/TradingApp.Application/Backtesting/Services/CandleReplayEngine.cs against the eventual Phase 4 MarketContextBuilder usage, because the PBI examples mix open-time and close-time wording and the current implementation follows the phase detail rule of closed time less than or equal to the trigger candle open time.

<!-- Phase 4: BacktestRunner Orchestrator -->
- Review the assumption that the injected strategy pipeline services are stateless across runs. The runner now creates fresh orchestration components per run, but the strategy, grid, risk, and position services still come from DI because the current contracts do not expose factory-based construction.
- Review the stricter interval validation in src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs, which enforces 15m, 1h, and 4h explicitly to align with the current replay engine implementation.

## Release Summary

Implemented all four phases of the backtest replay engine plan. The workspace now includes the simulated execution engine, replay loader, metrics calculator, scheduler, and end-to-end backtest runner, along with focused application tests for each component. The implementation was validated with passing phase-specific tests, passing TradingApp.Application.Tests, and full solution Release builds at each major stage. Remaining recorded risks are limited to the pre-existing AutoMapper NU1903 warning and the intentional design assumptions called out in the Review Hints.
