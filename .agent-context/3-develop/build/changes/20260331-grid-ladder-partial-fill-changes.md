<!-- markdownlint-disable-file -->
# Release Changes: Grid Ladder Remains Active After Partial Fill

**Related Plan**: 20260331-grid-ladder-partial-fill-plan.instructions.md
**Implementation Date**: 2026-03-31

## Summary

Implemented and verified the corrected multi-level grid lifecycle end to end. The controller now keeps partially filled ladders active until explicit take-profit or stop-loss conditions occur, the backtest integration coverage exercises partial-fill, full-fill, and stop-loss flows, and the knowledge docs now match the runtime behavior.

## Changes

### Added

<!-- Phase 1: GridController Lifecycle Fix + Unit Tests -->
- tests/TradingApp.Application.Tests/Trading/Services/GridControllerTests.cs: Added 12 unit tests covering deploy behavior, partial-fill lifecycle handling, fully-filled behavior, closing behavior, stop-loss priority, and open-position fallback paths.

### Modified

<!-- Phase 1: GridController Lifecycle Fix + Unit Tests -->
- src/TradingApp.Application/Trading/Services/GridController.cs: Refactored the controller to preserve partially filled ladders, evaluate TP on candle close for partial fills, and keep stop-loss priority across open-position states.
- src/TradingApp.Application/Backtesting/Models/CancellationReason.cs: Renamed `PositionOpened` to `TakeProfitTriggered` for clearer audit semantics.
- src/TradingApp.Application/Trading/Models/OrderRequest.cs: Added `CloseReason` propagation so simulated exits can record whether a close came from TP or stop loss.
- src/TradingApp.Application/Backtesting/Models/SimulatedOrder.cs: Added `CloseReason` storage for simulated open orders.
- src/TradingApp.Application/Backtesting/Models/SimulatedFill.cs: Added `CloseReason` to captured fills for downstream audit and cycle summaries.
- src/TradingApp.Application/Backtesting/Services/SimulatedExecutionEngine.cs: Propagated close reasons from simulated orders into fills.
- src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs: Updated grid-cycle audit tracking to infer take-profit versus stop-loss from close reasons on fills.
- src/TradingApp.Application/Trading/Services/BacktestPositionManager.cs: Updated take-profit cancellation logging to use the renamed cancellation reason.
- frontend/trading-ui/src/app/core/models/backtest-debug.model.ts: Renamed the frontend cancellation reason enum member to `TakeProfitTriggered`.
- tests/TradingApp.Api.Tests/Controllers/BacktestsControllerTests.cs: Updated API-facing backtest fixture data to use `CancellationReason.TakeProfitTriggered`.

<!-- Phase 2: Integration Tests + Knowledge Documentation -->
- tests/TradingApp.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs: Added multi-level integration coverage for partial-fill TP, partial-fill stop-loss, fully filled limit TP, and updated the initial-market entry scenario for the corrected lifecycle.
- .agent-context/0-knowledge/15-grid-controller.md: Documented the corrected lifecycle flow and the controller-managed partial-fill TP behavior.
- .agent-context/0-knowledge/24-backtesting-grid-engine-explained.md: Rewrote the backtesting walkthrough and examples to describe ladders remaining active after partial fills and closing only on explicit exit conditions.

### Removed

<!-- Phase 1: GridController Lifecycle Fix + Unit Tests -->
- None.

## Test Results

<!-- Phase 1: GridController Lifecycle Fix + Unit Tests -->
- File diagnostics on touched files: no errors reported.
- `dotnet build`: PASSED.
- `dotnet test tests/TradingApp.Application.Tests --filter "FullyQualifiedName~GridController"`: PASSED (12/12).
- `dotnet test tests/TradingApp.Application.Tests --filter "FullyQualifiedName~RealBacktestRunner"`: PASSED (3/3).
- `dotnet test`: PASSED.
- `TradingApp.Domain.Tests`: PASSED (15/15).
- `TradingApp.Application.Tests`: PASSED (76/76).
- `TradingApp.Infrastructure.Tests`: PASSED (51/51).
- `TradingApp.Persistence.Tests`: PASSED (20/20).
- `TradingApp.Api.Tests`: PASSED (145/145).
- Architecture Tests: Not run.

<!-- Phase 2: Integration Tests + Knowledge Documentation -->
- `dotnet build TradingApp.sln`: PASSED.
- `dotnet test tests/TradingApp.Application.Tests`: PASSED (79/79).
- `dotnet test tests/TradingApp.Domain.Tests`: PASSED (15/15).
- `dotnet test tests/TradingApp.Api.Tests`: PASSED (145/145).
- `dotnet test tests/TradingApp.Infrastructure.Tests`: PASSED (51/51).
- `dotnet test tests/TradingApp.Persistence.Tests`: PASSED (20/20).

## Issues

<!-- Phase 1: GridController Lifecycle Fix + Unit Tests -->
- `dotnet build` and `dotnet test` continue to report existing `NU1903` warnings for `AutoMapper` 12.0.1 in `TradingApp.Application`; these warnings pre-existed this phase and did not block compilation or tests.
- Workspace diagnostics reported pre-existing unrelated frontmatter issues in other plan files under `.agent-context/3-develop/build/plans/`; these were not modified.

<!-- Phase 2: Integration Tests + Knowledge Documentation -->
- The `Phase Implementer` subagent failed twice with an internal tooling error (`mgt.clearMarks is not a function`), so Phase 2 was completed directly in the main agent using the same plan details and verification steps.
- The existing `NU1903` warning for `AutoMapper` 12.0.1 remained during the solution build and test runs, but it did not block verification.

## Design Decisions

<!-- Phase 1: GridController Lifecycle Fix + Unit Tests -->
- Implemented controller-checked take profit for `PartiallyFilled` grids exactly as planned: the controller waits for candle-close TP confirmation instead of placing a persistent TP order.
- Preserved stop-loss precedence ahead of closing-state handling so protective exits still fire if price breaches stop before an existing exit completes.
- Added broader lifecycle coverage in unit tests than the minimum example set so the controller behavior is protected across more than ten scenarios.

<!-- Phase 2: Integration Tests + Knowledge Documentation -->
- Kept the Phase 2 integration coverage inside `RealBacktestRunnerTests` so the scenarios exercise the real scheduler, controller, position manager, and simulated execution pipeline together rather than duplicating lower-level assertions.
- Used deterministic candle sequences that fill exact ladder levels across successive candles so the assertions verify partial-fill accumulation and stop-loss behavior without time-dependent flakiness.
- Documented partial-fill TP as a candle-close controller check and fully-filled TP as a resting limit order to match the implemented runtime split clearly in the knowledge docs.

## Review Hints

<!-- Phase 1: GridController Lifecycle Fix + Unit Tests -->
- Review the open-position branch ordering in `GridController` closely: stop-loss first, then `Closing`, then `PartiallyFilled`, then fallback limit TP handling.
- Pay particular attention to the new partial-fill TP behavior in unit tests and to the renamed cancellation reason surfacing through the API-facing backtest fixtures.

<!-- Phase 2: Integration Tests + Knowledge Documentation -->
- Review the candle sequences in `RealBacktestRunnerTests` to confirm the intended levels fill on each step and that the assertions match the average-entry-driven lifecycle.
- Review the `CloseReason` propagation path (`OrderRequest` -> simulated order -> simulated fill -> `BacktestRunner`) because it now drives the grid-cycle exit reason shown in audit data.

## Release Summary

Implemented the partial-fill grid lifecycle fix across controller logic, audit plumbing, regression tests, and agent knowledge documentation. The final regression pass succeeded across the solution (`TradingApp.Application.Tests` 79/79, `TradingApp.Domain.Tests` 15/15, `TradingApp.Infrastructure.Tests` 51/51, `TradingApp.Persistence.Tests` 20/20, `TradingApp.Api.Tests` 145/145), with only the pre-existing `AutoMapper` NU1903 warning remaining.