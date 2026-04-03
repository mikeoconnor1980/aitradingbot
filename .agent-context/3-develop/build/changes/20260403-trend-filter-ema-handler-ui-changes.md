<!-- markdownlint-disable-file -->
# Release Changes: F7 - Trend Filter + EMA Condition Handler + UI

**Related Plan**: 20260403-trend-filter-ema-handler-ui-plan.instructions.md
**Implementation Date**: 2026-04-03

## Summary

Implements F7 across backend trend filter evaluation, price-vs-EMA condition handling, and signal mode UI updates.

## Changes

### Added

<!-- Phase 1: Backend Models & Infrastructure -->
- None.

<!-- Phase 2: TrendFilterEvaluator & PriceVsEmaConditionHandler -->
- src/TradingApp.Application/StrategyAuthoring/Models/TrendFilterResult.cs: Added the trend-filter evaluation result model with pass and fail factory helpers.
- src/TradingApp.Application/StrategyAuthoring/Services/ITrendFilterEvaluator.cs: Added the trend-filter evaluator contract for signal-mode orchestration.
- src/TradingApp.Application/StrategyAuthoring/Services/TrendFilterEvaluator.cs: Implemented EMA-cross, SMA-cross, and price-vs-EMA trend-filter evaluation.
- src/TradingApp.Application/StrategyAuthoring/Services/PriceVsEmaConditionHandler.cs: Implemented price-vs-EMA condition handling for near, above, below, touch, and cross operators.
- tests/TradingApp.Application.Tests/StrategyAuthoring/Services/TrendFilterEvaluatorTests.cs: Added unit coverage for trend-filter evaluation paths and fail-closed behavior.
- tests/TradingApp.Application.Tests/StrategyAuthoring/Services/PriceVsEmaConditionHandlerTests.cs: Added unit coverage for price-vs-EMA operators and failure paths.

<!-- Phase 3: Frontend Trend Filter Card & Models -->
- frontend/trading-ui/src/app/features/strategy-builder/enums/trend-filter-operator.enum.ts: Added trend-filter operator options and display-name helpers for the new card.

<!-- Phase 4: Frontend Price vs EMA Condition & EMA Pullback Template -->
- frontend/trading-ui/src/app/features/strategy-builder/components/price-vs-ema-condition-item/price-vs-ema-condition-item.component.ts: Added the standalone Price vs EMA condition editor with operator-driven distance-field enable and disable behavior.
- frontend/trading-ui/src/app/features/strategy-builder/components/price-vs-ema-condition-item/price-vs-ema-condition-item.component.html: Added the Price vs EMA condition form UI and actions.
- frontend/trading-ui/src/app/features/strategy-builder/components/price-vs-ema-condition-item/price-vs-ema-condition-item.component.scss: Added styling aligned with the existing condition item card layout.
- frontend/trading-ui/src/app/features/strategy-builder/enums/price-vs-ema-operator.enum.ts: Added the Price vs EMA operator options used by the new component.

### Modified

<!-- Phase 1: Backend Models & Infrastructure -->
- src/TradingApp.Application/StrategyAuthoring/Models/TrendFilterType.cs: Added SmaCross and PriceAboveEma trend filter types.
- src/TradingApp.Application/StrategyAuthoring/Models/TrendOperator.cs: Added cross and directional operators needed by new trend filters.
- src/TradingApp.Application/StrategyAuthoring/Models/TrendFilterConfig.cs: Added nullable Period support for single-period trend filters.
- src/TradingApp.Application/StrategyAuthoring/Models/PriceVsEmaParams.cs: Added DistanceType and DistanceValue fields for near-EMA conditions.
- src/TradingApp.Application/Trading/Models/IndicatorContext.cs: Added SMA current and previous value storage and retrieval methods.
- src/TradingApp.Application/Trading/Services/BacktestMarketContextBuilder.cs: Added SMA indicator requirement handling and current and previous SMA calculations.
- src/TradingApp.Application/StrategyAuthoring/Services/IndicatorExtractor.cs: Extended extraction to include enabled trend filter EMA and SMA requirements with deduplication.
- src/TradingApp.Application/StrategyAuthoring/Validation/BusinessRuleValidator.cs: Added conditional trend filter validation and near-operator distance validation for PriceVsEmaParams.
- tests/TradingApp.Application.Tests/Trading/Models/IndicatorContextTests.cs: Added SMA indicator context coverage.
- tests/TradingApp.Application.Tests/StrategyAuthoring/Services/IndicatorExtractorTests.cs: Added trend filter extraction and deduplication coverage.
- tests/TradingApp.Application.Tests/StrategyAuthoring/Validation/BusinessRuleValidatorTests.cs: Added PriceAboveEma and PriceVsEma near-operator validation coverage.
- tests/TradingApp.Application.Tests/Trading/Services/BacktestMarketContextBuilderIndicatorTests.cs: Added SMA population coverage for backtest indicator context building.

<!-- Phase 2: TrendFilterEvaluator & PriceVsEmaConditionHandler -->
- src/TradingApp.Application/Trading/Models/MarketContext.cs: Added previous-candle support so cross detection can use actual previous close data.
- src/TradingApp.Application/Trading/Models/StrategyEvaluation.cs: Added TrendFilterPassed to carry trend-filter outcome through strategy evaluation.
- src/TradingApp.Application/Trading/Services/BacktestMarketContextBuilder.cs: Populates PreviousCandle during context construction for backtest signal evaluation.
- src/TradingApp.Application/Trading/Services/CompositeStrategyEngine.cs: Evaluates trend filters before signal conditions, gates failed setups, and propagates trend-filter status.
- src/TradingApp.Application/StrategyAuthoring/Validation/CrossFieldValidator.cs: Removed the obsolete TREND_FILTER_NOT_EVALUATED placeholder info message.
- src/TradingApp.Api/Program.cs: Registered PriceVsEmaConditionHandler and TrendFilterEvaluator in DI.
- tests/TradingApp.Application.Tests/StrategyAuthoring/Services/ConditionEvaluatorTests.cs: Added the new condition handler to the evaluator fixture and added price-vs-EMA coverage.
- tests/TradingApp.Application.Tests/Trading/Services/CompositeStrategyEngineTests.cs: Updated constructor setup for the new dependency and added trend-filter gating assertions.
- tests/TradingApp.Application.Tests/StrategyAuthoring/Validation/CrossFieldValidatorTests.cs: Updated validation expectations to reflect removal of the legacy info message.
- tests/TradingApp.Application.Tests/Backtesting/Services/RealBacktestRunnerTests.cs: Updated the backtest fixture to satisfy the new strategy-engine dependency graph.

<!-- Phase 3: Frontend Trend Filter Card & Models -->
- frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts: Added trend-filter and price-vs-EMA frontend contracts and widened entry-condition params typing.
- frontend/trading-ui/src/app/features/strategy-builder/components/trend-filter-card/trend-filter-card.component.ts: Replaced the stub with a reactive standalone card that handles enabled state, type switching, and operator filtering.
- frontend/trading-ui/src/app/features/strategy-builder/components/trend-filter-card/trend-filter-card.component.html: Added the full trend-filter form UI with dynamic fields for cross filters versus price-vs-EMA.
- frontend/trading-ui/src/app/features/strategy-builder/components/trend-filter-card/trend-filter-card.component.scss: Updated styles for the active card layout and field grid.
- frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.ts: Added stop-loss type synchronization so swing_low toggles value versus lookback controls correctly.
- frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/exit-rules-card.component.html: Enabled the swing_low option and added conditional lookback and value rendering.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts: Added the trendFilter form group, stop-loss lookback control, and trend-filter defaults for edited strategies.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.html: Passed the trend-filter form group into the card only in signal mode.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts: Mapped trend-filter values and swing_low stop-loss data into the API config shape.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts: Updated client-side exit-rule validation so swing_low requires lookback instead of percent value.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.spec.ts: Added coverage for trend-filter mapping and swing_low stop-loss mapping.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.spec.ts: Added coverage for valid and invalid swing_low validation paths.
- frontend/trading-ui/src/app/features/backtesting/backtest-form/backtest-form.component.ts: Narrowed union-typed condition params to preserve existing RSI summary compilation after widening frontend condition models.

<!-- Phase 4: Frontend Price vs EMA Condition & EMA Pullback Template -->
- frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.ts: Added typed Price vs EMA form-group creation with defaults and conditional distance-control disabling.
- frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.ts: Added mixed-condition rendering, Add Price vs EMA support, and type-aware duplication.
- frontend/trading-ui/src/app/features/strategy-builder/components/entry-conditions-card/entry-conditions-card.component.html: Added the new action button and polymorphic condition-item rendering.
- frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts: Added EMA Pullback template application, signal-template detection, and Price vs EMA condition loading for edited strategies.
- frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts: Expanded signal-mode preview text to include trend-filter and Price vs EMA summaries.
- frontend/trading-ui/src/app/features/strategy-builder/models/strategy.model.ts: Enabled the EMA Pullback template in the available template list.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts: Added mapping for Price vs EMA condition params and treated EMA Pullback as a signal template.
- frontend/trading-ui/src/app/features/strategy-builder/services/strategy-validation.service.ts: Added client validation for Price vs EMA near-distance fields and treated EMA Pullback as signal mode.

### Removed

<!-- Phase 1: Backend Models & Infrastructure -->
- None.

<!-- Phase 2: TrendFilterEvaluator & PriceVsEmaConditionHandler -->
- None.

<!-- Phase 3: Frontend Trend Filter Card & Models -->
- None.

<!-- Phase 4: Frontend Price vs EMA Condition & EMA Pullback Template -->
- None.

## Test Results

<!-- Phase 1: Backend Models & Infrastructure -->
- TradingApp.Application.Tests: 167/167 passed.
- Architecture Tests: PASSED - TradingApp.Domain.Tests scope ran successfully at 46/46 passed.

<!-- Phase 2: TrendFilterEvaluator & PriceVsEmaConditionHandler -->
- Solution Build Release: PASSED.
- TradingApp.Application.Tests: 198/198 passed.
- TradingApp.Domain.Tests: 46/46 passed.
- TradingApp.Api.Tests: 182/182 passed.
- TradingApp.Infrastructure.Tests: 59/59 passed.
- TradingApp.Persistence.Tests: 28/28 passed.
- TradingApp.Indicators.Tests: 33/33 passed.
- Architecture Tests: NOT RUN - not required by Phase 2.

<!-- Phase 3: Frontend Trend Filter Card & Models -->
- Angular Build: PASSED.
- Angular Lint: PASSED.
- Unit Tests: NOT RUN - Phase 3 required build and lint, and editor diagnostics were clean on touched spec files.

<!-- Phase 4: Frontend Price vs EMA Condition & EMA Pullback Template -->
- Angular Build: PASSED.
- Angular Lint: PASSED.
- Architecture Tests: NOT RUN - not required for this frontend phase.

## Issues

<!-- Phase 1: Backend Models & Infrastructure -->
- None.

<!-- Phase 2: TrendFilterEvaluator & PriceVsEmaConditionHandler -->
- Initial application test build failed because a new trend-filter helper tuple shape and one class-update pattern were incorrect; both were corrected before rerunning the suite.
- A broad host-side runTests workspace failure did not reproduce after direct solution build and per-project dotnet test validation; end-state backend verification passed.

<!-- Phase 3: Frontend Trend Filter Card & Models -->
- Expanding EntryConditionConfig.params to a union surfaced existing RSI-only assumptions in the strategy builder and backtest form; those call sites were narrowed explicitly so the wider model compiles cleanly.
- ng build completed with pre-existing Angular budget warnings on unrelated assets and initial bundle size; these warnings did not block the phase.

<!-- Phase 4: Frontend Price vs EMA Condition & EMA Pullback Template -->
- Lint initially failed on one redundant boolean cast in the new signal preview branch; this was corrected before rerunning lint successfully.
- Angular build completed with existing bundle-budget warnings unrelated to this phase; build still passed.

## Design Decisions

<!-- Phase 1: Backend Models & Infrastructure -->
- Kept enum serialization on the existing snake_case JsonStringEnumConverter configuration rather than adding type-specific converters.
- Used TradingApp.Domain.Tests as the architecture verification step because the repository does not contain a dedicated architecture-test project.

<!-- Phase 2: TrendFilterEvaluator & PriceVsEmaConditionHandler -->
- Added PreviousCandle to MarketContext and populated it in BacktestMarketContextBuilder so price and EMA cross detection uses actual previous-close semantics instead of an approximation.
- Propagated trend-filter state through StrategyEvaluation rather than trying to mutate ConditionEvaluationResult, because the strategy engine is the orchestration boundary and the condition result is init-only.
- Treated the legacy EmaSingle trend-filter type as unsupported and fail-closed with a warning, because the phase scope only defines ema_cross, sma_cross, and price_above_ema semantics.

<!-- Phase 3: Frontend Trend Filter Card & Models -->
- Updated the mapper and client-side validator alongside the card work so swing_low and trendFilter are visible, serializable, and validated end to end.
- Kept frontend trend-filter enum values in snake_case to match the backend JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) behavior.

<!-- Phase 4: Frontend Price vs EMA Condition & EMA Pullback Template -->
- Used template-id based signal detection for EMA Pullback because the current builder flow still keys mode from selected templates rather than a dedicated persisted form control.
- Updated mapper and client validation in the same phase so the new Price vs EMA UI is serializable and validated end to end instead of only being present in the form.

## Review Hints

<!-- Phase 1: Backend Models & Infrastructure -->
- Review src/TradingApp.Application/StrategyAuthoring/Services/IndicatorExtractor.cs to confirm the existing EmaSingle extraction behavior is still the intended requirement shape.
- Review src/TradingApp.Application/Trading/Services/BacktestMarketContextBuilder.cs if later work requires strict insufficient-history semantics, because the new SMA helpers currently average the available window.

<!-- Phase 2: TrendFilterEvaluator & PriceVsEmaConditionHandler -->
- Review src/TradingApp.Application/StrategyAuthoring/Services/TrendFilterEvaluator.cs to confirm the intentional fail-closed treatment of the legacy EmaSingle enum value is acceptable for existing persisted strategies.
- Review src/TradingApp.Application/Trading/Services/CompositeStrategyEngine.cs to confirm StrategyEvaluation is the correct boundary for propagating TrendFilterPassed to downstream consumers.

<!-- Phase 3: Frontend Trend Filter Card & Models -->
- Review frontend/trading-ui/src/app/features/strategy-builder/components/trend-filter-card/trend-filter-card.component.ts to confirm the operator reset and control enable and disable behavior matches the intended UX when switching filter types.
- Review frontend/trading-ui/src/app/features/strategy-builder/services/strategy-mapper.service.ts to confirm disabled signal-mode trend filter defaults should still be persisted rather than omitted.

<!-- Phase 4: Frontend Price vs EMA Condition & EMA Pullback Template -->
- Review frontend/trading-ui/src/app/features/strategy-builder/strategy-builder-page.component.ts to confirm the EMA Pullback defaults match the intended product behavior when switching between templates.
- Review frontend/trading-ui/src/app/features/strategy-builder/components/preview-summary-card/preview-summary-card.component.ts to confirm the new signal summary wording is the desired UX for mixed trend-filter and entry-condition strategies.

## Release Summary

Implemented all four planned phases for F7.

- Added backend support for ema_cross, sma_cross, and price_above_ema trend filters, plus a full Price vs EMA condition handler and signal-mode gating.
- Added frontend trend-filter editing, Price vs EMA condition authoring, swing_low exit support, EMA Pullback template enablement, and signal preview updates.
- Verified backend changes with solution build and backend project test runs, and verified frontend changes with Angular build and lint.