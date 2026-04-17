<!-- markdownlint-disable-file -->
# Release Changes: Kelly Criterion & Advanced Backtest Metrics

**Related Plan**: 20260412-kelly-criterion-advanced-metrics-plan.instructions.md
**Implementation Date**: 2026-04-12

## Summary

Implemented Kelly Criterion metrics end to end, including backend calculation and persistence, API exposure, Angular result rendering, and backtest list summary metrics.

## Changes

### Added

<!-- Phase 1: Backend — Kelly Calculation, Persistence & Tests -->
- src/TradePilot.Persistence/Migrations/20260412205416_AddKellyMetrics.cs: Adds the EF Core migration for Kelly metric columns on BacktestRuns.
- src/TradePilot.Persistence/Migrations/20260412205416_AddKellyMetrics.Designer.cs: Captures the generated EF Core model metadata for the Kelly metrics migration.

### Modified

<!-- Phase 1: Backend — Kelly Calculation, Persistence & Tests -->
- src/TradePilot.Application/Backtesting/Services/BacktestMetricsCalculator.cs: Added Kelly, half-Kelly, and win/loss R-ratio calculation and mapped them into BacktestResult.
- src/TradePilot.Application/Backtesting/BacktestRunResponseMapper.cs: Mirrored Kelly metric derivation and added persisted-or-derived response mapping.
- src/TradePilot.Application/Backtesting/Models/BacktestResult.cs: Added nullable Kelly-related result fields.
- src/TradePilot.Application/Backtesting/Models/BacktestRunResponse.cs: Added nullable Kelly-related response fields.
- src/TradePilot.Domain/Entities/BacktestRun.cs: Added persisted Kelly fields and extended Create and MarkCompleted to accept them.
- src/TradePilot.Persistence/TradePilotDbContext.cs: Added EF conversion configuration for KellyPercent, HalfKellyPercent, and WinLossRRatio.
- src/TradePilot.Persistence/Migrations/TradePilotDbContextModelSnapshot.cs: Updated the EF model snapshot to include the new BacktestRun columns.
- src/TradePilot.Api/Services/BacktestProcessorService.cs: Passed Kelly metrics through when marking backtests complete.
- src/TradePilot.Api/Models/BacktestSummaryDto.cs: Added ProfitFactor and Sqn to the list-view DTO.
- src/TradePilot.Application/Backtesting/Models/BacktestRunSummary.cs: Added ProfitFactor and Sqn to the summary model.
- src/TradePilot.Persistence/Repositories/BacktestRunRepository.cs: Added ProfitFactor and Sqn to summary projection and mapping.
- src/TradePilot.Api/Controllers/BacktestsController.cs: Exposed ProfitFactor and Sqn in list endpoint responses.
- tests/TradePilot.Application.Tests/Backtesting/Services/BacktestMetricsCalculatorTests.cs: Added Kelly metric assertions and edge-case coverage.

<!-- Phase 2: Frontend — Advanced Metrics Display -->
- frontend/trading-ui/src/app/core/models/backtest.model.ts: Added Kelly-related fields to BacktestResult and advanced summary metrics to BacktestSummary.
- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts: Added Kelly/risk/sample-size helpers, SQN labeling, and profit-factor infinity handling.
- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html: Extended advanced metrics UI with null-safe rendering, SQN labels, Win/Loss R-Ratio, Kelly comparison, warning, and advisory note.
- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.scss: Added styling for the Kelly comparison card and low-sample warning.
- frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.ts: Added Profit Factor and SQN columns plus infinity handling helper.
- frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.html: Rendered Profit Factor and SQN columns with null-safe and infinity fallbacks.
- frontend/trading-ui/src/app/features/backtesting/backtest-list/backtest-list.component.scss: Added muted styling for unavailable table values.

### Removed

<!-- Phase 1: Backend — Kelly Calculation, Persistence & Tests -->
- None.

## Test Results

<!-- Phase 1: Backend — Kelly Calculation, Persistence & Tests -->
- Solution Tests: 1861/1861 passed.
- Build: PASSED (`dotnet build TradePilot.sln --no-restore`).

<!-- Phase 2: Frontend — Advanced Metrics Display -->
- Angular Build: PASSED.
- Angular Lint: PASSED.

## Issues

<!-- Phase 1: Backend — Kelly Calculation, Persistence & Tests -->
- A stale `testhost` process locked `TradePilot.Api.Tests` output assemblies and caused `MSB3027` and `MSB3021` build failures. Resolved by terminating the stale `testhost` process and rerunning the build.
- The initial migration command reserved the `AddKellyMetrics` name before reporting back. Verified the generated migration files and model snapshot were present and correct instead of creating a duplicate migration.

<!-- Phase 2: Frontend — Advanced Metrics Display -->
- `npx ng build` completed with pre-existing Angular/Sass warnings, including global Sass deprecation warnings from `src/styles.scss` and existing bundle/style budget warnings. No build errors were introduced by this phase.

## Design Decisions

<!-- Phase 1: Backend — Kelly Calculation, Persistence & Tests -->
- Kept Kelly calculation logic duplicated between the calculator and response mapper, matching the existing project pattern for derived R-metrics.
- Preserved entity fallback behavior in response mapping by using persisted values first and recomputed metrics second, consistent with existing Expectancy, ProfitFactor, and Sqn handling.

<!-- Phase 2: Frontend — Advanced Metrics Display -->
- Kept Kelly percentages displayed as percentages by multiplying the backend fractional values by 100 in the UI, matching the acceptance criteria and backend formula.
- Rendered the advanced metrics section whenever a result has trades, so unavailable Kelly/R-based values can show `-` instead of hiding the section entirely.
- Displayed Profit Factor as `∞` when the API returns null but the run indicates an all-win outcome, which aligns the frontend with the acceptance expectation without changing backend contracts.

## Review Hints

<!-- Phase 1: Backend — Kelly Calculation, Persistence & Tests -->
- Review the duplicated Kelly formula in both BacktestMetricsCalculator and BacktestRunResponseMapper to ensure they stay aligned if R-metric rules change later.
- Review the migration ordering relative to the existing AddHighWaterMarkToStrategy migration if other pending local migrations are being developed in parallel.

<!-- Phase 2: Frontend — Advanced Metrics Display -->
- Review the decision to show the advanced metrics section for any non-empty result, since this slightly broadens the previous visibility behavior for runs without R-based metrics.
- Review the Profit Factor `∞` heuristic in the list view, which infers infinity from `winRate >= 100` because the summary DTO does not include win/loss counts.

## Release Summary

Completed both planned phases for Kelly Criterion & Advanced Backtest Metrics.

- Phases completed: 2/2
- Total tasks completed: 13/13
- Files added: 2
- Files modified: 20
- Validation: `dotnet build TradePilot.sln --no-restore`, solution tests, Angular build, and Angular lint all passed.
