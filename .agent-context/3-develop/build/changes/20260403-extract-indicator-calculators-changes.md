<!-- markdownlint-disable-file -->
# Release Changes: F6.5 - Extract Indicator Calculators into Standalone Project

**Related Plan**: 20260403-extract-indicator-calculators-plan.instructions.md
**Implementation Date**: 2026-04-03

## Summary

Implements standalone indicator calculators in a new zero-dependency project, refactors backtest indicator evaluation to use them, and extends dynamic indicator context support for MACD.

## Changes

### Added

<!-- Phase 1: Project Scaffolding + EMA and RSI Calculators with Tests -->
- src/TradingApp.Indicators/TradingApp.Indicators.csproj: Added the new zero-dependency indicator class library targeting .NET 8.
- src/TradingApp.Indicators/EmaCalculator.cs: Added an SMA-seeded EMA calculator with both final-value and full-series APIs.
- src/TradingApp.Indicators/RsiCalculator.cs: Added a Wilder-smoothed RSI calculator with neutral, all-gains, and all-losses edge-case handling.
- tests/TradingApp.Indicators.Tests/TradingApp.Indicators.Tests.csproj: Added a dedicated indicator test project that references only TradingApp.Indicators.
- tests/TradingApp.Indicators.Tests/Usings.cs: Added global test usings for MSTest and FluentAssertions.
- tests/TradingApp.Indicators.Tests/EmaCalculatorTests.cs: Added EMA coverage for warmup behavior, SMA seeding, known values, and series output.
- tests/TradingApp.Indicators.Tests/RsiCalculatorTests.cs: Added RSI coverage for insufficient data, known Wilder values, and flat/up/down edge cases.

<!-- Phase 2: ATR, MACD, and Bollinger Bands Calculators with Tests -->
- src/TradingApp.Indicators/AtrCalculator.cs: Added a dependency-free ATR calculator using Wilder-smoothed true range over primitive OHLC tuples.
- src/TradingApp.Indicators/MacdCalculator.cs: Added MACD calculation built from EMA series output and returning line, signal, and histogram.
- src/TradingApp.Indicators/MacdResult.cs: Added the MACD result record type.
- src/TradingApp.Indicators/BollingerBandsCalculator.cs: Added Bollinger Bands calculation using SMA plus population standard deviation.
- src/TradingApp.Indicators/BollingerBandsResult.cs: Added the Bollinger Bands result record type.
- tests/TradingApp.Indicators.Tests/AtrCalculatorTests.cs: Added ATR coverage for insufficient data, seeded ATR, and a known Wilder-smoothed value.
- tests/TradingApp.Indicators.Tests/MacdCalculatorTests.cs: Added MACD coverage for null handling, reference values, and output structure.
- tests/TradingApp.Indicators.Tests/BollingerBandsCalculatorTests.cs: Added Bollinger Bands coverage for exact bands, symmetry, multiplier widening, and flat-price convergence.

### Modified

<!-- Phase 1: Project Scaffolding + EMA and RSI Calculators with Tests -->
- TradingApp.sln: Registered the new indicators library and indicators test project in the existing solution structure.

<!-- Phase 3: Refactor BacktestMarketContextBuilder + Extend IndicatorContext for MACD -->
- src/TradingApp.Application/Trading/Models/IndicatorContext.cs: Reworked MACD storage so line, signal, and histogram are stored separately while preserving existing MACD-line accessors.
- src/TradingApp.Application/TradingApp.Application.csproj: Added the TradingApp.Indicators project reference to the Application layer.
- src/TradingApp.Application/Trading/Services/BacktestMarketContextBuilder.cs: Replaced private EMA, RSI, and ATR math with calculator delegation and wired MACD population into dynamic indicator context building.
- tests/TradingApp.Application.Tests/Trading/Models/IndicatorContextTests.cs: Expanded IndicatorContext coverage for MACD signal and histogram getters plus backward-compatible line access.
- tests/TradingApp.Application.Tests/Trading/Services/BacktestMarketContextBuilderIndicatorTests.cs: Added MACD integration assertions and updated indicator-builder expectations for the refactored calculator flow.

### Removed

## Test Results

<!-- Phase 1: Project Scaffolding + EMA and RSI Calculators with Tests -->
- TradingApp.Indicators.Tests: 12/12 passed
- Full solution test suite: 462/462 passed
- Architecture Tests: Not applicable for this phase

<!-- Phase 2: ATR, MACD, and Bollinger Bands Calculators with Tests -->
- TradingApp.Indicators.Tests: 29/29 passed
- Full solution test suite: 479/479 passed
- Architecture Tests: Not applicable for this phase

<!-- Phase 3: Refactor BacktestMarketContextBuilder + Extend IndicatorContext for MACD -->
- TradingApp.Application.Tests targeted phase tests: 6/6 passed
- Full solution test suite: 482/482 passed
- Architecture Tests: Not applicable for this phase

## Issues

<!-- Phase 1: Project Scaffolding + EMA and RSI Calculators with Tests -->
- The host test runner did not discover the new tests when invoked by direct file path, so verification was completed with `dotnet test` on the project and then on the full solution.
- `dotnet sln add` introduced extra x64/x86 solution configuration entries; these were removed so TradingApp.sln only contains the intended project-registration changes.

<!-- Phase 2: ATR, MACD, and Bollinger Bands Calculators with Tests -->
- The host test runner did not discover the new test files through the dedicated file-based test tool, so verification was completed with `dotnet test` on the indicators test project and then on the full solution.
- One ATR seed expectation in the new test was initially incorrect; it was corrected to match the first valid 14 true ranges before the suite passed.

<!-- Phase 3: Refactor BacktestMarketContextBuilder + Extend IndicatorContext for MACD -->
- None

## Design Decisions

<!-- Phase 1: Project Scaffolding + EMA and RSI Calculators with Tests -->
- Both calculators validate `period` with `ArgumentOutOfRangeException.ThrowIfNegativeOrZero` so invalid inputs fail fast while keeping the APIs pure and dependency-free.
- The new indicators library is intentionally limited to static calculator classes with no package or project references to satisfy the standalone math-only requirement.

<!-- Phase 2: ATR, MACD, and Bollinger Bands Calculators with Tests -->
- MACD composes `EmaCalculator.CalculateSeries` so all moving-average based indicators share the same SMA-seeded EMA behavior.
- ATR accepts `IReadOnlyList<(decimal High, decimal Low, decimal Close)>` to keep the indicators project free of domain-model dependencies.
- Bollinger Bands return a dedicated result record and use population standard deviation over the last `period` closes instead of multiple out parameters.

<!-- Phase 3: Refactor BacktestMarketContextBuilder + Extend IndicatorContext for MACD -->
- `GetMacd` and `GetPreviousMacd` continue to read the original MACD key so existing callers stay backward-compatible while new keys expose signal and histogram values.
- `BacktestMarketContextBuilder` preserves prior snapshot fallback behavior by using calculator results when available and defaulting to `0` or `50` when warmup data is insufficient.

## Review Hints

<!-- Phase 1: Project Scaffolding + EMA and RSI Calculators with Tests -->
- Review the tolerance choices in the EMA and RSI reference tests because the same precision expectations will influence the later MACD, ATR, and Bollinger calculators.
- Review how Phase 3 should handle nullable calculator outputs in `BacktestMarketContextBuilder` when warmup data is insufficient, because the new calculators return null rather than synthetic defaults.

<!-- Phase 2: ATR, MACD, and Bollinger Bands Calculators with Tests -->
- Review the exact-value test tolerances because the Phase 3 integration assertions will depend on these calculator outputs staying stable.
- Review whether future callers should impose semantic constraints on MACD fast/slow/signal periods beyond positivity, because the calculator currently allows any positive combination.

<!-- Phase 3: Refactor BacktestMarketContextBuilder + Extend IndicatorContext for MACD -->
- Review the warmup semantics for required MACD indicators in `BacktestMarketContextBuilder`, because MACD values are only populated once enough history exists and otherwise remain absent from `IndicatorContext`.

## Release Summary

Implemented all 3 phases of F6.5.

- Added a new zero-dependency `TradingApp.Indicators` project and matching test project with EMA, RSI, ATR, MACD, and Bollinger Bands calculators.
- Refactored `BacktestMarketContextBuilder` to use the standalone calculators instead of private indicator math methods.
- Extended dynamic indicator context support so MACD line, signal, and histogram are available while preserving backward-compatible MACD-line access.
- Verified the final state with full solution tests passing: 482/482.