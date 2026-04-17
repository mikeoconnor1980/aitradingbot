<!-- markdownlint-disable-file -->
# Release Changes: R-Multiple Exit Types & Trade Tracking

**Related Plan**: 20260412-r-multiple-exits-tracking-plan.instructions.md
**Implementation Date**: 2026-04-12

## Summary

Implementing R-multiple take-profit targeting, per-trade R tracking, aggregate backtest R metrics, and frontend support for configuring and displaying R-based results.

## Changes

### Added

<!-- Phase 4: Aggregate R Metrics & API -->
- src/TradePilot.Persistence/Migrations/20260412181404_AddRMultipleMetrics.cs: Added nullable Expectancy, ProfitFactor, and Sqn columns to BacktestRuns.
- src/TradePilot.Persistence/Migrations/20260412181404_AddRMultipleMetrics.Designer.cs: Added the EF migration designer for the R-metrics schema change.

<!-- Phase 6: Frontend — Backtest Results Display -->
- frontend/trading-ui/src/app/features/backtesting/r-distribution-chart/r-distribution-chart.component.ts: Added a standalone CSS histogram component that buckets realised R results for display.
- frontend/trading-ui/src/app/features/backtesting/r-distribution-chart/r-distribution-chart.component.html: Added the R-distribution histogram template with bucket labels and percentage output.
- frontend/trading-ui/src/app/features/backtesting/r-distribution-chart/r-distribution-chart.component.scss: Added responsive styling for the new R-distribution chart.

### Modified

<!-- Phase 1: Domain Models & Validation -->
- src/TradePilot.Application/StrategyAuthoring/Models/ExitRuleType.cs: Added the RMultiple enum member so strategy configs can express take-profit targets in R.
- src/TradePilot.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs: Added R-multiple-specific take-profit validation for negative and sub-1R values while preserving the base positive-value rule.
- src/TradePilot.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs: Enforced RiskBased sizing for R-multiple take-profit exits without duplicating existing stop-loss requirements.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs: Added coverage for negative, sub-1R, and valid R-multiple take-profit validation paths.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/CrossFieldValidatorTests.cs: Added coverage for RiskBased gating and stop-loss interaction with R-multiple take-profit rules.

<!-- Phase 2: R-Multiple TP Price Calculation -->
- src/TradePilot.Application/Trading/Services/TriggerOrderManager.cs: Added R-multiple take-profit price calculation and reused resolved stop-loss percent when placing or updating TP triggers.
- src/TradePilot.Application/Trading/Services/GridController.cs: Replaced duplicated inline take-profit math with a shared helper that supports R-multiple targets.
- tests/TradePilot.Application.Tests/Trading/Services/TriggerOrderManagerTests.cs: Added long and short R-multiple take-profit calculation coverage plus guard cases for missing stop-loss input.

<!-- Phase 3: Per-Trade R Tracking & MFE/MAE -->
- src/TradePilot.Application/Trading/Models/GridState.cs: Added InitialRDollars so active grid cycles can retain their captured one-R dollar risk.
- src/TradePilot.Application/Trading/Services/PositionSizeResolver.cs: Added ResolveInitialR to expose RiskBased one-R calculation without changing existing notional resolution behavior.
- src/TradePilot.Application/Trading/Services/GridController.cs: Captured InitialRDollars at grid deployment time and cleared it when grids move into closing paths.
- src/TradePilot.Application/Backtesting/Models/BacktestTrade.cs: Added nullable InitialRDollars, RMultipleResult, MFE, and MAE fields for per-trade R analytics.
- src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs: Threaded InitialR through trade pairing, tracked per-candle excursion, and computed realised R metrics at close.
- tests/TradePilot.Application.Tests/Trading/Services/PositionSizeResolverTests.cs: Added RiskBased and non-RiskBased coverage for ResolveInitialR.
- tests/TradePilot.Application.Tests/Trading/Services/GridControllerTests.cs: Added coverage for InitialR capture on deploy and clearing on stop-loss close.
- tests/TradePilot.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs: Added direct trade-log tests for InitialR threading, RMultipleResult calculation, MFE/MAE capture, and null R metrics for non-RiskBased trades.
- tests/TradePilot.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs: Added an integration test covering risk-based backtests with populated R metrics.

<!-- Phase 4: Aggregate R Metrics & API -->
- src/TradePilot.Application/Backtesting/Models/BacktestResult.cs: Added aggregate R-metric fields and distribution output for risk-based backtest summaries.
- src/TradePilot.Application/Backtesting/Services/BacktestMetricsCalculator.cs: Added expectancy, profit factor, SQN, average win and loss R, win rate, and distribution calculations from R-tracked trades.
- src/TradePilot.Domain/Entities/BacktestRun.cs: Added persisted R-metric fields and updated completion methods to accept them.
- src/TradePilot.Persistence/TradePilotDbContext.cs: Configured EF persistence for the new nullable BacktestRun R-metric columns.
- src/TradePilot.Persistence/Migrations/TradePilotDbContextModelSnapshot.cs: Updated the EF snapshot to include the BacktestRun R-metric columns.
- src/TradePilot.Api/Services/BacktestProcessorService.cs: Passed aggregate R metrics into BacktestRun completion persistence.
- src/TradePilot.Application/Backtesting/Models/BacktestRunResponse.cs: Added aggregate R-metric response fields.
- src/TradePilot.Application/Backtesting/Models/BacktestTradeResponse.cs: Added trade-level R response fields.
- src/TradePilot.Application/Backtesting/BacktestRunResponseMapper.cs: Mapped persisted R metrics and derived fallback values from TradesJson for older runs.
- tests/TradePilot.Application.Tests/Backtesting/Services/BacktestMetricsCalculatorTests.cs: Added aggregate R-metric calculation coverage.
- tests/TradePilot.Persistence.Tests/Repositories/BacktestRunRepositoryTests.cs: Added persistence assertions for the new BacktestRun R fields.
- tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs: Added API response coverage for aggregate and trade-level R metrics.
- tests/TradePilot.Api.Tests/Controllers/StrategiesControllerTests.cs: Updated test helpers for the BacktestRun completion signature changes.

<!-- Phase 5: Frontend — Strategy Configuration -->
- frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts: Added r_multiple to the strategy-builder exit rule type union.
- frontend/trading-ui/src/app/core/models/backtest.model.ts: Added a typed backtest exit-rule union including r_multiple for client-side consistency.
- frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.ts: Added take-profit mode helpers and sub-1R warning logic.
- frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.html: Enabled the R-multiple option, rendered the R Target input, and added the warning copy.
- frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.scss: Styled the new R-multiple warning state.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts: Added required validation to the take-profit type control.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts: Preserved the selected take-profit type instead of always forcing fixed_percent.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.spec.ts: Added regression coverage for r_multiple take-profit mapping.

<!-- Phase 6: Frontend — Backtest Results Display -->
- frontend/trading-ui/src/app/core/models/backtest.model.ts: Added aggregate and trade-level R metric fields needed by the backtest results UI.
- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.ts: Added R-metric guards and wired in the histogram component.
- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html: Added conditional R KPI cards and histogram rendering for R-tracked results.
- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.scss: Added styling for the R metrics section and histogram card.
- frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.ts: Added R-data detection, sorting support, formatting helpers, and dynamic detail colspan handling.
- frontend/trading-ui/src/app/features/backtesting/trade-log-table/trade-log-table.component.html: Added conditional Initial R, R-Multiple, MFE, and MAE columns for closed and open trade sections.

### Removed

## Test Results

<!-- Phase 1: Domain Models & Validation -->
- BusinessRuleValidatorTests and CrossFieldValidatorTests: 60/60 passed.
- tests/TradePilot.Application.Tests/TradePilot.Application.Tests.csproj build: passed.
- TradePilot.sln build: passed.
- TradePilot.sln test --no-build: 7 projects completed successfully before the run stalled in TradePilot.Api.Tests.
- tests/TradePilot.Api.Tests/TradePilot.Api.Tests.csproj diagnostic run with blame-hang: remained in Testing state past 166.6 seconds.

<!-- Phase 2: R-Multiple TP Price Calculation -->
- TriggerOrderManagerTests: 52/52 passed.
- tests/TradePilot.Application.Tests/TradePilot.Application.Tests.csproj build: passed.
- TradePilot.sln build: passed.
- CandleIngestionService timing test rerun: 1/1 passed.
- TradePilot.sln test final run: 1038/1038 passed.

<!-- Phase 3: Per-Trade R Tracking & MFE/MAE -->
- TradePilot.Application.Tests focused suite: 62/62 passed.
- TradePilot.sln build: passed.
- TradePilot.sln test: 1046/1046 passed.

<!-- Phase 4: Aggregate R Metrics & API -->
- TradePilot.sln build: passed.
- TradePilot.sln test --no-build: 1050/1050 passed.
- dotnet ef migrations add AddRMultipleMetrics --startup-project ../TradePilot.Api --no-build: passed.

<!-- Phase 5: Frontend — Strategy Configuration -->
- frontend/trading-ui npm run build: passed.
- frontend/trading-ui npm run lint: passed.

<!-- Phase 6: Frontend — Backtest Results Display -->
- frontend/trading-ui npm run build: passed.
- frontend/trading-ui npm run lint: passed.

## Issues

<!-- Phase 1: Domain Models & Validation -->
- A running TradePilot.Api process locked build outputs during the first verification attempt; process 69260 was stopped and the build succeeded on rerun.
- Full-solution test verification could not be completed cleanly because TradePilot.Api.Tests stalled without surfacing a failing test.

<!-- Phase 2: R-Multiple TP Price Calculation -->
- A timing-sensitive CandleIngestionService test briefly failed around an 88.5ms versus 90ms delay threshold under full-suite load, then passed in isolation and on the subsequent full-suite rerun.
- Pre-existing package vulnerability warnings in infrastructure projects and a forwarded headers obsolescence warning in the API project remain unchanged.

<!-- Phase 3: Per-Trade R Tracking & MFE/MAE -->
- A new integration assertion initially expected replay candles to produce MFE >= 1R, but the deterministic sequence produced a smaller positive MFE, so the test was corrected to validate presence and sign instead of an unsupported threshold.
- Existing package vulnerability warnings and the API forwarded headers deprecation warning remain unchanged.

<!-- Phase 4: Aggregate R Metrics & API -->
- The first migration generation attempt failed during its internal build step even though the code compiled; rerunning a clean solution build and then generating the migration with --no-build resolved it.
- EF tooling reported a non-blocking version warning because dotnet-ef is 9.0.9 while the runtime is 10.0.0.
- The phase report noted unrelated concurrent workspace changes around persistence and tracking files; they were not reverted or incorporated beyond the required work for this phase.

<!-- Phase 5: Frontend — Strategy Configuration -->
- The Angular build reported pre-existing Sass deprecation warnings from frontend/trading-ui/src/styles.scss and existing bundle-budget warnings; they did not block the build and were left unchanged.
- No implementation-blocking issues were encountered in the modified frontend files.

<!-- Phase 6: Frontend — Backtest Results Display -->
- The first lint run failed on an unused ngOnChanges parameter in the new histogram component; removing that usage and rerunning lint resolved it.
- The Angular build still reports pre-existing Sass deprecation warnings from frontend/trading-ui/src/styles.scss; they remain unchanged and non-blocking.

## Design Decisions

<!-- Phase 1: Domain Models & Validation -->
- Kept the existing enabled take-profit greater-than-zero invariant, so 0R still maps to the generic TP_VALUE_INVALID rule while negative R-multiples use a dedicated validation code.
- Reused the existing RiskBased stop-loss cross-field rule instead of duplicating stop-loss enforcement inside the new R-multiple validator path.

<!-- Phase 2: R-Multiple TP Price Calculation -->
- Added an optional stop-loss percent parameter to the TriggerOrderManager take-profit calculator so fixed-percent callers remain compatible while R-multiple math can opt in.
- Reused StopLossDistanceResolver rather than duplicating stop-loss distance logic inside take-profit calculation paths.
- Kept GridController changes scoped to the existing long-side flow instead of expanding controller behavior beyond the phase requirements.

<!-- Phase 3: Per-Trade R Tracking & MFE/MAE -->
- Captured InitialR once in GridController and copied it onto opening trades so close-time R metrics do not depend on mutable runtime grid state.
- Kept excursion tracking as an in-memory dictionary inside BacktestRunner to scope the change to backtesting without introducing persistence changes in this phase.
- Cleared InitialRDollars when grids enter closing or closed states to avoid leaking a prior cycle’s risk into a reused GridState instance.

<!-- Phase 4: Aggregate R Metrics & API -->
- Persisted only Expectancy, ProfitFactor, and Sqn on BacktestRun while deriving AvgWinR, AvgLossR, RWinRate, and RDistribution from trade JSON to preserve backward compatibility.
- Used sample standard deviation for SQN and returned null when there are fewer than two R-tracked trades or when variance is zero.
- Added mapper-side fallback computation so historical runs with trade-level R data but null aggregate columns can still surface aggregate metrics.

<!-- Phase 5: Frontend — Strategy Configuration -->
- Reused the existing take-profit value field for both percent and R-multiple modes, changing only labels, hints, and step size in the UI.
- Surfaced sub-1R guidance as a warning instead of a blocking validation error so values between 0 and 1 remain allowed but discouraged.
- Added a typed backtest exit-rule union now to keep frontend models aligned ahead of the results-display phase.

<!-- Phase 6: Frontend — Backtest Results Display -->
- Used data-presence guards instead of a dedicated RiskBased UI flag so the frontend remains compatible with both newly persisted metrics and mapper-derived fallback metrics.
- Implemented the R distribution as a lightweight CSS bucketed bar chart instead of misusing lightweight-charts for value-distribution data.
- Included Initial R alongside realised R, MFE, and MAE in the trade log because the phase success criteria explicitly require that field.

## Review Hints

<!-- Phase 1: Domain Models & Validation -->
- Confirm that 0R should remain invalid rather than receiving its own product-specific validation outcome.
- Investigate the stalled tests/TradePilot.Api.Tests/TradePilot.Api.Tests.csproj run separately before treating whole-solution verification as clean.

<!-- Phase 2: R-Multiple TP Price Calculation -->
- Review the shared take-profit helper in src/TradePilot.Application/Trading/Services/GridController.cs to confirm the current long-side controller assumption is still intentional.
- Review the timing sensitivity in tests/TradePilot.Api.Tests/Services/CandleIngestionServiceTests.cs because it can fail under full-suite load even when reruns pass.

<!-- Phase 3: Per-Trade R Tracking & MFE/MAE -->
- Review the partial-close path in src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs because it is the area most sensitive to future partial-fill semantics.
- Review the new risk-based integration coverage in tests/TradePilot.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs if exact MFE and MAE thresholds need to be tightened later.

<!-- Phase 4: Aggregate R Metrics & API -->
- Review src/TradePilot.Application/Backtesting/BacktestRunResponseMapper.cs closely because it now serves both newly persisted runs and historical runs via a fallback derivation path.
- Review src/TradePilot.Persistence/Migrations/TradePilotDbContextModelSnapshot.cs for migration ordering and snapshot consistency, especially if other persistence work lands nearby.
- Review tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs if exact SQN assertions need to be tightened beyond non-null coverage.

<!-- Phase 5: Frontend — Strategy Configuration -->
- Check the strategy-builder flow end to end to confirm that selecting R-multiple, saving, and reloading preserves both the chosen type and its value.
- Review warning behavior around 0.5R versus 1.0R to confirm the threshold and copy match product expectations.
- Review frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts closely because this is the root-cause fix that lets the selected take-profit type reach the backend.

<!-- Phase 6: Frontend — Backtest Results Display -->
- Review the backtest results page on narrower widths to confirm the added KPI density and histogram still read well in the existing responsive layout.
- Review a historical RiskBased backtest that derives aggregate R metrics from trade JSON to confirm the conditional cards and histogram appear as intended.
- Review the open-positions section of the trade log to confirm showing Initial R, MFE, and MAE with an empty realised R column matches product expectations.

## Release Summary

Implemented all 6 phases of the R-multiple exits and trade tracking plan. The backend now supports R-multiple take-profit rules, captures per-trade InitialR and excursion metrics, calculates and persists aggregate R statistics for backtests, and exposes those values through API responses with backward-compatible fallbacks. The frontend now supports authoring R-multiple take-profit targets, warns on sub-1R setups, shows R KPI cards and a distribution histogram for RiskBased backtests, and adds Initial R, realised R, MFE, and MAE columns to the trade log. Verification completed with passing solution builds and tests on the backend and passing build and lint runs for the Angular frontend.
