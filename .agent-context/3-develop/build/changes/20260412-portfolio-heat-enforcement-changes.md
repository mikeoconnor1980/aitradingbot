<!-- markdownlint-disable-file -->
# Release Changes: Portfolio Heat Enforcement

**Related Plan**: 20260412-portfolio-heat-enforcement-plan.instructions.md
**Implementation Date**: 2026-04-12

## Summary

Portfolio heat enforcement is implemented across configuration, live risk checks, the API, the dashboard, and backtesting.

## Changes

### Added

<!-- Phase 1: Configuration + Heat Calculation Core -->
- src/TradingApp.Application/Trading/Models/PortfolioHeatEntry.cs: Added the per-position portfolio heat entry model used by live, API, and backtest calculations.
- src/TradingApp.Application/Trading/Models/PortfolioHeatResult.cs: Added the portfolio heat result model with limit-state helpers for downstream consumers.
- src/TradingApp.Application/Trading/Services/PortfolioHeatCalculator.cs: Added the shared portfolio heat calculator for exchange positions and tracked risk values.
- tests/TradingApp.Application.Tests/Trading/Services/PortfolioHeatCalculatorTests.cs: Added calculator unit tests covering stop-loss, fallback, aggregate, and edge-case heat calculations.

<!-- Phase 3: API Endpoint -->
- src/TradingApp.Application/Trading/Models/PortfolioHeatResponse.cs: Added the API-facing portfolio heat response model and nested position entries.
- src/TradingApp.Api/Controllers/RiskController.cs: Added the authenticated portfolio heat endpoint backed by live account summary and position reads.
- tests/TradingApp.Api.Tests/Controllers/RiskControllerTests.cs: Added integration tests for portfolio heat data, empty-wallet behavior, and unauthorized access.

<!-- Phase 4: Frontend Dashboard -->
- frontend/trading-ui/src/app/core/models/portfolio-heat.model.ts: Added the shared frontend model for portfolio heat and per-position risk contributions.
- frontend/trading-ui/src/app/features/dashboard/account-summary/portfolio-heat-indicator/portfolio-heat-indicator.component.ts: Added the standalone portfolio heat indicator component with threshold logic and tooltip composition.
- frontend/trading-ui/src/app/features/dashboard/account-summary/portfolio-heat-indicator/portfolio-heat-indicator.component.html: Added the portfolio heat indicator template with progress bar, percentage display, and critical-state icon.
- frontend/trading-ui/src/app/features/dashboard/account-summary/portfolio-heat-indicator/portfolio-heat-indicator.component.scss: Added the portfolio heat indicator styling and critical-state pulse animation.

<!-- Phase 5: Backtest Heat Enforcement -->
- src/TradingApp.Application/Backtesting/Services/BacktestRiskEngine.cs: Added a backtest-specific risk engine that enforces portfolio heat and counts blocked signals.
- tests/TradingApp.Application.Tests/Backtesting/Services/BacktestRiskEngineTests.cs: Added focused tests for backtest heat enforcement, disabled mode, and tracked-risk cleanup.

### Modified

<!-- Phase 1: Configuration + Heat Calculation Core -->
- src/TradingApp.Application/StrategyAuthoring/Models/RiskLimitsConfig.cs: Added MaxPortfolioHeatPercent with a default value and XML documentation.
- src/TradingApp.Api/appsettings.json: Added the RiskLimits section with the new default portfolio heat setting for API consumers.
- src/TradingApp.Worker/appsettings.json: Extended the worker RiskLimits section with the default portfolio heat cap.

<!-- Phase 2: LiveRiskEngine Heat Enforcement -->
- src/TradingApp.Application/Abstractions/Services/IRiskEngine.cs: Added default portfolio-state and position-lifecycle tracking methods so existing implementations remain source-compatible.
- src/TradingApp.Application/Trading/Services/LiveRiskEngine.cs: Added portfolio heat state, validation checks, and signal-driven risk tracking for live enforcement.
- src/TradingApp.Application/Trading/Services/GridController.cs: Added estimatedRiskUsd to DeployGrid signals using the strategy risk configuration and stop-loss context.
- src/TradingApp.Application/Scheduling/StrategyScheduler.cs: Updated the scheduler to refresh risk-engine equity before validating candle-close signals.
- src/TradingApp.Application/Trading/Services/FillProcessor.cs: Added authoritative tracked-risk cleanup when full exit fills are processed.
- tests/TradingApp.Application.Tests/Trading/Services/LiveRiskEngineTests.cs: Expanded the risk-engine test suite to cover heat limits, disabled mode, and tracked-risk cleanup.

<!-- Phase 3: API Endpoint -->
- src/TradingApp.Api/Program.cs: Registered RiskLimitsConfig in the API container so the new controller can resolve heat thresholds from configuration.

<!-- Phase 4: Frontend Dashboard -->
- frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts: Added the API client method for retrieving portfolio heat from the new backend endpoint.
- frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.ts: Added portfolio heat as an optional account-summary input and imported the new indicator component.
- frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.html: Inserted the portfolio heat metric row into the expanded account summary metrics list.
- frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts: Added portfolio heat state and folded the new API call into the existing dashboard refresh pipeline.
- frontend/trading-ui/src/app/features/dashboard/dashboard.component.html: Passed portfolio heat data from the dashboard container into the account summary card.

<!-- Phase 5: Backtest Heat Enforcement -->
- src/TradingApp.Api/Program.cs: Switched API-hosted backtests to use BacktestRiskEngine instead of the pass-through risk engine.
- src/TradingApp.Application/Backtesting/Models/BacktestResult.cs: Added HeatBlockedSignalCount so backtest consumers can see when heat rules rejected entries.
- src/TradingApp.Application/Backtesting/Services/BacktestRunner.cs: Populated HeatBlockedSignalCount from the scoped backtest risk engine at run completion.

### Removed

## Test Results

<!-- Phase 1: Configuration + Heat Calculation Core -->
- PortfolioHeatCalculatorTests: 11/11 passed.
- Solution build: `dotnet build TradingApp.sln --no-restore` completed without build errors; existing package/deprecation warnings remain in Infrastructure and Api projects.

<!-- Phase 2: LiveRiskEngine Heat Enforcement -->
- LiveRiskEngineTests: 42/42 passed.
- Solution build: `dotnet build TradingApp.sln --no-restore` succeeded with the same pre-existing Infrastructure package warnings and Api forwarded-headers deprecation warning.

<!-- Phase 3: API Endpoint -->
- RiskControllerTests: 3/3 passed after rebuilding the Api test project.
- API build: `dotnet build src/TradingApp.Api/TradingApp.Api.csproj --no-restore` succeeded with the existing Infrastructure package warnings and Api forwarded-headers deprecation warning.
- Solution regression tests: `dotnet test TradingApp.sln --no-build --verbosity minimal` passed 1020/1020 tests.

<!-- Phase 4: Frontend Dashboard -->
- Frontend build: `npm run build` completed successfully for `frontend/trading-ui`.
- Frontend lint: `npm run lint` passed with no lint errors.

<!-- Phase 5: Backtest Heat Enforcement -->
- BacktestRiskEngineTests: 5/5 passed.
- Solution build: `dotnet build TradingApp.sln --no-restore` succeeded with the same pre-existing Infrastructure package warnings and Api forwarded-headers deprecation warning.
- Final regression tests: `dotnet test TradingApp.sln --no-build --verbosity minimal` passed 1028/1028 tests.

## Issues

<!-- Phase 1: Configuration + Heat Calculation Core -->
- None.

<!-- Phase 2: LiveRiskEngine Heat Enforcement -->
- None.

<!-- Phase 3: API Endpoint -->
- The first filtered `dotnet test --no-build` run matched 0 tests because the new test file was not yet compiled into the stale test assembly; rerunning with build resolved it.

<!-- Phase 4: Frontend Dashboard -->
- The frontend build still reports the existing global Sass deprecation warnings and the pre-existing initial bundle budget warning; no new build errors were introduced by this phase.

<!-- Phase 5: Backtest Heat Enforcement -->
- The file-based test runner again did not discover the newly added backtest test file; running `dotnet test` against the project directly with a filter executed the intended tests successfully.

## Design Decisions

<!-- Phase 1: Configuration + Heat Calculation Core -->
- Portfolio heat estimation falls back to `MarginUsed` when a position has no stop-loss, matching the phase specification's conservative risk proxy.

<!-- Phase 2: LiveRiskEngine Heat Enforcement -->
- DeployGrid heat estimation uses whole-grid notional for non-risk-based sizing so the portfolio cap reflects the aggregate exposure being introduced rather than a single ladder level.
- LiveRiskEngine records heat state on approved entry signals and removes it on explicit close signals plus exit fills, balancing early prevention with authoritative cleanup.

<!-- Phase 3: API Endpoint -->
- The API endpoint computes portfolio heat directly from exchange positions and account equity rather than engine-tracked heat so the dashboard reflects actual live account state in the Api host.

<!-- Phase 4: Frontend Dashboard -->
- The dashboard fetches portfolio heat inside the existing 2-second `forkJoin` refresh cycle so the new metric stays synchronized with account summary, positions, and orders.
- The heat indicator thresholds are expressed as a ratio of current heat to configured max heat, which keeps the UI meaningful if the configured cap changes from the default 6%.

<!-- Phase 5: Backtest Heat Enforcement -->
- BacktestRiskEngine treats TakeProfit as a tracked-risk close signal because the replay pipeline does not have the live FillProcessor callback path that removes heat state after exits.

## Review Hints

<!-- Phase 1: Configuration + Heat Calculation Core -->
- Review the API appsettings defaults carefully because this phase introduced the first `RiskLimits` section into the API host.

<!-- Phase 2: LiveRiskEngine Heat Enforcement -->
- Review the whole-grid `estimatedRiskUsd` calculation in GridController against the intended semantics of `notionalUsd` because it now represents aggregate heat impact rather than per-order size.

<!-- Phase 3: API Endpoint -->
- Review whether `PortfolioHeatResponse.Empty` should always return the configured max heat, because the dashboard will receive that value even when no wallet is connected.

<!-- Phase 4: Frontend Dashboard -->
- Review the dashboard polling cadence after adding portfolio heat because the account summary card now depends on one additional API call every refresh cycle.

<!-- Phase 5: Backtest Heat Enforcement -->
- Review whether any non-backtest API flows accidentally rely on the pass-through risk engine registration, because API-hosted backtests now resolve BacktestRiskEngine by default.

## Release Summary

Portfolio heat enforcement now applies consistently across live validation, account visibility, dashboard monitoring, and backtest replay. The implementation adds a shared heat calculator, live and backtest risk tracking, a new `GET /api/risk/portfolio-heat` endpoint, dashboard rendering, and blocked-signal reporting in backtest results while keeping the existing solution test suite green.
