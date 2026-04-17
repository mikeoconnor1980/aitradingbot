<!-- markdownlint-disable-file -->
# Release Changes: F0 - Typed Config & Execution Separation

**Related Plan**: 20260402-typed-config-separation-plan.instructions.md
**Implementation Date**: 2026-04-02

## Summary

Implements typed strategy and execution configuration separation across the trading pipeline, persistence model, API contract, and Angular backtest UI.

## Changes

### Added

<!-- Phase 1: Domain Types & Model Migration -->
- src/TradePilot.Domain/Trading/IStrategyConfig.cs: Added the new marker interface for typed strategy configs.
- src/TradePilot.Domain/Enums/OrderSide.cs: Moved OrderSide into the Domain enum namespace.
- src/TradePilot.Domain/Trading/FeeModel.cs: Moved FeeModel into Domain and kept fee/slippage behavior unchanged.
- src/TradePilot.Domain/Trading/EntryModes.cs: Added the renamed domain-level entry mode constants and validator.
- src/TradePilot.Domain/Trading/ExecutionConfig.cs: Added the new execution config record with FeeModel and Leverage.
- src/TradePilot.Domain/Trading/GridStrategyConfig.cs: Added the new domain strategy config record implementing IStrategyConfig.

<!-- Phase 3: Entity, Command, Mapper & API Contract -->
- src/TradePilot.Persistence/Migrations/20260402150658_SplitStrategyExecutionConfig.cs: Adds the new required ExecutionConfigJson column to BacktestRuns with a safe JSON default.
- src/TradePilot.Persistence/Migrations/20260402150658_SplitStrategyExecutionConfig.Designer.cs: EF Core designer snapshot for the new split-config migration.

### Modified

<!-- Phase 1: Domain Types & Model Migration -->
- src/TradePilot.Api/Controllers/BacktestsController.cs: Switched validation to EntryModes and aliased the legacy GridStrategyConfig to avoid phase-1 ambiguity.
- src/TradePilot.Api/Models/RunBacktestRequest.cs: Updated the default entry mode source to Domain EntryModes.
- src/TradePilot.Api/Services/BacktestProcessorService.cs: Updated FeeModel import and aliased the legacy GridStrategyConfig for deserialization.
- src/TradePilot.Application/Backtesting/Models/BacktestConfig.cs: Updated FeeModel reference to the Domain namespace.
- src/TradePilot.Application/Backtesting/Models/GridStrategyConfig.cs: Updated the legacy model to use EntryModes for its default value.
- src/TradePilot.Application/Backtesting/Models/BacktestTrade.cs: Updated OrderSide reference to Domain.
- src/TradePilot.Application/Backtesting/Models/SimulatedFill.cs: Updated OrderSide reference to Domain.
- src/TradePilot.Application/Backtesting/Models/SimulatedOrder.cs: Updated OrderSide reference to Domain.
- src/TradePilot.Application/Backtesting/Services/BacktestMetricsCalculator.cs: Updated OrderSide import to Domain.
- src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs: Updated FeeModel and OrderSide imports to Domain.
- src/TradePilot.Application/Backtesting/Services/SimulatedExecutionEngine.cs: Updated FeeModel and OrderSide imports to Domain.
- src/TradePilot.Application/Trading/Models/OrderRequest.cs: Updated OrderSide reference to Domain.
- src/TradePilot.Application/Trading/Services/BacktestPositionManager.cs: Replaced BacktestEntryModes with EntryModes and moved OrderSide import to Domain.
- src/TradePilot.Application/Trading/Services/GridController.cs: Replaced BacktestEntryModes with EntryModes and aliased the legacy GridStrategyConfig.
- tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs: Updated tests to use EntryModes, Domain OrderSide, and the aliased legacy GridStrategyConfig.
- tests/TradePilot.Application.Tests/Backtesting/Services/BacktestMetricsCalculatorTests.cs: Updated OrderSide import to Domain.
- tests/TradePilot.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs: Updated FeeModel and OrderSide imports to Domain.
- tests/TradePilot.Application.Tests/Backtesting/Services/CandleReplayEngineTests.cs: Updated FeeModel import to Domain.
- tests/TradePilot.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs: Updated FeeModel and OrderSide imports to Domain.
- tests/TradePilot.Application.Tests/Backtesting/Services/SimulatedExecutionEngineTests.cs: Updated FeeModel and OrderSide imports to Domain.

<!-- Phase 2: Core Pipeline Refactoring -->
- src/TradePilot.Application/Abstractions/Services/IStrategyEngine.cs: Changed EvaluateAsync to accept typed strategy config.
- src/TradePilot.Application/Abstractions/Services/IGridController.cs: Changed ProcessAsync to accept typed strategy config.
- src/TradePilot.Application/Trading/Services/GridStrategyEngine.cs: Removed JSON deserialization and added typed GridStrategyConfig guard and cast handling.
- src/TradePilot.Application/Trading/Services/GridController.cs: Removed JSON deserialization and switched lifecycle processing to typed GridStrategyConfig.
- src/TradePilot.Application/Scheduling/StrategyScheduler.cs: Replaced raw strategy-config JSON storage with typed IStrategyConfig and passed it through the pipeline.
- src/TradePilot.Application/Backtesting/Models/BacktestConfig.cs: Split backtest config into Strategy and Execution properties.
- src/TradePilot.Application/Backtesting/Services/BacktestRunner.cs: Wired typed strategy config to StrategyScheduler and execution fee model to SimulatedExecutionEngine; updated validation.
- src/TradePilot.Api/Services/BacktestProcessorService.cs: Implemented the temporary bridge from legacy single JSON config into typed strategy and execution config objects.
- tests/TradePilot.Application.Tests/Trading/Services/GridControllerTests.cs: Replaced raw strategy-config JSON with typed GridStrategyConfig test data.
- tests/TradePilot.Application.Tests/Scheduling/StrategySchedulerTests.cs: Updated constructor usage and mock expectations for typed strategy config.
- tests/TradePilot.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs: Updated BacktestConfig helpers to the new Strategy and Execution shape and replaced the obsolete invalid-JSON test.
- tests/TradePilot.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs: Replaced inline strategy JSON with typed strategy and execution config builders while preserving scenario parameters.
- tests/TradePilot.Application.Tests/Backtesting/Services/CandleReplayEngineTests.cs: Updated BacktestConfig creation to the new typed Strategy and Execution shape.

<!-- Phase 3: Entity, Command, Mapper & API Contract -->
- src/TradePilot.Api/Models/RunBacktestRequest.cs: Split the request DTO into nested StrategyConfigRequest and ExecutionConfigRequest sections.
- src/TradePilot.Api/Controllers/BacktestsController.cs: Mapped split request DTOs into domain GridStrategyConfig and ExecutionConfig and passed both into the command.
- src/TradePilot.Application/Backtesting/RunBacktestCommand.cs: Extended the command and handler to carry, serialize, and persist both config objects.
- src/TradePilot.Domain/Entities/BacktestRun.cs: Added ExecutionConfigJson and updated both factory methods to require separate strategy and execution JSON.
- src/TradePilot.Application/Backtesting/BacktestRunResponseMapper.cs: Added execution-config serialization and deserialized both JSON columns into the response.
- src/TradePilot.Application/Backtesting/Models/BacktestRunResponse.cs: Exposed separate StrategyConfig and ExecutionConfig properties using domain types.
- src/TradePilot.Api/Services/BacktestProcessorService.cs: Replaced the phase-2 bridge with final two-column deserialization into BacktestConfig.Strategy and BacktestConfig.Execution.
- src/TradePilot.Persistence/TradePilotDbContext.cs: Configured ExecutionConfigJson as a required BacktestRun column.
- src/TradePilot.Persistence/Migrations/TradePilotDbContextModelSnapshot.cs: Updated EF snapshot to include ExecutionConfigJson.
- tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs: Updated request helpers, persisted-run helpers, and assertions for the split request and response contract.
- tests/TradePilot.Persistence.Tests/Repositories/BacktestRunRepositoryTests.cs: Updated BacktestRun factory usage and assertions for the new execution JSON column.

<!-- Phase 4: Frontend -->
- frontend/trading-ui/src/app/core/models/backtest.model.ts: Split strategy and execution TypeScript contracts and updated request and result shapes.
- frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts: Emitted separate strategyConfig and executionConfig payloads, updated prefill to read executionConfig.feeModel, and expanded validation token matching.
- frontend/trading-ui/src/app/features/backtesting/backtest-compare/backtest-compare.component.ts: Switched leverage and fee/slippage comparison rows to the new executionConfig path.
- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html: Switched displayed leverage to the new executionConfig path.
- frontend/trading-ui/src/app/core/services/backtest.service.spec.ts: Updated request and response mocks for the split config contract.
- frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.spec.ts: Updated emitted payload and prefill assertions for separate executionConfig data.
- frontend/trading-ui/src/app/features/backtesting/backtest-compare/backtest-compare.component.spec.ts: Updated result mocks to include executionConfig and removed execution fields from strategyConfig.
- frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.spec.ts: Updated result mock to the new split config shape.
- frontend/trading-ui/src/app/features/backtesting/cycle-chart/cycle-chart.component.spec.ts: Removed pre-existing explicit any casts so frontend lint passes cleanly.

### Removed

<!-- Phase 1: Domain Types & Model Migration -->
- src/TradePilot.Application/Trading/Models/OrderSide.cs: Removed the old Application OrderSide definition after moving it to Domain.
- src/TradePilot.Application/Backtesting/Models/FeeModel.cs: Removed the old Application FeeModel definition after moving it to Domain.
- src/TradePilot.Application/Backtesting/Models/BacktestEntryModes.cs: Removed the old Application entry mode constants after moving and renaming them in Domain.

<!-- Phase 3: Entity, Command, Mapper & API Contract -->
- src/TradePilot.Application/Backtesting/Models/GridStrategyConfig.cs: Removed the obsolete application-layer combined config type.

## Test Results

<!-- Phase 1: Domain Types & Model Migration -->
- TradePilot.Domain.Tests: 15/15 passed
- TradePilot.Application.Tests: 79/79 passed
- TradePilot.Infrastructure.Tests: 51/51 passed
- TradePilot.Persistence.Tests: 20/20 passed
- TradePilot.Api.Tests: 145/145 passed
- Architecture Tests: N/A - no architecture tests exist in the project
- Build: dotnet build TradePilot.sln PASSED
- Full Test Suite: dotnet test TradePilot.sln --no-build PASSED

<!-- Phase 2: Core Pipeline Refactoring -->
- TradePilot.Application.Tests: 79/79 passed
- TradePilot.Domain.Tests: 15/15 passed
- TradePilot.Infrastructure.Tests: 51/51 passed
- TradePilot.Persistence.Tests: 20/20 passed
- TradePilot.Api.Tests: 145/145 passed
- Architecture Tests: N/A - no architecture tests exist in the project

<!-- Phase 3: Entity, Command, Mapper & API Contract -->
- Build: PASSED (dotnet build TradePilot.sln)
- TradePilot.Api.Tests: 145/145 passed
- TradePilot.Persistence.Tests: 20/20 passed
- TradePilot.Domain.Tests: 15/15 passed
- TradePilot.Application.Tests: 79/79 passed
- TradePilot.Infrastructure.Tests: 51/51 passed
- Full Test Suite: 310/310 passed (dotnet test TradePilot.sln --no-build)
- EF Migration Apply: PASSED (dotnet ef database update)
- Architecture Tests: N/A - no architecture test project exists in this repository

<!-- Phase 4: Frontend -->
- Angular Build: PASSED
- Angular Lint: PASSED
- Architecture Tests: N/A - no frontend architecture test target exists for this phase

## Issues

<!-- Phase 1: Domain Types & Model Migration -->
- The temporary coexistence of Application.Backtesting.Models.GridStrategyConfig and Domain.Trading.GridStrategyConfig caused ambiguity in GridController, BacktestsController, BacktestProcessorService, and related tests. Resolved with explicit aliases while keeping the legacy Application model in place for later phases.
- The host test runner reported stale build-failed summaries after the solution built cleanly. Phase verification used terminal dotnet test execution, which passed.

<!-- Phase 2: Core Pipeline Refactoring -->
- GridController initially failed to compile because CancellationReason still lives in application backtesting models during this phase; resolved by restoring the correct namespace import while keeping the domain GridStrategyConfig explicit.
- BacktestProcessorService initially had ambiguity between the legacy application GridStrategyConfig and the new domain GridStrategyConfig; resolved with an explicit alias for the domain type in the temporary bridge.

<!-- Phase 3: Entity, Command, Mapper & API Contract -->
- The host runTests tool reported stale project build failures for the API test project even though the project compiled and passed when run directly with dotnet test; final verification used direct terminal test runs.
- dotnet ef database update initially failed because the design-time factory targets src/TradePilot.Persistence/Data/TradePilot.db and that directory did not exist; creating the Data directory resolved the issue.
- The patch delete operation did not physically remove the obsolete GridStrategyConfig.cs file; it was then removed directly after the type had already been eliminated from code.

<!-- Phase 4: Frontend -->
- Angular lint initially failed due to pre-existing explicit any usage in frontend/trading-ui/src/app/features/backtesting/cycle-chart/cycle-chart.component.spec.ts; resolved by replacing the casts with typed bracket access.
- Angular build completed successfully but reported existing bundle and style budget warnings; these were non-blocking and unrelated to the typed config separation changes.

## Design Decisions

<!-- Phase 1: Domain Types & Model Migration -->
- Kept the legacy Application GridStrategyConfig in place exactly as specified for Phase 1, and only moved the new typed domain config into TradePilot.Domain.Trading.
- Used Domain EntryModes as the single source of truth immediately, while preserving existing request and serialization shapes until later phases.
- Preferred minimal import and alias updates over broader refactoring so behavior remains unchanged in this migration phase.

<!-- Phase 2: Core Pipeline Refactoring -->
- Kept the legacy application GridStrategyConfig only at the BacktestProcessorService bridge point, as specified, and used typed domain strategy config everywhere else in the pipeline.
- Preserved existing grid and backtest behavior by changing only config transport and validation shape, not the signal-generation or execution logic.

<!-- Phase 3: Entity, Command, Mapper & API Contract -->
- Set the migration default for ExecutionConfigJson to "{}" rather than an empty string so any surviving legacy rows remain valid JSON during rollout.
- Kept the new API contract aligned across request, persistence, processor, and response layers by using domain GridStrategyConfig and ExecutionConfig end to end rather than introducing another bridge DTO in Application.

<!-- Phase 4: Frontend -->
- Prefill logic currently lives in frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts rather than frontend/trading-ui/src/app/features/backtesting/backtest-page.component.ts, so the executionConfig prefill update was applied at the active prefill point while leaving page orchestration unchanged.
- Updated backtesting display components to consume executionConfig in the same phase because the new model shape removes leverage and fees from strategyConfig and would otherwise break the frontend build.

## Review Hints

<!-- Phase 1: Domain Types & Model Migration -->
- Review the temporary alias usage around the legacy GridStrategyConfig carefully; those are intentional bridge points that should disappear in Phase 3.
- Review future Phase 2 and Phase 3 changes for accidental mixing of the legacy Application GridStrategyConfig and the new Domain GridStrategyConfig, since both types currently coexist by design.

<!-- Phase 2: Core Pipeline Refactoring -->
- Review src/TradePilot.Api/Services/BacktestProcessorService.cs carefully; it is the intentional remaining place where the legacy combined config model is still deserialized and should be removed in Phase 3.
- Review the typed guards in src/TradePilot.Application/Trading/Services/GridStrategyEngine.cs and src/TradePilot.Application/Trading/Services/GridController.cs; they are the main enforcement points preventing accidental mixed strategy-config types in the shared pipeline.

<!-- Phase 3: Entity, Command, Mapper & API Contract -->
- Review the request and response contract boundary in src/TradePilot.Api/Controllers/BacktestsController.cs and src/TradePilot.Application/Backtesting/BacktestRunResponseMapper.cs; that is the main place where the new split shape is enforced and exposed.
- Review the migration rollout assumption around legacy backtest rows. The column addition is safe, but old StrategyConfigJson payloads are still historical single-object data unless cleaned beforehand as the plan notes.

<!-- Phase 4: Frontend -->
- Review the request and response contract boundary in frontend/trading-ui/src/app/core/models/backtest.model.ts and frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts; that is where the request-specific flat execution shape and response-specific nested feeModel shape intentionally diverge.
- Review the config echo and comparison paths in frontend/trading-ui/src/app/features/backtesting/backtest-compare/backtest-compare.component.ts and frontend/trading-ui/src/app/features/backtesting/backtest-result/backtest-result.component.html; those are the main UI surfaces now reading executionConfig.

## Release Summary

Implemented all four phases of the typed config separation refactor. The trading pipeline now accepts typed strategy configs instead of raw JSON, backtest execution concerns are isolated in ExecutionConfig, BacktestRun persists separate strategy and execution JSON payloads, the API contract now uses nested strategy and execution sections, and the Angular backtest UI has been updated to send and display the split shape. Solution build, full backend test suite, EF migration apply, Angular build, and Angular lint all passed during implementation.
