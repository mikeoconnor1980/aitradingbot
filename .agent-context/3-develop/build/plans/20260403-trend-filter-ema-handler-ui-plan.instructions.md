---
applyTo: ".agent-context/3-develop/build/changes/20260403-trend-filter-ema-handler-ui-changes.md"
currentAgent: "Plan Reviewer"
agentStartedAt: "2026-04-03T17:00:00Z"
status: "plan-in-review"
lastUpdated: "2026-04-03T17:00:00Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: F7 — Trend Filter + EMA Condition Handler + UI

## Overview

Deliver trend filter evaluation (ema_cross, sma_cross, price_above_ema), PriceVsEma condition handler, and UI updates enabling the EMA Pullback template end-to-end.

## PBI Details

**PBI:** F7 — Trend Filter + EMA Condition Handler + UI
**Status:** Draft
**Depends On:** F5 (Condition Evaluator + Indicator Infra), F6 (Signal Mode UI)
**Phase:** 1c (Incremental Conditions)

### Acceptance Criteria

#### Trend Filter — ema_cross
- Given `trendFilter.type = "ema_cross"`, operator = `"gt"`, fast = 50, slow = 200, When EMA(50) > EMA(200), Then trend filter passes
- Given `trendFilter.type = "ema_cross"`, operator = `"gt"`, fast = 50, slow = 200, When EMA(50) < EMA(200), Then trend filter fails and `SetupDetected = false`
- Given `trendFilter.type = "ema_cross"`, operator = `"cross_above"`, When EMA(fast) was below EMA(slow) on previous candle and above on current, Then passes

#### Trend Filter — sma_cross
- Given `trendFilter.type = "sma_cross"`, operator = `"gt"`, fast = 20, slow = 50, When SMA(20) > SMA(50), Then passes

#### Trend Filter — price_above_ema
- Given `trendFilter.type = "price_above_ema"`, operator = `"above"`, period = 200, When close > EMA(200), Then passes
- Given `trendFilter.type = "price_above_ema"`, operator = `"cross_above"`, period = 50, When prev close < EMA(50) and current close > EMA(50), Then passes

#### Trend Filter — Edge Cases
- Given `trendFilter.enabled = false`, Then auto-passes (skipped)
- Given `direction = "long"`, `appliesTo = "short"`, Then auto-passes (skipped)
- Given insufficient candle history for EMA(200), Then fails closed (`SetupDetected = false`)
- Given unknown trend filter type, Then warning logged and fails closed

#### Price vs EMA Condition
- Given `price_vs_ema` with `operator = "near"`, `distanceType = "percent"`, `distanceValue = 0.25`, EMA(50) = 42,050, When close = 42,150 (within 0.25%), Then passes
- Given same setup, When close = 43,000 (outside 0.25%), Then fails
- Given `operator = "touch"`, EMA(50) = 42,000, When high = 42,100, low = 41,900 (wick spans EMA), Then passes
- Given `operator = "touch"`, When wick above EMA, Then fails
- Given `operator = "above"`, When close > EMA, Then passes
- Given `operator = "cross_above"`, When prev close < EMA and current close > EMA, Then passes
- Given insufficient data to compute EMA, Then condition fails with warning in reason

#### End-to-End — EMA Pullback Template
- Given EMA Pullback template selected, Then pre-populates: direction=long, trend filter ema_cross (50 > 200), entry conditions: price near EMA 50 + RSI(14) < 40, exit: TP 3% + SL swing_low lookback 5
- Given trend filter fails, entry conditions skipped entirely and `SetupDetected = false`

#### UI
- Given signal mode, trend filter card editable (not greyed out)
- Given trend filter type = price_above_ema, shows Period field (not Fast/Slow)
- Given price_vs_ema operator = near, shows Distance fields; operator = above hides them
- Given strategy saved, JSON includes trendFilter object and price_vs_ema entry

## Objectives

- Implement `TrendFilterEvaluator` service evaluating ema_cross, sma_cross, price_above_ema
- Implement `PriceVsEmaConditionHandler` for all operators (near, above, below, cross_above, cross_below, touch)
- Add SMA infrastructure (IndicatorContext, BacktestMarketContextBuilder, IndicatorExtractor)
- Expand domain models (TrendFilterType, TrendOperator, PriceVsEmaParams, TrendFilterConfig)
- Implement trend filter card UI with dynamic fields per filter type
- Implement price_vs_ema condition item component
- Enable EMA Pullback template with pre-population
- Enable swing_low exit type

### Discovery References

- F5 delivered: ConditionEvaluator, RsiConditionHandler, IndicatorContext (EMA/RSI), IndicatorExtractor
- F6 assumed delivered: signal mode form wiring, entryConditions FormArray, strategyMode control, mapper signal mode branch
- `ConditionEvaluationResult.TrendFilterPassed` already declared but never populated
- `CrossFieldValidator` emits `TREND_FILTER_NOT_EVALUATED` placeholder info — must be removed/updated
- `BusinessRuleValidator.ValidateTrendFilter` fails price_above_ema (always checks SlowPeriod > 0)
- `PriceVsEmaParams` missing DistanceType, DistanceValue fields
- No SMA support anywhere (IndicatorContext, BacktestMarketContextBuilder)
- `IndicatorExtractor` only extracts from EntryConditions, not from TrendFilter
- Frontend swing_low exit type is disabled in exit-rules-card

### Project Patterns

- `src/TradingApp.Application/StrategyAuthoring/Services/RsiConditionHandler.cs` — canonical IConditionHandler implementation
- `src/TradingApp.Application/StrategyAuthoring/Services/ConditionEvaluator.cs` — handler dispatch pattern
- `src/TradingApp.Application/Trading/Services/CompositeStrategyEngine.cs` — signal mode routing, TrendFilterEvaluator insertion point
- `src/TradingApp.Application/Trading/Models/IndicatorContext.cs` — indicator key/value storage pattern
- `src/TradingApp.Application/Trading/Services/BacktestMarketContextBuilder.cs` — EMA calculation, indicator provisioning
- `src/TradingApp.Application/StrategyAuthoring/Services/IndicatorExtractor.cs` — requirement extraction
- `tests/TradingApp.Application.Tests/StrategyAuthoring/Services/RsiConditionHandlerTests.cs` — handler test pattern
- `tests/TradingApp.Application.Tests/StrategyAuthoring/Services/ConditionEvaluatorTests.cs` — evaluator test pattern
- `frontend/trading-ui/src/app/features/strategy-builder/components/rsi-condition-item/` — condition item UI pattern
- `frontend/trading-ui/src/app/features/strategy-builder/services/condition-factory.service.ts` — condition factory pattern
- `frontend/trading-ui/src/app/features/strategy-builder/components/exit-rules-card/` — card form group input pattern

### [ ] Phase 1: Backend Models & Infrastructure

**Complexity**: Medium | **Risk**: Low

- [ ] Task 1.1: Expand TrendFilterType and TrendOperator enums
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-01-details.md#task-11-expand-trendfiltertype-and-trendoperator-enums

- [ ] Task 1.2: Add Period property and update TrendFilterConfig serialization
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-01-details.md#task-12-add-period-property-and-update-trendfilterconfig-serialization

- [ ] Task 1.3: Expand PriceVsEmaParams with DistanceType and DistanceValue
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-01-details.md#task-13-expand-pricevsemaparams-with-distancetype-and-distancevalue

- [ ] Task 1.4: Add SMA support to IndicatorContext
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-01-details.md#task-14-add-sma-support-to-indicatorcontext

- [ ] Task 1.5: Add SMA calculation to BacktestMarketContextBuilder
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-01-details.md#task-15-add-sma-calculation-to-backtestmarketcontextbuilder

- [ ] Task 1.6: Extend IndicatorExtractor for TrendFilter requirements
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-01-details.md#task-16-extend-indicatorextractor-for-trendfilter-requirements

- [ ] Task 1.7: Update BusinessRuleValidator for new trend filter types
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-01-details.md#task-17-update-businessrulevalidator-for-new-trend-filter-types

- [ ] Task 1.8: Tests for Phase 1 changes
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-01-details.md#task-18-tests-for-phase-1-changes

- [ ] Task 1.9: Build and run architecture tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-01-details.md#task-19-build-and-run-architecture-tests

### [ ] Phase 2: TrendFilterEvaluator & PriceVsEmaConditionHandler

**Complexity**: High | **Risk**: Medium

- [ ] Task 2.1: Create ITrendFilterEvaluator interface and TrendFilterEvaluator
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-02-details.md#task-21-create-itrendfilterevaluator-interface-and-trendfilterevaluator

- [ ] Task 2.2: Create PriceVsEmaConditionHandler
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-02-details.md#task-22-create-pricevsemaconditionhandler

- [ ] Task 2.3: Wire TrendFilterEvaluator into CompositeStrategyEngine
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-02-details.md#task-23-wire-trendfilterevaluator-into-compositesstrategyengine

- [ ] Task 2.4: Update CrossFieldValidator
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-02-details.md#task-24-update-crossfieldvalidator

- [ ] Task 2.5: Register new services in DI
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-02-details.md#task-25-register-new-services-in-di

- [ ] Task 2.6: TrendFilterEvaluator tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-02-details.md#task-26-trendfilterevaluator-tests

- [ ] Task 2.7: PriceVsEmaConditionHandler tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-02-details.md#task-27-pricevsemaconditionhandler-tests

- [ ] Task 2.8: Update CompositeStrategyEngine and ConditionEvaluator tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-02-details.md#task-28-update-compositestrategyengine-and-conditionevaluator-tests

- [ ] Task 2.9: Build and run all backend tests
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-02-details.md#task-29-build-and-run-all-backend-tests

### [ ] Phase 3: Frontend Trend Filter Card & Models

**Complexity**: Medium | **Risk**: Medium

- [ ] Task 3.1: Add TypeScript trend filter types and interfaces
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-03-details.md#task-31-add-typescript-trend-filter-types-and-interfaces

- [ ] Task 3.2: Implement trend-filter-card component
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-03-details.md#task-32-implement-trend-filter-card-component

- [ ] Task 3.3: Create trend filter operator enum and display names
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-03-details.md#task-33-create-trend-filter-operator-enum-and-display-names

- [ ] Task 3.4: Enable swing_low in exit rules card
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-03-details.md#task-34-enable-swing-low-in-exit-rules-card

- [ ] Task 3.5: Build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-03-details.md#task-35-build-and-lint

### [ ] Phase 4: Frontend Price vs EMA Condition & EMA Pullback Template

**Complexity**: Medium | **Risk**: Low

- [ ] Task 4.1: Create price-vs-ema-condition-item component
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-04-details.md#task-41-create-price-vs-ema-condition-item-component

- [ ] Task 4.2: Add PriceVsEma condition factory method
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-04-details.md#task-42-add-pricevsema-condition-factory-method

- [ ] Task 4.3: Update entry-conditions-card with Add Price vs EMA button
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-04-details.md#task-43-update-entry-conditions-card-with-add-price-vs-ema-button

- [ ] Task 4.4: Implement EMA Pullback template pre-population
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-04-details.md#task-44-implement-ema-pullback-template-pre-population

- [ ] Task 4.5: Update preview-summary-card for signal mode
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-04-details.md#task-45-update-preview-summary-card-for-signal-mode

- [ ] Task 4.6: Enable EMA Pullback template
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-04-details.md#task-46-enable-ema-pullback-template

- [ ] Task 4.7: Build and lint
  - Details: .agent-context/3-develop/build/plans/details/20260403-trend-filter-ema-handler-ui-phase-04-details.md#task-47-build-and-lint

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 1: Backend Models & Infrastructure | Medium | Low |
| Phase 2: TrendFilterEvaluator & PriceVsEmaConditionHandler | High | Medium |
| Phase 3: Frontend Trend Filter Card & Models | Medium | Medium |
| Phase 4: Frontend Price vs EMA Condition & EMA Pullback Template | Medium | Low |
| **Total** | **Medium-High** | **Medium** |

### Scoping Notes

- **F6 dependency**: This plan assumes F6 (Signal Mode UI) is fully delivered before implementation begins. F6 provides: `strategyMode` form control, `trendFilter` FormGroup passed to trend-filter-card, `entryConditions` FormArray wired to entry-conditions-card, mapper signal mode branch, template selector driving strategyMode. If F6 is not yet complete, Phases 3–4 are blocked.
- SMA calculation reuses the same streaming approach as EMA (simple moving average over candle closes)
- `TrendFilterConfig.Period` added as nullable `int?` — used only by `PriceAboveEma` type; `FastPeriod`/`SlowPeriod` used by `EmaCross`/`SmaCross`
- Cross detection requires previous indicator values — available via `IndicatorContext.GetPreviousEma/GetPreviousSma`
- Enabling `swing_low` in exit rules is a UI-only change (remove `disabled` attribute from mat-option); backend validation already supports it
- `StrategyConfigRequest` (backtest DTO) is out of scope for this PBI — backtest integration of trend filters is in a separate PBI

## Dependencies

- **F5**: Condition Evaluator + Indicator Infrastructure (delivered)
- **F6**: Signal Mode UI (assumed delivered — blocking for Phases 3–4)
- Angular Material (existing)
- MSTest, Moq, FluentAssertions v6 (existing)

## Success Criteria

- All trend filter acceptance criteria pass in unit tests
- All price_vs_ema condition acceptance criteria pass in unit tests
- TrendFilterEvaluator gates entry conditions (fails → conditions skipped)
- EMA Pullback template pre-populates correctly and evaluates end-to-end
- `dotnet build` succeeds, all backend tests pass
- `ng build` and `npm run lint` succeed
- No regressions in existing grid mode or RSI condition functionality

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|----------|
| Implementation Planner | planned | 2026-04-03T14:00:00Z | 2026-04-03T14:30:00Z |
| Plan Reviewer | plan-in-review | 2026-04-03T17:00:00Z | - |
