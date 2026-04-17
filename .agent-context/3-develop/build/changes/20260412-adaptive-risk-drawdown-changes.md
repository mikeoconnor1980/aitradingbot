<!-- markdownlint-disable-file -->
# Release Changes: Adaptive Risk (Drawdown-Adjusted)

**Related Plan**: 20260412-adaptive-risk-drawdown-plan.instructions.md
**Implementation Date**: 2026-04-12

## Summary

All four phases are complete for adaptive risk drawdown scaling, including configuration validation, HWM persistence, live and backtest drawdown enforcement, blocked-signal reporting, and dashboard visibility.

## Changes

### Added

<!-- Phase 1: Configuration & Domain Model -->
- src/TradePilot.Application/StrategyAuthoring/Models/DrawdownTier.cs: Created the drawdown tier model used for adaptive risk scaling thresholds.
- src/TradePilot.Application/StrategyAuthoring/Validation/RiskLimitsConfigValidator.cs: Added startup validation for drawdown tier ordering and range rules.
- src/TradePilot.Application/StrategyAuthoring/Validation/RiskLimitsConfigServiceCollectionExtensions.cs: Added a shared helper to apply RiskLimits defaults after host binding.
- src/TradePilot.Persistence/Migrations/20260412203117_AddHighWaterMarkToStrategy.cs: Added the SQL Server migration for Strategy.HighWaterMarkUsd.
- src/TradePilot.Persistence/Migrations/20260412203117_AddHighWaterMarkToStrategy.Designer.cs: Added the EF migration designer snapshot for the new strategy column.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/RiskLimitsConfigValidatorTests.cs: Added validation coverage for drawdown tier rules and defaults.

<!-- Phase 2: Drawdown Tracking & Risk Engine Integration -->
- src/TradePilot.Application/Trading/Services/DrawdownEvaluator.cs: Added the pure drawdown and high-water-mark evaluator used by live and backtest scheduling flows.
- tests/TradePilot.Application.Tests/Trading/Services/DrawdownEvaluatorTests.cs: Added boundary and recovery coverage for drawdown tier evaluation.

<!-- Phase 4: API Endpoint & Frontend Dashboard -->
- src/TradePilot.Application/Trading/Models/DrawdownStateResponse.cs: Added the API response DTO for current drawdown state with an empty-state helper.
- frontend/trading-ui/src/app/core/models/drawdown-state.model.ts: Added the frontend drawdown-state model matching the backend DTO shape.
- frontend/trading-ui/src/app/features/dashboard/account-summary/drawdown-indicator/drawdown-indicator.component.ts: Added the standalone drawdown indicator component logic and threshold mapping.
- frontend/trading-ui/src/app/features/dashboard/account-summary/drawdown-indicator/drawdown-indicator.component.html: Added the drawdown indicator template with progress bar and circuit-breaker badge.
- frontend/trading-ui/src/app/features/dashboard/account-summary/drawdown-indicator/drawdown-indicator.component.scss: Added styling for drawdown tiers and halted-state pulse animation.

### Modified

<!-- Phase 1: Configuration & Domain Model -->
- src/TradePilot.Application/StrategyAuthoring/Models/RiskLimitsConfig.cs: Added drawdown tier support and centralized default tier definitions.
- src/TradePilot.Api/Program.cs: Registered RiskLimits validation and host-side default application for bound drawdown tiers.
- src/TradePilot.Worker/Program.cs: Registered RiskLimits validation and host-side default application for bound drawdown tiers.
- src/TradePilot.Domain/Entities/Strategy.cs: Added HighWaterMarkUsd persistence state and an encapsulated update method.
- src/TradePilot.Persistence/TradePilotDbContext.cs: Mapped Strategy.HighWaterMarkUsd with EF conversion support.
- src/TradePilot.Persistence/PersistenceServiceExtensions.cs: Extended SQLite startup schema drift handling to add missing columns for existing local databases.
- src/TradePilot.Persistence/Migrations/TradePilotDbContextModelSnapshot.cs: Updated the EF model snapshot for the new strategy field.
- src/TradePilot.Api/appsettings.json: Added default DrawdownTiers under RiskLimits.
- src/TradePilot.Worker/appsettings.json: Added default DrawdownTiers under RiskLimits.
- tests/TradePilot.Domain.Tests/Entities/StrategyTests.cs: Added domain coverage for high-water-mark updates.

<!-- Phase 2: Drawdown Tracking & Risk Engine Integration -->
- src/TradePilot.Application/Abstractions/Services/IRiskEngine.cs: Extended the risk engine contract with backward-compatible drawdown state members.
- src/TradePilot.Application/Trading/Services/LiveRiskEngine.cs: Added drawdown circuit breaker tracking and validation behavior for entry-blocking while allowing risk-reducing signals.
- src/TradePilot.Application/Trading/Models/MarketContext.cs: Added DrawdownScalingFactor so controllers can apply adaptive risk overlays.
- src/TradePilot.Application/Scheduling/StrategyScheduler.cs: Evaluates drawdown each cycle, updates risk state, applies scaling to market context, tracks HWM in memory, and persists HWM when strategy persistence context is available.
- src/TradePilot.Application/Trading/Services/GridController.cs: Applied drawdown scaling to resolved grid notionals and estimated risk.
- src/TradePilot.Application/Trading/Services/SignalController.cs: Applied drawdown scaling to resolved entry notionals.
- src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs: Passed drawdown tiers into the scheduler and kept the new risk-limits dependency backward-compatible.
- src/TradePilot.Worker/Services/TradingSession.cs: Passed configured drawdown tiers into live scheduler creation.
- src/TradePilot.Worker/Services/AgentCheckInService.cs: Forwarded risk-limits configuration into trading session construction.
- tests/TradePilot.Application.Tests/Trading/Services/LiveRiskEngineTests.cs: Added drawdown state and circuit breaker coverage.
- tests/TradePilot.Application.Tests/Scheduling/StrategySchedulerTests.cs: Added scheduler drawdown propagation and HWM persistence coverage.
- tests/TradePilot.Application.Tests/Trading/Services/GridControllerTests.cs: Added drawdown scaling coverage for grid sizing.
- tests/TradePilot.Application.Tests/Trading/Services/SignalControllerTests.cs: Added drawdown scaling coverage for signal sizing.

<!-- Phase 3: Backtest Support -->
- src/TradePilot.Application/Backtesting/Services/BacktestRiskEngine.cs: Added in-memory high-water-mark tracking, drawdown tier evaluation, drawdown circuit-breaker state, and drawdown-blocked signal counting.
- src/TradePilot.Application/Backtesting/Models/BacktestResult.cs: Added drawdown-blocked signal count to runtime backtest results.
- src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs: Propagated drawdown-blocked signal metrics from the backtest risk engine into the final result.
- tests/TradePilot.Application.Tests/Backtesting/Services/BacktestRiskEngineTests.cs: Added coverage for halt-tier blocking, recovery, risk-reducing passthrough, and HWM ratcheting behavior.
- tests/TradePilot.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs: Added runner-level coverage proving drawdown-blocked signals are reported during a replay.

<!-- Phase 4: API Endpoint & Frontend Dashboard -->
- src/TradePilot.Application/Trading/Services/DrawdownEvaluator.cs: Made the evaluator public so the API can reuse the existing drawdown calculation instead of duplicating logic.
- src/TradePilot.Api/Controllers/RiskController.cs: Added the drawdown-state endpoint and on-demand drawdown calculation using persisted strategy HWM plus live account equity.
- frontend/trading-ui/src/app/core/services/hyperliquid-api.service.ts: Added the drawdown-state API method.
- frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.ts: Added drawdown input wiring and imported the new indicator component.
- frontend/trading-ui/src/app/features/dashboard/account-summary/account-summary.component.html: Rendered the drawdown indicator inside the account-summary metrics grid.
- frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts: Added drawdown polling into the existing dashboard refresh batch and propagated the result to the summary card.
- frontend/trading-ui/src/app/features/dashboard/dashboard.component.html: Passed drawdown state into the account-summary component.

### Removed

<!-- Phase 1: Configuration & Domain Model -->
- None.

## Test Results

<!-- Phase 1: Configuration & Domain Model -->
- TradePilot.Domain.Tests: 84/84 passed.
- TradePilot.Indicators.Tests: 59/59 passed.
- TradePilot.Application.Tests: 528/528 passed.
- TradePilot.AI.Tests: 42/42 passed.
- TradePilot.Infrastructure.Tests: 80/80 passed.
- TradePilot.Persistence.Tests: 34/34 passed.
- TradePilot.Worker.Tests: 24/24 passed.
- TradePilot.Api.Tests: 210/210 passed.
- Architecture Tests: No dedicated architecture test project was present in the workspace; full solution build passed and all test projects were verified individually.

<!-- Phase 2: Drawdown Tracking & Risk Engine Integration -->
- TradePilot.Application.Tests: 546/546 passed.
- Architecture Tests: PASSED using dotnet build TradePilot.sln --no-restore --verbosity minimal because no dedicated architecture test project exists in this repo.

<!-- Phase 3: Backtest Support -->
- BacktestRiskEngineTests and BacktestRunnerTests: 47/47 passed.
- Full Solution Tests: 1084/1084 passed.
- Architecture Tests: PASSED using dotnet build TradePilot.sln --no-restore --verbosity minimal because no dedicated architecture test project exists in this repo.

<!-- Phase 4: API Endpoint & Frontend Dashboard -->
- TradePilot.Api build: PASSED.
- Frontend Build: PASSED.
- Frontend Lint: PASSED.
- Static file error check: PASSED.
- Architecture Tests: Not run because they were not part of Phase 4.

## Issues

<!-- Phase 1: Configuration & Domain Model -->
- Options binding merged in-model drawdown tier defaults with appsettings tiers, causing duplicate-threshold validation failures; resolved by binding from an empty collection and applying defaults in host post-configuration.
- Solution-level test orchestration intermittently aborted with testhost crashes or locked outputs; resolved by running projects individually and clearing stale testhost processes before final verification.
- Final build verification produced transient copy-retry warnings when a Worker testhost still had outputs open, but the build completed successfully after retries.

<!-- Phase 2: Drawdown Tracking & Risk Engine Integration -->
- StrategyScheduler initially failed to compile because DrawdownEvaluator was referenced without the TradePilot.Application.Trading.Services import; resolved by adding the missing using.
- BacktestRunner constructor changes initially broke existing positional test instantiations; resolved by making the new risk-limits dependency optional with a safe fallback.

<!-- Phase 3: Backtest Support -->
- The new runner integration test initially failed to compile because tests/TradePilot.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs was missing the TradePilot.Application.Trading.Services import needed for BacktestPositionManager; resolved by adding the missing using.
- Full solution build completed with pre-existing warnings only: NU1901 and NU1902 package vulnerability warnings in Infrastructure projects, plus ASPDEPR005 in src/TradePilot.Api/Program.cs.

<!-- Phase 4: API Endpoint & Frontend Dashboard -->
- DrawdownEvaluator was internal, which blocked reuse from the API project; resolved by making it public so the endpoint uses the same drawdown logic as the rest of the application.
- The phase detail sample referenced API-side live risk state and helper shapes that do not exist in this repo; resolved by computing drawdown from GetAccountSummaryAsync equity plus persisted strategy HWM through existing repositories and services.
- Frontend build completed with pre-existing Angular Sass deprecation warnings from frontend/trading-ui/src/styles.scss; no Phase 4 build errors were present.
- API build completed with pre-existing warnings: NU1901 and NU1902 package vulnerability warnings in Infrastructure and ASPDEPR005 in src/TradePilot.Api/Program.cs.

## Design Decisions

<!-- Phase 1: Configuration & Domain Model -->
- Added strict drawdown tier validation instead of silently deduplicating or reordering config values so invalid configuration fails fast at startup.
- Applied RiskLimits defaults in host post-configuration instead of relying on initialized collection defaults because the configuration binder appended configured tiers onto in-type defaults.
- Extended SQLite startup schema reconciliation to add missing columns for existing local databases because local development uses EnsureCreated and would not otherwise receive the new HighWaterMarkUsd column.

<!-- Phase 2: Drawdown Tracking & Risk Engine Integration -->
- Kept drawdown evaluation in a static utility to match the existing calculator pattern and avoid DI lifetime complexity.
- Used in-memory HWM tracking inside StrategyScheduler so adaptive drawdown behavior works even when only StrategyConfig is available at runtime.
- Persisted HWM only when a Strategy aggregate and repository context are supplied, which preserves existing worker and backtest construction patterns without widening unrelated APIs.
- Made BacktestRunner's new risk-limits dependency optional to preserve existing callers and tests while still enabling drawdown tier propagation.

<!-- Phase 3: Backtest Support -->
- Backtest drawdown blocking was enforced independently of portfolio heat so entry signals are still halted correctly even when MaxPortfolioHeatPercent is disabled.
- The drawdown-blocked metric was added to the runtime BacktestResult only, matching the phase scope without widening persistence or API read-model storage in this phase.

<!-- Phase 4: API Endpoint & Frontend Dashboard -->
- Computed drawdown state on demand in the API from current account equity plus persisted strategy high-water mark because the API host registers a backtest risk engine and does not share the worker's live in-memory risk state.
- Reused the existing controller pattern of returning an empty 200 OK response when no wallet is configured, matching the current account and risk endpoints instead of introducing a new API behavior just for drawdown.
- Kept dashboard polling inside the existing forkJoin refresh loop so drawdown state stays synchronized with account summary and portfolio heat without adding a second polling mechanism.
- Used the active strategy list ordered by repository behavior and selected the first available high-water mark, which is the minimal change consistent with current strategy access patterns.

## Review Hints

- Review src/TradePilot.Persistence/PersistenceServiceExtensions.cs closely, especially the SQLite missing-column logic and type mapping assumptions.
- Review src/TradePilot.Api/Program.cs and src/TradePilot.Worker/Program.cs to confirm the host-level RiskLimits defaulting behavior matches the intended configuration model.
- Existing repo warnings remain outside this phase: NU1901 and NU1902 package vulnerability warnings in Infrastructure projects, and ASPDEPR005 in src/TradePilot.Api/Program.cs.
- Review src/TradePilot.Application/Scheduling/StrategyScheduler.cs closely, especially the optional Strategy and repository flow versus the always-available in-memory HWM path.
- Review the controller sizing changes to confirm applying the drawdown overlay after PositionSizeResolver matches the intended risk model.
- Review the LiveRiskEngine validation order to confirm daily-loss and drawdown circuit breakers remain independent and preserve risk-reducing signal passthrough.
- Review src/TradePilot.Application/Backtesting/Services/BacktestRiskEngine.cs closely for validation order: risk-reducing signals should continue to pass while drawdown-halted entries are blocked before heat checks.
- Review tests/TradePilot.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs to confirm the replay scenario matches the intended acceptance criterion for signals skipped during a halt-tier backtest drawdown.
- Review src/TradePilot.Api/Controllers/RiskController.cs closely, especially the assumption that the first active strategy with a stored HWM is the correct source for dashboard drawdown state.
- Review frontend/trading-ui/src/app/features/dashboard/account-summary/drawdown-indicator/drawdown-indicator.component.scss for visual token alignment, especially the custom critical-tier color and halted pulse treatment.
- Review frontend/trading-ui/src/app/features/dashboard/dashboard.component.ts to confirm the extra drawdown request is acceptable within the current 2-second polling cadence.

## Release Summary

Adaptive drawdown risk scaling is now implemented end to end. The system validates and loads configurable drawdown tiers, persists per-strategy high-water marks, reduces position sizing by tier during drawdowns, halts new entries when the halt tier is reached, mirrors the same logic in backtests with blocked-signal reporting, and exposes the current drawdown state through the API and Angular dashboard.
