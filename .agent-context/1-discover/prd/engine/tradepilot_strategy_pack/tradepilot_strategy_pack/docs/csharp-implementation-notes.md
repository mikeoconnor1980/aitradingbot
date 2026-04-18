# C# Implementation Notes for TradePilot

## Suggested packages

- YamlDotNet
- FluentValidation
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging
- Optional: OneOf, Ardalis.GuardClauses

---

## Recommended model approach

Use a shared base record with subtype records.

```csharp
namespace TradePilot.Strategies.Contracts;

public abstract record StrategyBase
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required StrategyType StrategyType { get; init; }
    public required StrategyDirection Direction { get; init; }
    public required bool Enabled { get; init; }
    public required MarketConfig Market { get; init; }
    public required ExecutionConfig Execution { get; init; }
    public required RiskConfig Risk { get; init; }
    public required TelemetryConfig Telemetry { get; init; }
    public IReadOnlyList<IndicatorDefinition> Indicators { get; init; } = Array.Empty<IndicatorDefinition>();
    public IReadOnlyList<SignalDefinition> Signals { get; init; } = Array.Empty<SignalDefinition>();
    public IReadOnlyList<ConditionDefinition> EnablementConditions { get; init; } = Array.Empty<ConditionDefinition>();
    public IReadOnlyList<ConditionDefinition> DisablementConditions { get; init; } = Array.Empty<ConditionDefinition>();
}
```

```csharp
public sealed record SignalStrategy : StrategyBase
{
    public required SignalCoreConfig Core { get; init; }
    public required SignalFiltersConfig Filters { get; init; }
}
```

```csharp
public sealed record DcaStrategy : StrategyBase
{
    public required DcaTriggerConfig Trigger { get; init; }
    public required DcaLadderConfig Ladder { get; init; }
    public DcaBudgetConfig? Budget { get; init; }
}
```

```csharp
public sealed record GridStrategy : StrategyBase
{
    public required IReadOnlyList<ConditionDefinition> Activation { get; init; }
    public required GridConfig Grid { get; init; }
    public required InventoryConfig Inventory { get; init; }
    public required ProfitModelConfig ProfitModel { get; init; }
    public GridRebalanceConfig? Rebalance { get; init; }
}
```

---

## Loader pattern

Recommended pattern:

1. Deserialize YAML into raw DTOs
2. Detect `strategy_type`
3. Map to subtype DTO
4. Validate using subtype validator
5. Map to domain record
6. Compile runtime evaluators

This is much safer than trying to deserialize directly to runtime objects.

---

## Condition compilation idea

Example condition:

```yaml
lhs: price.close
operator: ">"
rhs:
  type: indicator
  id: ema50_trend
timeframe: 1h
```

Can compile to a delegate like:

```csharp
Func<StrategyContext, bool>
```

Where `StrategyContext` exposes:

- current symbol
- current timeframe snapshots
- indicator values
- derived signals
- portfolio state
- account state

---

## Avoid stringly-typed hell

The YAML will necessarily contain strings, but do not let those strings flow everywhere.

Centralize parsing for:

- lhs field paths
- operator enums
- rhs references
- timeframe aliases
- derived signal ids

Use typed wrappers once the YAML has been parsed.

---

## Validation rules worth enforcing early

- ids must be unique
- indicator ids must be unique within a strategy
- signal ids must be unique within a strategy
- all referenced indicator ids must exist
- all referenced signal ids must exist
- all timeframes must be declared or resolvable
- signal strategies cannot be `neutral`
- grid strategies should usually be `neutral`
- dca strategies should have explicit investment cap
- risk config must exist on every strategy

---

## Runtime state suggestion

Use a per-strategy state store keyed by strategy id.

Something like:

```csharp
public interface IStrategyStateStore
{
    Task<TState?> GetAsync<TState>(string strategyId, CancellationToken ct);
    Task SaveAsync<TState>(string strategyId, TState state, CancellationToken ct);
}
```

Different strategies can then persist different state types.

---

## Backtesting note

Grid backtesting should not rely purely on candle closes.  
You will likely need intra-candle path assumptions or finer-grained replay to model fills properly.

DCA also needs correct average-cost tracking across fills.

Signal strategies are the easiest place to start.