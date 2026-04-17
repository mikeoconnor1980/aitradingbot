<!-- markdownlint-disable-file -->
# Release Changes: F1 - Extensible Strategy Schema (v1 - Grid)

**Related Plan**: 20260402-extensible-strategy-schema-plan.instructions.md
**Implementation Date**: 2026-04-02

## Summary

Implements the extensible strategy schema, validation pipeline, and consumer migration from the legacy grid-only config.

## Changes

### Added

<!-- Phase 1: Foundation — Schema Models, Enums & JSON Serialization -->
- src/TradePilot.Application/StrategyAuthoring/Models/StrategyMode.cs: Added the root strategy mode enum for grid and signal schemas.
- src/TradePilot.Application/StrategyAuthoring/Models/Direction.cs: Added the shared trading direction enum for strategy configs.
- src/TradePilot.Application/StrategyAuthoring/Models/TrendFilterType.cs: Added the trend filter discriminator enum for schema composition.
- src/TradePilot.Application/StrategyAuthoring/Models/TrendOperator.cs: Added comparison operators used by trend filter rules.
- src/TradePilot.Application/StrategyAuthoring/Models/EntryConditionType.cs: Added the entry condition discriminator enum with an unknown fallback value.
- src/TradePilot.Application/StrategyAuthoring/Models/EntryLogic.cs: Added the enum that controls all/any entry-condition evaluation semantics.
- src/TradePilot.Application/StrategyAuthoring/Models/ExitRuleType.cs: Added the enum for fixed-percent and swing-low exit rules.
- src/TradePilot.Application/StrategyAuthoring/Models/PositionSizeType.cs: Added the enum for percent-wallet and fixed-notional position sizing.
- src/TradePilot.Application/StrategyAuthoring/Models/CooldownUnit.cs: Added the enum for cooldown unit handling.
- src/TradePilot.Application/StrategyAuthoring/Models/StrategyEntryPoint.cs: Added the enum used to tag strategy source metadata.
- src/TradePilot.Application/StrategyAuthoring/Models/GridConfig.cs: Added the grid subsection model for the extensible strategy schema.
- src/TradePilot.Application/StrategyAuthoring/Models/TrendFilterConfig.cs: Added the trend filter subsection model.
- src/TradePilot.Application/StrategyAuthoring/Models/ExitRuleConfig.cs: Added the flat exit-rule subsection model with value and lookback support.
- src/TradePilot.Application/StrategyAuthoring/Models/ExitConfig.cs: Added the exit subsection model with take-profit and stop-loss rules.
- src/TradePilot.Application/StrategyAuthoring/Models/RiskConfig.cs: Added the risk subsection model including leverage and cooldown settings.
- src/TradePilot.Application/StrategyAuthoring/Models/StrategyMetadata.cs: Added optional strategy metadata storage for tags and notes.
- src/TradePilot.Application/StrategyAuthoring/Models/SourceMetadata.cs: Added optional strategy source metadata for UI and migration tracking.
- src/TradePilot.Application/StrategyAuthoring/Models/IEntryConditionParams.cs: Added the marker interface for typed entry-condition parameter models.
- src/TradePilot.Application/StrategyAuthoring/Models/RsiParams.cs: Added the typed RSI parameter model.
- src/TradePilot.Application/StrategyAuthoring/Models/PriceVsEmaParams.cs: Added the typed price-vs-EMA parameter model.
- src/TradePilot.Application/StrategyAuthoring/Models/MacdParams.cs: Added the typed MACD parameter model.
- src/TradePilot.Application/StrategyAuthoring/Models/UnknownConditionParams.cs: Added the fallback parameter model for unknown condition payloads.
- src/TradePilot.Application/StrategyAuthoring/Models/EntryConditionConfig.cs: Added the entry-condition wrapper model with typed params support.
- src/TradePilot.Application/StrategyAuthoring/Models/StrategyConfig.cs: Added the main extensible strategy config model implementing IStrategyConfig.
- src/TradePilot.Application/StrategyAuthoring/Serialization/EntryConditionParamsConverter.cs: Added the helper converter for typed entry-condition parameter deserialization.
- src/TradePilot.Application/StrategyAuthoring/Serialization/EntryConditionConfigConverter.cs: Added the discriminator-based converter for entry-condition config payloads.
- src/TradePilot.Application/StrategyAuthoring/Serialization/StrategyJsonOptions.cs: Added the shared strategy JSON serializer options with camelCase and snake_case enum handling.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Models/StrategyConfigSerializationTests.cs: Added round-trip tests for grid and signal strategy config JSON serialization.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Serialization/EntryConditionParamsConverterTests.cs: Added tests for typed entry-condition parameter conversion and unknown-condition fallback handling.

<!-- Phase 2: Validation Pipeline & API Endpoint -->
- src/TradePilot.Application/StrategyAuthoring/Validation/ValidationSeverity.cs: Added validation severity enum for error, warning, and info messages.
- src/TradePilot.Application/StrategyAuthoring/Validation/ValidationError.cs: Added the validation message record used across all validator levels.
- src/TradePilot.Application/StrategyAuthoring/Validation/ValidationResult.cs: Added the aggregate validation result model with grouped projections and validity state.
- src/TradePilot.Application/StrategyAuthoring/Validation/IStrategyValidator.cs: Added the strategy validation contract for StrategyConfig.
- src/TradePilot.Application/StrategyAuthoring/Validation/SchemaValidator.cs: Added Level 1 schema validation for required top-level fields.
- src/TradePilot.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs: Added Level 2 business-rule validation for grid, exit, risk, entry-condition, and trend-filter rules.
- src/TradePilot.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs: Added Level 3 cross-field validation and v1 informational messages.
- src/TradePilot.Application/StrategyAuthoring/Validation/CompositeStrategyValidator.cs: Added the composite validator that runs all three validation levels.
- src/TradePilot.Api/Controllers/StrategiesController.cs: Added the new validation endpoint at POST /api/strategies/validate.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/SchemaValidatorTests.cs: Added unit tests for Level 1 validation.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs: Added unit tests for Level 2 validation.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/CrossFieldValidatorTests.cs: Added unit tests for Level 3 validation.
- tests/TradePilot.Application.Tests/StrategyAuthoring/Validation/CompositeStrategyValidatorTests.cs: Added unit tests for the composite validator.
- tests/TradePilot.Api.Tests/Controllers/StrategiesControllerTests.cs: Added API tests for the strategy validation endpoint.

<!-- Phase 3: Consumer Migration & Domain Cleanup -->
- src/TradePilot.Persistence/Migrations/20260402183000_CleanOldBacktestData.cs: Added the EF migration that clears incompatible historical backtest rows.

### Modified

<!-- Phase 2: Validation Pipeline & API Endpoint -->
- src/TradePilot.Api/Program.cs: Registered validation services in DI and enabled global camelCase plus snake_case enum JSON handling for controller bodies.
- tests/TradePilot.Api.Tests/Infrastructure/BaseControllerTests.cs: Aligned API test JSON serialization and deserialization with the API's global JSON enum policy so existing controller tests continue to pass.

<!-- Phase 3: Consumer Migration & Domain Cleanup -->
- src/TradePilot.Domain/Trading/ExecutionConfig.cs: Removed leverage so execution config now contains only fee settings.
- src/TradePilot.Application/Trading/Services/GridStrategyEngine.cs: Switched runtime casting and property access to the nested StrategyConfig schema.
- src/TradePilot.Application/Trading/Services/GridController.cs: Migrated grid, exit, and risk reads to StrategyConfig.Grid, StrategyConfig.Exit, and StrategyConfig.Risk.
- src/TradePilot.Application/Backtesting/BacktestRunResponseMapper.cs: Switched strategy serialization and deserialization to StrategyConfig and shared JSON options.
- src/TradePilot.Application/Backtesting/RunBacktestCommand.cs: Updated command contract to carry StrategyConfig with separate ExecutionConfig.
- src/TradePilot.Application/Backtesting/Models/BacktestRunResponse.cs: Exposed StrategyConfig instead of the removed GridStrategyConfig type.
- src/TradePilot.Application/Backtesting/GetBacktestDebugQuery.cs: Fixed audit-log deserialization to use shared snake_case-aware JSON options.
- src/TradePilot.Api/Services/BacktestProcessorService.cs: Deserializes StrategyConfig and ExecutionConfig with shared JSON options.
- src/TradePilot.Api/Models/RunBacktestRequest.cs: Replaced the flat request DTO with nested strategy, grid, exit, risk, and execution request models.
- src/TradePilot.Api/Controllers/BacktestsController.cs: Mapped nested request DTOs into StrategyConfig and fee-only ExecutionConfig, with entry-mode normalization and updated validation.
- tests/TradePilot.Application.Tests/Trading/Services/GridControllerTests.cs: Replaced legacy config fixtures with nested StrategyConfig fixtures.
- tests/TradePilot.Application.Tests/Scheduling/StrategySchedulerTests.cs: Updated scheduler tests to pass typed StrategyConfig instances.
- tests/TradePilot.Application.Tests/Backtesting/Services/BacktestRunnerTests.cs: Updated helpers and assertions for StrategyConfig plus fee-only ExecutionConfig.
- tests/TradePilot.Application.Tests/Backtesting/Services/CandleReplayEngineTests.cs: Updated backtest config setup to the new typed strategy schema.
- tests/TradePilot.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs: Replaced legacy config JSON with nested StrategyConfig builders and fee-only execution config builders.
- tests/TradePilot.Persistence.Tests/Repositories/BacktestRunRepositoryTests.cs: Updated persisted strategy and execution JSON fixtures to the new schema shapes.
- tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs: Migrated request payloads, persisted fixtures, and response assertions to the new nested schema.
- tests/TradePilot.Api.Tests/Infrastructure/BaseControllerTests.cs: Uses API-matching JSON options for snake_case enum serialization and deserialization.

### Removed

<!-- Phase 3: Consumer Migration & Domain Cleanup -->
- src/TradePilot.Domain/Trading/GridStrategyConfig.cs: Removed the obsolete legacy grid-only strategy type after all backend consumers migrated to StrategyConfig.

## Test Results

<!-- Phase 1: Foundation — Schema Models, Enums & JSON Serialization -->
- TradePilot.Application.Tests: 87/87 passed via `dotnet test tests/TradePilot.Application.Tests/TradePilot.Application.Tests.csproj -v minimal`

<!-- Phase 2: Validation Pipeline & API Endpoint -->
- TradePilot.Application.Tests: 99/99 passed
- TradePilot.Api.Tests: 148/148 passed
- Build: PASSED via `dotnet build TradePilot.sln`
- Architecture Tests: N/A - no architecture test project exists in this repository

<!-- Phase 3: Consumer Migration & Domain Cleanup -->
- TradePilot.Domain.Tests: passed
- TradePilot.Application.Tests: passed
- TradePilot.Persistence.Tests: passed
- TradePilot.Infrastructure.Tests: passed
- TradePilot.Api.Tests: 148/148 passed
- Full backend suite: 333/333 passed
- Architecture Tests: N/A - no architecture test project exists in this repository

## Issues

<!-- Phase 1: Foundation — Schema Models, Enums & JSON Serialization -->
- The first shared-terminal test run was interrupted before completion; rerunning the targeted application test project completed successfully.

<!-- Phase 2: Validation Pipeline & API Endpoint -->
- Solution build and API test build initially failed because a running TradePilot.Api process was locking output assemblies. I stopped the process and reran verification successfully.
- TradePilot.Api.Tests initially had one failure in BacktestsControllerTests because the new global snake_case enum JSON policy changed response serialization, but the shared API test helper still deserialized with default options. I fixed the helper to use matching JSON options and reran the suite successfully.

<!-- Phase 3: Consumer Migration & Domain Cleanup -->
- The patch-based delete operation reported success but did not physically remove src/TradePilot.Domain/Trading/GridStrategyConfig.cs in this workspace. I removed it directly from disk and then rebuilt.
- API tests initially failed because response and audit-log enum payloads now use the global snake_case enum policy. I fixed test deserialization to use the shared API JSON options and updated GetBacktestDebugQuery to deserialize audit logs with StrategyJsonOptions.Default.
- A targeted `dotnet test --no-build` re-run initially exercised stale binaries after those fixes. Rebuilding the solution resolved that and the subsequent targeted and full test runs passed.

## Design Decisions

<!-- Phase 1: Foundation — Schema Models, Enums & JSON Serialization -->
- Added `EntryConditionType.Unknown` so the schema can preserve unsupported future condition payloads without failing deserialization during round-trip tests.
- Kept Phase 1 additive only: no existing strategy consumers were changed yet, and the new serialization layer is isolated under `StrategyAuthoring` until validation and migration phases land.

<!-- Phase 2: Validation Pipeline & API Endpoint -->
- Kept the existing Phase 2 implementation already present in the workspace and verified it against the phase details instead of rewriting equivalent code.
- Fixed the JSON policy regression at the shared API test helper layer rather than weakening the API's global enum serialization settings, because the new validate endpoint depends on that contract.

<!-- Phase 3: Consumer Migration & Domain Cleanup -->
- Kept the Phase 2 global controller JSON configuration as the source of truth for Task 3.10 and aligned tests and debug-log deserialization to that contract instead of weakening enum serialization behavior.
- Normalized request `grid.entryMode` values in BacktestsController so schema-style values like `wait_for_limit_price` continue to work with the existing runtime `EntryModes` constants without broader runtime churn.
- Used the shared StrategyJsonOptions for strategy and audit-log serialization paths to keep enum handling and property casing consistent across request, persistence, and response boundaries.

## Review Hints

<!-- Phase 1: Foundation — Schema Models, Enums & JSON Serialization -->
- Review the new entry-condition converter pair under `src/TradePilot.Application/StrategyAuthoring/Serialization/` first; that is the core extensibility point for future condition types.
- Review the default string values in the new schema models, especially `GridConfig.EntryMode` and `StrategyConfig.Exchange`, because later migration code will depend on those defaults matching the intended API contract.

<!-- Phase 2: Validation Pipeline & API Endpoint -->
- Review `src/TradePilot.Application/StrategyAuthoring/Validation/CompositeStrategyValidator.cs` and the three validator classes together first; that is the core Phase 2 behavior.
- Review `src/TradePilot.Api/Program.cs` and `tests/TradePilot.Api.Tests/Infrastructure/BaseControllerTests.cs` together; they now intentionally share the same camelCase plus snake_case enum JSON contract.

<!-- Phase 3: Consumer Migration & Domain Cleanup -->
- Review src/TradePilot.Api/Controllers/BacktestsController.cs first. That is the main boundary where the nested request DTO is converted into the executable StrategyConfig shape.
- Review src/TradePilot.Application/Trading/Services/GridController.cs and src/TradePilot.Application/Trading/Services/GridStrategyEngine.cs together. Those are the core runtime consumers whose property-path migration drives behavior.
- Review tests/TradePilot.Api.Tests/Controllers/BacktestsControllerTests.cs and src/TradePilot.Application/Backtesting/GetBacktestDebugQuery.cs together for the JSON-contract changes around snake_case enums and persisted audit-log deserialization.

## Release Summary

Implemented all three phases of F1. The backend now uses the extensible StrategyConfig schema end to end, validates configs through a three-level pipeline with a dedicated validation endpoint, stores and rehydrates the new schema consistently, and removes the legacy grid-only domain config. Full backend build and test verification passed with 333/333 tests green.
