
# Trading Strategy Engine Architecture Spec (.NET)

## Purpose

This document brings together the recommended architecture for a trading strategy engine that supports:

- multiple strategy types
- reusable indicator calculations
- signal rule evaluation
- strategy composition
- backtesting
- future live execution
- multiple input methods:
  - UI forms
  - natural language
  - Pine import / converter
  - YAML / JSON config

The goal is to create a clean internal model so that all strategy definitions eventually converge into one canonical structure.

---

# 1. High-level architecture

## Core pipeline

```text
UI / Natural Language / Pine Import / YAML
    ->
Canonical Strategy Definition / AST
    ->
Validation
    ->
Indicator Calculation
    ->
Signal Evaluation
    ->
Strategy Decision Engine
    ->
Backtest Engine / Live Execution Engine
```

---

# 2. Design principles

## 2.1 Separation of concerns

Keep these concerns separate:

- **Indicator calculators**: pure maths, no trading decisions
- **Signal evaluators**: comparisons, crosses, conditions
- **Strategy engine**: orchestrates entry/exit decisions
- **Backtest engine**: simulates trading history
- **Execution engine**: submits real trades

---

## 2.2 Canonical internal model

No matter how a user defines a strategy:

- clicks in UI
- writes “buy when RSI crosses above 30”
- imports Pine
- pastes JSON
- loads YAML

all of them should end up as the same internal structure.

That is the single most important architecture choice.

---

## 2.3 Extensibility

The design should allow easy addition of:

- EMA
- RSI
- MACD
- ATR
- Bollinger Bands
- grid strategies
- DCA strategies
- martingale
- mean reversion
- trend following
- breakout logic
- portfolio/risk overlays

without rewriting the entire engine.

---

## 2.4 Testability

Every layer should be unit-testable independently:

- indicator outputs
- rule evaluation
- strategy decisions
- backtest behaviour
- JSON/YAML parsing
- NL to canonical mapping

---

# 3. Recommended solution structure

```text
src/
  TradingApp.Domain/
    Market/
    Indicators/
    Signals/
    Strategies/
    Backtesting/
    Execution/

  TradingApp.Application/
    StrategyCompilation/
    StrategyValidation/
    BacktestRunner/
    LiveTrading/

  TradingApp.Infrastructure/
    Json/
    Persistence/
    Exchanges/
    MarketData/
    NaturalLanguage/
    PineImport/

  TradingApp.Api/
    Controllers/
    Contracts/

  TradingApp.Ui/
    Angular app / strategy builder UI
```

---

# 4. Domain model overview

## Main domain areas

### Market
Contains candle and market input models.

### Indicators
Contains indicator definitions and calculators.

### Signals
Contains condition rules and rule evaluators.

### Strategies
Contains strategy definitions and decision logic.

### Backtesting
Contains historical simulation.

### Execution
Contains live order intent and execution abstraction.

---

# 5. Candle / market model

## Candle

```csharp
namespace TradingApp.Domain.Market;

public sealed class Candle
{
    public DateTime TimestampUtc { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal Volume { get; init; }
}
```

## PriceSourceType

```csharp
namespace TradingApp.Domain.Market;

public enum PriceSourceType
{
    Open,
    High,
    Low,
    Close,
    Hl2,
    Hlc3,
    Ohlc4
}
```

---

# 6. Indicator architecture

## Goal

Each indicator should have:

- a definition
- a calculator
- a result series

Each calc should be its own class.

Examples:

- `EmaCalculator`
- `RsiCalculator`
- later:
  - `MacdCalculator`
  - `AtrCalculator`
  - `BollingerBandsCalculator`

---

## IndicatorType

```csharp
namespace TradingApp.Domain.Indicators;

public enum IndicatorType
{
    Ema,
    Rsi,
    Macd,
    Atr,
    BollingerBands
}
```

## IndicatorDefinition

```csharp
namespace TradingApp.Domain.Indicators;

using TradingApp.Domain.Market;

public sealed class IndicatorDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public IndicatorType Type { get; init; }
    public int Period { get; init; }
    public PriceSourceType Source { get; init; } = PriceSourceType.Close;
    public Dictionary<string, string> Parameters { get; init; } = new();
}
```

## IndicatorSeries

```csharp
namespace TradingApp.Domain.Indicators;

public sealed class IndicatorSeries
{
    public string IndicatorId { get; init; } = "";
    public IndicatorType Type { get; init; }
    public IReadOnlyList<decimal?> Values { get; init; } = Array.Empty<decimal?>();
}
```

## IndicatorCalculationContext

```csharp
namespace TradingApp.Domain.Indicators;

using TradingApp.Domain.Market;

public sealed class IndicatorCalculationContext
{
    public required IReadOnlyList<Candle> Candles { get; init; }
    public required IndicatorDefinition Definition { get; init; }
}
```

## IIndicatorCalculator

```csharp
namespace TradingApp.Domain.Indicators;

public interface IIndicatorCalculator
{
    IndicatorType SupportedType { get; }

    IndicatorSeries Calculate(IndicatorCalculationContext context);
}
```

## IIndicatorCalculatorRegistry

```csharp
namespace TradingApp.Domain.Indicators;

public interface IIndicatorCalculatorRegistry
{
    IIndicatorCalculator GetCalculator(IndicatorType indicatorType);
}
```

---

# 7. EMA calculation logic

## What EMA is

EMA is a weighted moving average that gives more importance to recent prices.

## Inputs

- candles
- period
- source

## Formula

### Multiplier

```text
multiplier = 2 / (period + 1)
```

### Seed

Use the SMA of the first `period` values.

### Recursive formula

```text
ema = ((price - previousEma) * multiplier) + previousEma
```

---

## EmaCalculator

```csharp
namespace TradingApp.Domain.Indicators.Calculators;

using TradingApp.Domain.Market;

public sealed class EmaCalculator : IIndicatorCalculator
{
    public IndicatorType SupportedType => IndicatorType.Ema;

    public IndicatorSeries Calculate(IndicatorCalculationContext context)
    {
        var definition = context.Definition;
        var candles = context.Candles;

        if (definition.Period <= 0)
            throw new ArgumentOutOfRangeException(nameof(definition.Period));

        var prices = candles.Select(c => GetSourceValue(c, definition.Source)).ToList();
        var results = new decimal?[prices.Count];

        if (prices.Count < definition.Period)
        {
            return new IndicatorSeries
            {
                IndicatorId = definition.Id,
                Type = definition.Type,
                Values = results
            };
        }

        var multiplier = 2m / (definition.Period + 1);

        decimal seed = prices.Take(definition.Period).Average();
        results[definition.Period - 1] = seed;

        decimal previousEma = seed;

        for (int i = definition.Period; i < prices.Count; i++)
        {
            var ema = ((prices[i] - previousEma) * multiplier) + previousEma;
            results[i] = ema;
            previousEma = ema;
        }

        return new IndicatorSeries
        {
            IndicatorId = definition.Id,
            Type = definition.Type,
            Values = results
        };
    }

    private static decimal GetSourceValue(Candle candle, PriceSourceType source)
    {
        return source switch
        {
            PriceSourceType.Open => candle.Open,
            PriceSourceType.High => candle.High,
            PriceSourceType.Low => candle.Low,
            PriceSourceType.Close => candle.Close,
            PriceSourceType.Hl2 => (candle.High + candle.Low) / 2m,
            PriceSourceType.Hlc3 => (candle.High + candle.Low + candle.Close) / 3m,
            PriceSourceType.Ohlc4 => (candle.Open + candle.High + candle.Low + candle.Close) / 4m,
            _ => throw new NotSupportedException($"Unsupported source: {source}")
        };
    }
}
```

---

# 8. RSI calculation logic

## What RSI is

RSI is a momentum oscillator based on average gains and average losses.

## Inputs

- candles
- period
- source

## Core steps

1. Calculate price changes
2. Split into gains and losses
3. Seed average gain/loss
4. Use Wilder smoothing
5. Compute RS
6. Compute RSI

### RSI formula

```text
RS = avgGain / avgLoss
RSI = 100 - (100 / (1 + RS))
```

---

## RsiCalculator

```csharp
namespace TradingApp.Domain.Indicators.Calculators;

using TradingApp.Domain.Market;

public sealed class RsiCalculator : IIndicatorCalculator
{
    public IndicatorType SupportedType => IndicatorType.Rsi;

    public IndicatorSeries Calculate(IndicatorCalculationContext context)
    {
        var definition = context.Definition;
        var candles = context.Candles;

        if (definition.Period <= 0)
            throw new ArgumentOutOfRangeException(nameof(definition.Period));

        var prices = candles.Select(c => GetSourceValue(c, definition.Source)).ToList();
        var results = new decimal?[prices.Count];

        if (prices.Count < definition.Period + 1)
        {
            return new IndicatorSeries
            {
                IndicatorId = definition.Id,
                Type = definition.Type,
                Values = results
            };
        }

        var gains = new decimal[prices.Count];
        var losses = new decimal[prices.Count];

        for (int i = 1; i < prices.Count; i++)
        {
            var change = prices[i] - prices[i - 1];
            gains[i] = Math.Max(change, 0m);
            losses[i] = Math.Max(-change, 0m);
        }

        decimal avgGain = 0m;
        decimal avgLoss = 0m;

        for (int i = 1; i <= definition.Period; i++)
        {
            avgGain += gains[i];
            avgLoss += losses[i];
        }

        avgGain /= definition.Period;
        avgLoss /= definition.Period;

        results[definition.Period] = CalculateRsi(avgGain, avgLoss);

        for (int i = definition.Period + 1; i < prices.Count; i++)
        {
            avgGain = ((avgGain * (definition.Period - 1)) + gains[i]) / definition.Period;
            avgLoss = ((avgLoss * (definition.Period - 1)) + losses[i]) / definition.Period;

            results[i] = CalculateRsi(avgGain, avgLoss);
        }

        return new IndicatorSeries
        {
            IndicatorId = definition.Id,
            Type = definition.Type,
            Values = results
        };
    }

    private static decimal CalculateRsi(decimal avgGain, decimal avgLoss)
    {
        if (avgLoss == 0m)
            return 100m;

        if (avgGain == 0m)
            return 0m;

        var rs = avgGain / avgLoss;
        return 100m - (100m / (1m + rs));
    }

    private static decimal GetSourceValue(Candle candle, PriceSourceType source)
    {
        return source switch
        {
            PriceSourceType.Open => candle.Open,
            PriceSourceType.High => candle.High,
            PriceSourceType.Low => candle.Low,
            PriceSourceType.Close => candle.Close,
            PriceSourceType.Hl2 => (candle.High + candle.Low) / 2m,
            PriceSourceType.Hlc3 => (candle.High + candle.Low + candle.Close) / 3m,
            PriceSourceType.Ohlc4 => (candle.Open + candle.High + candle.Low + candle.Close) / 4m,
            _ => throw new NotSupportedException($"Unsupported source: {source}")
        };
    }
}
```

---

# 9. Indicator registry

```csharp
namespace TradingApp.Domain.Indicators.Registry;

public sealed class IndicatorCalculatorRegistry : IIndicatorCalculatorRegistry
{
    private readonly Dictionary<IndicatorType, IIndicatorCalculator> _calculators;

    public IndicatorCalculatorRegistry(IEnumerable<IIndicatorCalculator> calculators)
    {
        _calculators = calculators.ToDictionary(x => x.SupportedType);
    }

    public IIndicatorCalculator GetCalculator(IndicatorType indicatorType)
    {
        if (_calculators.TryGetValue(indicatorType, out var calculator))
            return calculator;

        throw new InvalidOperationException($"No calculator registered for indicator type '{indicatorType}'.");
    }
}
```

---

# 10. Signal architecture

## Goal

The signal layer evaluates conditions using:

- current candle values
- previous candle values
- indicator series values
- constants

This layer should not calculate indicators. It only uses already-calculated values.

---

## SignalOperator

```csharp
namespace TradingApp.Domain.Signals;

public enum SignalOperator
{
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Equal
}
```

## CrossRuleType

```csharp
namespace TradingApp.Domain.Signals.Rules;

public enum CrossRuleType
{
    CrossesAbove,
    CrossesBelow
}
```

---

# 11. Signal value references

## Base value reference

```csharp
namespace TradingApp.Domain.Signals.Values;

public abstract record SignalValueReference;
```

## Price value reference

```csharp
namespace TradingApp.Domain.Signals.Values;

using TradingApp.Domain.Market;

public sealed record PriceValueReference(PriceSourceType Source) : SignalValueReference;
```

## Indicator value reference

```csharp
namespace TradingApp.Domain.Signals.Values;

public sealed record IndicatorValueReference(string IndicatorId) : SignalValueReference;
```

## Constant value reference

```csharp
namespace TradingApp.Domain.Signals.Values;

public sealed record ConstantValueReference(decimal Value) : SignalValueReference;
```

---

# 12. Signal rule definitions

## Marker interface

```csharp
namespace TradingApp.Domain.Signals.Rules;

public interface ISignalRuleDefinition
{
}
```

## ComparisonRule

```csharp
namespace TradingApp.Domain.Signals.Rules;

using TradingApp.Domain.Signals.Values;

public sealed class ComparisonRule
{
    public required SignalValueReference Left { get; init; }
    public required SignalOperator Operator { get; init; }
    public required SignalValueReference Right { get; init; }
}
```

## ComparisonRuleDefinition

```csharp
namespace TradingApp.Domain.Signals.Rules;

public sealed class ComparisonRuleDefinition : ISignalRuleDefinition
{
    public required ComparisonRule Rule { get; init; }
}
```

## CrossRule

```csharp
namespace TradingApp.Domain.Signals.Rules;

using TradingApp.Domain.Signals.Values;

public sealed class CrossRule
{
    public required SignalValueReference Left { get; init; }
    public required SignalValueReference Right { get; init; }
    public required CrossRuleType Type { get; init; }
}
```

## CrossRuleDefinition

```csharp
namespace TradingApp.Domain.Signals.Rules;

public sealed class CrossRuleDefinition : ISignalRuleDefinition
{
    public required CrossRule Rule { get; init; }
}
```

## Logical operator

```csharp
namespace TradingApp.Domain.Signals.Rules;

public enum LogicalOperator
{
    All,
    Any
}
```

## LogicalRuleGroup

```csharp
namespace TradingApp.Domain.Signals.Rules;

public sealed class LogicalRuleGroup : ISignalRuleDefinition
{
    public required LogicalOperator Operator { get; init; }
    public required IReadOnlyList<ISignalRuleDefinition> Rules { get; init; }
}
```

---

# 13. Signal evaluation context and result

## SignalEvaluationContext

```csharp
namespace TradingApp.Domain.Signals;

using TradingApp.Domain.Indicators;
using TradingApp.Domain.Market;

public sealed class SignalEvaluationContext
{
    public required IReadOnlyList<Candle> Candles { get; init; }
    public required IReadOnlyDictionary<string, IndicatorSeries> IndicatorSeriesById { get; init; }
    public required int CurrentIndex { get; init; }
}
```

## SignalEvaluationResult

```csharp
namespace TradingApp.Domain.Signals;

public sealed class SignalEvaluationResult
{
    public required bool IsMatch { get; init; }
    public string? Reason { get; init; }

    public static SignalEvaluationResult True(string? reason = null) => new() { IsMatch = true, Reason = reason };
    public static SignalEvaluationResult False(string? reason = null) => new() { IsMatch = false, Reason = reason };
}
```

---

# 14. Signal value resolver

## Interface

```csharp
namespace TradingApp.Domain.Signals;

using TradingApp.Domain.Signals.Values;

public interface ISignalValueResolver
{
    decimal? ResolveCurrent(SignalValueReference reference, SignalEvaluationContext context);
    decimal? ResolvePrevious(SignalValueReference reference, SignalEvaluationContext context);
}
```

## Implementation

```csharp
namespace TradingApp.Domain.Signals.Evaluators;

using TradingApp.Domain.Signals.Values;
using TradingApp.Domain.Market;

public sealed class SignalValueResolver : ISignalValueResolver
{
    public decimal? ResolveCurrent(SignalValueReference reference, SignalEvaluationContext context)
        => Resolve(reference, context, context.CurrentIndex);

    public decimal? ResolvePrevious(SignalValueReference reference, SignalEvaluationContext context)
        => context.CurrentIndex <= 0 ? null : Resolve(reference, context, context.CurrentIndex - 1);

    private static decimal? Resolve(SignalValueReference reference, SignalEvaluationContext context, int index)
    {
        return reference switch
        {
            ConstantValueReference constant => constant.Value,
            PriceValueReference price => ResolvePrice(price.Source, context.Candles[index]),
            IndicatorValueReference indicator => ResolveIndicator(indicator.IndicatorId, context, index),
            _ => throw new NotSupportedException($"Unsupported signal value reference: {reference.GetType().Name}")
        };
    }

    private static decimal ResolvePrice(PriceSourceType source, Candle candle)
    {
        return source switch
        {
            PriceSourceType.Open => candle.Open,
            PriceSourceType.High => candle.High,
            PriceSourceType.Low => candle.Low,
            PriceSourceType.Close => candle.Close,
            PriceSourceType.Hl2 => (candle.High + candle.Low) / 2m,
            PriceSourceType.Hlc3 => (candle.High + candle.Low + candle.Close) / 3m,
            PriceSourceType.Ohlc4 => (candle.Open + candle.High + candle.Low + candle.Close) / 4m,
            _ => throw new NotSupportedException($"Unsupported source: {source}")
        };
    }

    private static decimal? ResolveIndicator(string indicatorId, SignalEvaluationContext context, int index)
    {
        if (!context.IndicatorSeriesById.TryGetValue(indicatorId, out var series))
            return null;

        if (index < 0 || index >= series.Values.Count)
            return null;

        return series.Values[index];
    }
}
```

---

# 15. Rule evaluators

## ISignalRuleEvaluator

```csharp
namespace TradingApp.Domain.Signals;

public interface ISignalRuleEvaluator<in TRule>
{
    SignalEvaluationResult Evaluate(TRule rule, SignalEvaluationContext context);
}
```

## ComparisonRuleEvaluator

```csharp
namespace TradingApp.Domain.Signals.Evaluators;

using TradingApp.Domain.Signals.Rules;

public sealed class ComparisonRuleEvaluator : ISignalRuleEvaluator<ComparisonRule>
{
    private readonly ISignalValueResolver _resolver;

    public ComparisonRuleEvaluator(ISignalValueResolver resolver)
    {
        _resolver = resolver;
    }

    public SignalEvaluationResult Evaluate(ComparisonRule rule, SignalEvaluationContext context)
    {
        var left = _resolver.ResolveCurrent(rule.Left, context);
        var right = _resolver.ResolveCurrent(rule.Right, context);

        if (left is null || right is null)
            return SignalEvaluationResult.False("Comparison values not ready.");

        bool result = rule.Operator switch
        {
            SignalOperator.GreaterThan => left > right,
            SignalOperator.GreaterThanOrEqual => left >= right,
            SignalOperator.LessThan => left < right,
            SignalOperator.LessThanOrEqual => left <= right,
            SignalOperator.Equal => left == right,
            _ => throw new NotSupportedException($"Unsupported operator: {rule.Operator}")
        };

        return result ? SignalEvaluationResult.True() : SignalEvaluationResult.False();
    }
}
```

## CrossRuleEvaluator

```csharp
namespace TradingApp.Domain.Signals.Evaluators;

using TradingApp.Domain.Signals.Rules;

public sealed class CrossRuleEvaluator : ISignalRuleEvaluator<CrossRule>
{
    private readonly ISignalValueResolver _resolver;

    public CrossRuleEvaluator(ISignalValueResolver resolver)
    {
        _resolver = resolver;
    }

    public SignalEvaluationResult Evaluate(CrossRule rule, SignalEvaluationContext context)
    {
        var previousLeft = _resolver.ResolvePrevious(rule.Left, context);
        var previousRight = _resolver.ResolvePrevious(rule.Right, context);
        var currentLeft = _resolver.ResolveCurrent(rule.Left, context);
        var currentRight = _resolver.ResolveCurrent(rule.Right, context);

        if (previousLeft is null || previousRight is null || currentLeft is null || currentRight is null)
            return SignalEvaluationResult.False("Cross values not ready.");

        bool result = rule.Type switch
        {
            CrossRuleType.CrossesAbove => previousLeft <= previousRight && currentLeft > currentRight,
            CrossRuleType.CrossesBelow => previousLeft >= previousRight && currentLeft < currentRight,
            _ => throw new NotSupportedException($"Unsupported cross rule type: {rule.Type}")
        };

        return result ? SignalEvaluationResult.True() : SignalEvaluationResult.False();
    }
}
```

## LogicalRuleGroupEvaluator

```csharp
namespace TradingApp.Domain.Signals.Evaluators;

using TradingApp.Domain.Signals.Rules;

public sealed class LogicalRuleGroupEvaluator
{
    private readonly ComparisonRuleEvaluator _comparisonEvaluator;
    private readonly CrossRuleEvaluator _crossEvaluator;

    public LogicalRuleGroupEvaluator(
        ComparisonRuleEvaluator comparisonEvaluator,
        CrossRuleEvaluator crossEvaluator)
    {
        _comparisonEvaluator = comparisonEvaluator;
        _crossEvaluator = crossEvaluator;
    }

    public SignalEvaluationResult Evaluate(ISignalRuleDefinition definition, SignalEvaluationContext context)
    {
        return definition switch
        {
            ComparisonRuleDefinition comparison => _comparisonEvaluator.Evaluate(comparison.Rule, context),
            CrossRuleDefinition cross => _crossEvaluator.Evaluate(cross.Rule, context),
            LogicalRuleGroup group => EvaluateGroup(group, context),
            _ => throw new NotSupportedException($"Unsupported rule definition type: {definition.GetType().Name}")
        };
    }

    private SignalEvaluationResult EvaluateGroup(LogicalRuleGroup group, SignalEvaluationContext context)
    {
        var results = group.Rules.Select(rule => Evaluate(rule, context)).ToList();

        bool isMatch = group.Operator switch
        {
            LogicalOperator.All => results.All(r => r.IsMatch),
            LogicalOperator.Any => results.Any(r => r.IsMatch),
            _ => throw new NotSupportedException($"Unsupported logical operator: {group.Operator}")
        };

        return isMatch ? SignalEvaluationResult.True() : SignalEvaluationResult.False();
    }
}
```

---

# 16. Strategy layer

## Goal

The strategy layer combines:

- indicator definitions
- entry rules
- exit rules
- optional stop loss / take profit
- later:
  - position sizing
  - leverage
  - trade direction
  - execution settings

---

## StrategyDefinition

```csharp
namespace TradingApp.Domain.Strategies;

using TradingApp.Domain.Indicators;
using TradingApp.Domain.Signals.Rules;

public sealed class StrategyDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "";
    public IReadOnlyList<IndicatorDefinition> Indicators { get; init; } = Array.Empty<IndicatorDefinition>();

    public ISignalRuleDefinition? EntryRule { get; init; }
    public ISignalRuleDefinition? ExitRule { get; init; }

    public decimal? StopLossPercent { get; init; }
    public decimal? TakeProfitPercent { get; init; }
}
```

## StrategyAction

```csharp
namespace TradingApp.Domain.Strategies;

public enum StrategyAction
{
    None,
    EnterLong,
    ExitLong,
    EnterShort,
    ExitShort
}
```

## StrategyExecutionDecision

```csharp
namespace TradingApp.Domain.Strategies;

public sealed class StrategyExecutionDecision
{
    public required StrategyAction Action { get; init; }
    public string? Reason { get; init; }
}
```

## StrategyEvaluationResult

```csharp
namespace TradingApp.Domain.Strategies;

public sealed class StrategyEvaluationResult
{
    public required bool EntryMatched { get; init; }
    public required bool ExitMatched { get; init; }
    public StrategyExecutionDecision Decision { get; init; } = new() { Action = StrategyAction.None };
}
```

## IStrategyEngine

```csharp
namespace TradingApp.Domain.Strategies;

using TradingApp.Domain.Market;

public interface IStrategyEngine
{
    StrategyEvaluationResult Evaluate(
        StrategyDefinition strategy,
        IReadOnlyList<Candle> candles,
        int currentIndex,
        bool currentlyInPosition);
}
```

## StrategyEngine

```csharp
namespace TradingApp.Domain.Strategies;

using TradingApp.Domain.Indicators;
using TradingApp.Domain.Signals;
using TradingApp.Domain.Signals.Evaluators;

public sealed class StrategyEngine : IStrategyEngine
{
    private readonly IIndicatorCalculatorRegistry _indicatorRegistry;
    private readonly LogicalRuleGroupEvaluator _ruleEvaluator;

    public StrategyEngine(
        IIndicatorCalculatorRegistry indicatorRegistry,
        LogicalRuleGroupEvaluator ruleEvaluator)
    {
        _indicatorRegistry = indicatorRegistry;
        _ruleEvaluator = ruleEvaluator;
    }

    public StrategyEvaluationResult Evaluate(
        StrategyDefinition strategy,
        IReadOnlyList<Market.Candle> candles,
        int currentIndex,
        bool currentlyInPosition)
    {
        var indicatorSeries = strategy.Indicators
            .Select(def =>
            {
                var calculator = _indicatorRegistry.GetCalculator(def.Type);
                return calculator.Calculate(new IndicatorCalculationContext
                {
                    Candles = candles,
                    Definition = def
                });
            })
            .ToDictionary(x => x.IndicatorId);

        var signalContext = new SignalEvaluationContext
        {
            Candles = candles,
            IndicatorSeriesById = indicatorSeries,
            CurrentIndex = currentIndex
        };

        var entryMatched = strategy.EntryRule is not null
            && _ruleEvaluator.Evaluate(strategy.EntryRule, signalContext).IsMatch;

        var exitMatched = strategy.ExitRule is not null
            && _ruleEvaluator.Evaluate(strategy.ExitRule, signalContext).IsMatch;

        var decision = new StrategyExecutionDecision
        {
            Action = ResolveAction(entryMatched, exitMatched, currentlyInPosition)
        };

        return new StrategyEvaluationResult
        {
            EntryMatched = entryMatched,
            ExitMatched = exitMatched,
            Decision = decision
        };
    }

    private static StrategyAction ResolveAction(bool entryMatched, bool exitMatched, bool currentlyInPosition)
    {
        if (!currentlyInPosition && entryMatched)
            return StrategyAction.EnterLong;

        if (currentlyInPosition && exitMatched)
            return StrategyAction.ExitLong;

        return StrategyAction.None;
    }
}
```

---

# 17. Backtesting layer

## Goal

Replay candle history in order, evaluating strategy rules at each step.

Important:

- use candle-by-candle processing
- do not look ahead
- ideally use closed candles only
- separate strategy logic from fill logic

---

## BacktestRequest

```csharp
namespace TradingApp.Domain.Backtesting;

using TradingApp.Domain.Market;
using TradingApp.Domain.Strategies;

public sealed class BacktestRequest
{
    public required StrategyDefinition Strategy { get; init; }
    public required IReadOnlyList<Candle> Candles { get; init; }
    public decimal StartingCapital { get; init; } = 1000m;
}
```

## BacktestTrade

```csharp
namespace TradingApp.Domain.Backtesting;

public sealed class BacktestTrade
{
    public DateTime EntryTimeUtc { get; init; }
    public decimal EntryPrice { get; init; }
    public DateTime? ExitTimeUtc { get; set; }
    public decimal? ExitPrice { get; set; }
    public decimal? ProfitLoss { get; set; }
}
```

## BacktestResult

```csharp
namespace TradingApp.Domain.Backtesting;

public sealed class BacktestResult
{
    public IReadOnlyList<BacktestTrade> Trades { get; init; } = Array.Empty<BacktestTrade>();
    public decimal FinalCapital { get; init; }
}
```

## IBacktestEngine

```csharp
namespace TradingApp.Domain.Backtesting;

public interface IBacktestEngine
{
    BacktestResult Run(BacktestRequest request);
}
```

## BacktestEngine

```csharp
namespace TradingApp.Domain.Backtesting;

using TradingApp.Domain.Strategies;

public sealed class BacktestEngine : IBacktestEngine
{
    private readonly IStrategyEngine _strategyEngine;

    public BacktestEngine(IStrategyEngine strategyEngine)
    {
        _strategyEngine = strategyEngine;
    }

    public BacktestResult Run(BacktestRequest request)
    {
        var trades = new List<BacktestTrade>();
        BacktestTrade? openTrade = null;
        decimal capital = request.StartingCapital;

        for (int i = 0; i < request.Candles.Count; i++)
        {
            var currentlyInPosition = openTrade is not null;

            var result = _strategyEngine.Evaluate(
                request.Strategy,
                request.Candles,
                i,
                currentlyInPosition);

            var candle = request.Candles[i];

            if (!currentlyInPosition && result.Decision.Action == StrategyAction.EnterLong)
            {
                openTrade = new BacktestTrade
                {
                    EntryTimeUtc = candle.TimestampUtc,
                    EntryPrice = candle.Close
                };
            }
            else if (currentlyInPosition && result.Decision.Action == StrategyAction.ExitLong)
            {
                openTrade!.ExitTimeUtc = candle.TimestampUtc;
                openTrade.ExitPrice = candle.Close;
                openTrade.ProfitLoss = candle.Close - openTrade.EntryPrice;
                capital += openTrade.ProfitLoss.Value;

                trades.Add(openTrade);
                openTrade = null;
            }
        }

        return new BacktestResult
        {
            Trades = trades,
            FinalCapital = capital
        };
    }
}
```

---

# 18. Canonical strategy JSON contract

## Goal

This contract should represent the canonical format produced by:

- UI strategy builder
- natural language compiler
- Pine import compiler
- YAML parser

---

## Example JSON

```json
{
  "id": "ema-rsi-pullback",
  "name": "EMA RSI Pullback",
  "indicators": [
    {
      "id": "ema200",
      "type": "ema",
      "period": 200,
      "source": "close"
    },
    {
      "id": "ema9",
      "type": "ema",
      "period": 9,
      "source": "close"
    },
    {
      "id": "ema21",
      "type": "ema",
      "period": 21,
      "source": "close"
    },
    {
      "id": "rsi14",
      "type": "rsi",
      "period": 14,
      "source": "close"
    }
  ],
  "entryRule": {
    "type": "all",
    "rules": [
      {
        "type": "comparison",
        "left": { "type": "price", "source": "close" },
        "operator": "greaterThan",
        "right": { "type": "indicator", "indicatorId": "ema200" }
      },
      {
        "type": "comparison",
        "left": { "type": "indicator", "indicatorId": "ema9" },
        "operator": "greaterThan",
        "right": { "type": "indicator", "indicatorId": "ema21" }
      },
      {
        "type": "cross",
        "crossType": "crossesAbove",
        "left": { "type": "indicator", "indicatorId": "rsi14" },
        "right": { "type": "constant", "value": 30 }
      }
    ]
  },
  "exitRule": {
    "type": "cross",
    "crossType": "crossesBelow",
    "left": { "type": "indicator", "indicatorId": "ema9" },
    "right": { "type": "indicator", "indicatorId": "ema21" }
  },
  "risk": {
    "stopLossPercent": 2,
    "takeProfitPercent": 5
  }
}
```

---

# 19. Canonical AST model

## Why AST?

An AST makes it easier to unify different inputs before serializing into final runtime JSON.

It is especially useful for:

- natural language parsing
- Pine conversion
- strategy editor drag/drop UIs

---

## Example AST model

```csharp
public sealed class StrategyAst
{
    public string Name { get; init; } = "";
    public IReadOnlyList<IndicatorAst> Indicators { get; init; } = Array.Empty<IndicatorAst>();
    public RuleNodeAst? EntryRule { get; init; }
    public RuleNodeAst? ExitRule { get; init; }
    public RiskAst? Risk { get; init; }
}

public sealed class IndicatorAst
{
    public string Id { get; init; } = "";
    public string Type { get; init; } = "";
    public int Period { get; init; }
    public string Source { get; init; } = "close";
    public Dictionary<string, string> Parameters { get; init; } = new();
}

public abstract record RuleNodeAst;

public sealed record RuleGroupAst(
    string Operator,
    IReadOnlyList<RuleNodeAst> Rules) : RuleNodeAst;

public sealed record ComparisonRuleAst(
    ValueReferenceAst Left,
    string Operator,
    ValueReferenceAst Right) : RuleNodeAst;

public sealed record CrossRuleAst(
    string CrossType,
    ValueReferenceAst Left,
    ValueReferenceAst Right) : RuleNodeAst;

public abstract record ValueReferenceAst;

public sealed record PriceValueAst(string Source) : ValueReferenceAst;
public sealed record IndicatorValueAst(string IndicatorId) : ValueReferenceAst;
public sealed record ConstantValueAst(decimal Value) : ValueReferenceAst;

public sealed class RiskAst
{
    public decimal? StopLossPercent { get; init; }
    public decimal? TakeProfitPercent { get; init; }
}
```

---

# 20. Mapping AST to domain

## Flow

```text
Natural language / UI / Pine
    ->
AST
    ->
Validation
    ->
Domain StrategyDefinition
    ->
Runtime engine
```

## Mapper responsibilities

Create a mapper layer such as:

- `StrategyAstMapper`
- `IndicatorAstMapper`
- `RuleAstMapper`

These should:

- validate enum names
- validate indicator references
- convert strings to domain enums
- reject invalid tree structures

---

# 21. Natural language strategy flow

## Suggested flow

```text
User prompt
  ->
LLM prompt
  ->
Structured AST JSON
  ->
Validation
  ->
Canonical domain model
```

## Recommendation

Do not ask the LLM to generate C# objects directly.

Do this instead:

- LLM outputs strict JSON AST
- your app validates it
- your mapper converts AST to domain objects

That gives you much better control.

---

## Example NL prompt intent

User says:

> Buy when the close is above the 200 EMA, the 9 EMA is above the 21 EMA, and RSI 14 crosses above 30. Exit when the 9 EMA crosses below the 21 EMA.

Compiler produces canonical AST/JSON.

---

# 22. Pine import flow

## Suggested flow

```text
Pine / pseudo-Pine
   ->
Parser / converter
   ->
Intermediate AST
   ->
Canonical AST
   ->
Validation
   ->
Domain strategy
```

You may not support all Pine features initially.

That is okay.

A good v1 goal is to support only:

- indicator declarations
- comparison rules
- cross rules
- simple long/exit logic

---

# 23. Validation layer

## Recommended validators

### IndicatorDefinitionValidator
Checks:

- period > 0
- source supported
- type supported
- required params present

### StrategyDefinitionValidator
Checks:

- indicator ids unique
- all indicator references exist
- rule tree is valid
- entry/exit rules are not malformed
- risk settings sensible

### StrategyAstValidator
Checks:

- JSON shape correct
- strings map to allowed values
- references exist
- no empty rule groups
- no duplicate IDs

---

## Example validator interfaces

```csharp
public interface IValidator<in T>
{
    ValidationResult Validate(T instance);
}

public sealed class ValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}
```

---

# 24. Recommended helper services

Useful shared services:

- `IPriceSourceResolver`
- `IIndicatorCalculatorRegistry`
- `ISignalValueResolver`
- `IStrategyDefinitionValidator`
- `IStrategyAstMapper`
- `IStrategyJsonSerializer`
- `IStrategyCompiler`

---

# 25. Dependency injection registration

```csharp
services.AddSingleton<IIndicatorCalculator, EmaCalculator>();
services.AddSingleton<IIndicatorCalculator, RsiCalculator>();
services.AddSingleton<IIndicatorCalculatorRegistry, IndicatorCalculatorRegistry>();

services.AddSingleton<ISignalValueResolver, SignalValueResolver>();
services.AddSingleton<ComparisonRuleEvaluator>();
services.AddSingleton<CrossRuleEvaluator>();
services.AddSingleton<LogicalRuleGroupEvaluator>();

services.AddSingleton<IStrategyEngine, StrategyEngine>();
services.AddSingleton<IBacktestEngine, BacktestEngine>();
```

Later add:

```csharp
services.AddSingleton<IStrategyAstMapper, StrategyAstMapper>();
services.AddSingleton<IValidator<StrategyAst>, StrategyAstValidator>();
services.AddSingleton<IValidator<StrategyDefinition>, StrategyDefinitionValidator>();
```

---

# 26. Example strategy in C#

```csharp
var ema200 = new IndicatorDefinition
{
    Id = "ema200",
    Type = IndicatorType.Ema,
    Period = 200,
    Source = PriceSourceType.Close
};

var ema9 = new IndicatorDefinition
{
    Id = "ema9",
    Type = IndicatorType.Ema,
    Period = 9,
    Source = PriceSourceType.Close
};

var ema21 = new IndicatorDefinition
{
    Id = "ema21",
    Type = IndicatorType.Ema,
    Period = 21,
    Source = PriceSourceType.Close
};

var rsi14 = new IndicatorDefinition
{
    Id = "rsi14",
    Type = IndicatorType.Rsi,
    Period = 14,
    Source = PriceSourceType.Close
};

var strategy = new StrategyDefinition
{
    Name = "EMA RSI Pullback",
    Indicators = new[] { ema200, ema9, ema21, rsi14 },
    EntryRule = new LogicalRuleGroup
    {
        Operator = LogicalOperator.All,
        Rules = new ISignalRuleDefinition[]
        {
            new ComparisonRuleDefinition
            {
                Rule = new ComparisonRule
                {
                    Left = new PriceValueReference(PriceSourceType.Close),
                    Operator = SignalOperator.GreaterThan,
                    Right = new IndicatorValueReference("ema200")
                }
            },
            new ComparisonRuleDefinition
            {
                Rule = new ComparisonRule
                {
                    Left = new IndicatorValueReference("ema9"),
                    Operator = SignalOperator.GreaterThan,
                    Right = new IndicatorValueReference("ema21")
                }
            },
            new CrossRuleDefinition
            {
                Rule = new CrossRule
                {
                    Left = new IndicatorValueReference("rsi14"),
                    Right = new ConstantValueReference(30m),
                    Type = CrossRuleType.CrossesAbove
                }
            }
        }
    },
    ExitRule = new CrossRuleDefinition
    {
        Rule = new CrossRule
        {
            Left = new IndicatorValueReference("ema9"),
            Right = new IndicatorValueReference("ema21"),
            Type = CrossRuleType.CrossesBelow
        }
    },
    StopLossPercent = 2m,
    TakeProfitPercent = 5m
};
```

---

# 27. Recommended implementation phases

## Phase 1: foundations

Build:

- Candle
- PriceSourceType
- IndicatorDefinition
- IndicatorSeries
- EmaCalculator
- RsiCalculator
- registry
- basic tests

## Phase 2: signal engine

Build:

- value references
- comparison rules
- cross rules
- group rules
- evaluators
- tests

## Phase 3: strategy engine

Build:

- StrategyDefinition
- StrategyEngine
- entry/exit evaluation
- tests

## Phase 4: backtesting

Build:

- BacktestEngine
- trade recording
- P/L logic
- metrics
- tests

## Phase 5: canonical contracts

Build:

- AST models
- JSON contracts
- validators
- mappers

## Phase 6: external strategy inputs

Build:

- UI to JSON/AST
- natural language compiler
- Pine converter

## Phase 7: live execution

Build:

- exchange adapters
- order execution abstraction
- risk checks
- state tracking
- portfolio/account support

---

# 28. Important practical decisions

## Closed candles only

Recommended for v1:

- evaluate on closed candles only

Benefits:

- stable signals
- easier backtest/live parity
- less signal flicker

---

## Full precision internally

Recommended:

- do not round indicator values internally
- round only for display

---

## Null warm-up period

Indicators need warm-up.

Examples:

- EMA 21 unavailable before enough candles exist
- RSI 14 unavailable before 15 prices exist

Recommended:

- use `null`
- make rule evaluators treat null as not-ready

---

## No look-ahead bias

Backtests must process strictly in order.

Never use future candles when evaluating current decision logic.

---

# 29. Near-future improvements

## Indicator result cache

At the moment the sample strategy engine recalculates indicators for each evaluation.

That is acceptable for a sketch, but not ideal for production.

A production version should:

- calculate indicator series once per backtest run
- cache them
- reuse them across all candle indexes

---

## Multi-output indicators

Indicators like MACD and Bollinger Bands return multiple outputs.

Future evolution could be:

```csharp
public sealed class IndicatorSeries
{
    public string IndicatorId { get; init; } = "";
    public IndicatorType Type { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<decimal?>> Outputs { get; init; }
        = new Dictionary<string, IReadOnlyList<decimal?>>();
}
```

Examples:

- MACD outputs:
  - macd
  - signal
  - histogram
- Bollinger outputs:
  - upper
  - middle
  - lower

For EMA/RSI, single series is fine.

---

## Position sizing

Later, add:

- fixed amount
- percentage of equity
- leverage
- DCA sizing
- martingale sizing
- risk-based sizing by stop distance

---

## Direction support

Later, support:

- long only
- short only
- both long and short

---

## Strategy metadata

Later, add:

- category
- tags
- version
- author
- created/updated timestamps
- source type (UI/NL/Pine/manual)

---

# 30. Recommended next-level contracts

Eventually, your strategy model will probably want sections like:

```json
{
  "metadata": {},
  "indicators": [],
  "entryRule": {},
  "exitRule": {},
  "risk": {},
  "positionSizing": {},
  "execution": {},
  "filters": {}
}
```

Where:

- `risk` handles stop loss / take profit / max drawdown constraints
- `positionSizing` handles capital allocation
- `execution` handles order type / slippage / cooldown
- `filters` handles day/time/session/market regime filters

---

# 31. Summary recommendation

For your app, the cleanest architecture is:

## Core split

- **Indicators** calculate values
- **Signals** evaluate conditions
- **Strategies** combine rules
- **Backtest engine** simulates history
- **Execution engine** places trades
- **AST/JSON compiler layer** unifies UI, NL, and Pine inputs

## Most important design rule

Everything should flow into a **single canonical internal representation**.

That gives you:

- one engine
- one validator path
- one backtest path
- one execution path
- multiple strategy input methods

That is exactly what will make the platform scalable as you add more strategies and features.

---

# 32. Suggested next file

A great next artifact would be one of these:

## Option A: Strategy JSON schema spec
A very detailed schema for:

- indicators
- rule groups
- risk
- execution
- sizing

## Option B: Angular strategy builder UI spec
A full field-by-field and component-by-component design for building these strategies in your app.

## Option C: Natural language compiler prompt + validation spec
A strict prompt and response contract for generating AST JSON from plain English strategy descriptions.
