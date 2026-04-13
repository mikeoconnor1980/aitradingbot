# Signal Contracts

Signals are the boundary between strategy evaluation and execution. The current implementation uses one flexible `TradingSignal` class with string `SignalType` values and a parameter bag rather than a hierarchy of typed signal objects.

## Current Signal Model

```csharp
public sealed class TradingSignal
{
    public required string SignalType { get; init; }
    public required string Symbol { get; init; }
    public string? Reason { get; init; }
    public IReadOnlyDictionary<string, object>? Parameters { get; init; }
}
```

Signals flow through:

`IStrategyEngine` -> controller (`IGridController` or `ISignalController`) -> `IRiskEngine` -> `IPositionManager` -> `IExecutionEngine`

## Implemented Signal Types

### `DeployGrid`

Emitted by `GridController` when a new grid should be opened.

Typical parameters:

| Parameter | Type | Purpose |
|-----------|------|---------|
| `anchorPrice` | `decimal` | Price from which the ladder is built |
| `gridLevels` | `int` | Number of levels to place |
| `gridSpacingPercent` | `decimal` | Percent gap between ladder levels |
| `notionalUsd` | `decimal` | Per-level notional |
| `gridCycleId` | `string` | Correlation id for the cycle |
| `entryMode` | `string` | Grid entry mode |
| `leverage` | `int` | Resolved leverage |
| `isIsolated` | `bool` | Isolation flag for live execution |
| `estimatedRiskUsd` | `decimal` | Used for portfolio-heat validation |

### `OpenPosition`

Emitted by `SignalController` for signal-mode entries.

Typical parameters:

| Parameter | Type | Purpose |
|-----------|------|---------|
| `entryPrice` | `decimal` | Entry price, usually current close |
| `size` | `decimal` | Position size |
| `notionalUsd` | `decimal` | Entry notional |
| `orderType` | `string` | Currently `Market` |
| `gridCycleId` | `string` | Uses `signal` as the logical cycle id |

### `TakeProfit`

Used for both normal profit-taking and protective exits. Despite the name, it is also the signal emitted for stop-loss and trailing-stop exits.

Current parameters:

| Parameter | Type | Purpose |
|-----------|------|---------|
| `targetPrice` | `decimal` | Trigger or order price; may be current close for market exits |
| `size` | `decimal` | Exit size |
| `orderType` | `string` | `Limit` or `Market` |
| `gridCycleId` | `string` | Correlates the exit to a grid or signal cycle |
| `cancellationReason` | `string` | Exit reason encoded from `CancellationReason` |

Important nuance:

Stop-loss exits do not use a separate stop-loss signal type. They emit `TakeProfit` with `cancellationReason = StopLossTriggered`.

## `CancellationReason` Enum

The current backtest/live execution flows use these close reasons:

| Value | Meaning |
|-------|---------|
| `GridRedeployed` | Existing open grid orders cancelled before redeployment |
| `TakeProfitTriggered` | Standard profit target hit |
| `StopLossTriggered` | Stop-loss exit |
| `LiquidationTriggered` | Simulated liquidation or forced close |
| `TrailingStopTriggered` | ATR trailing stop fired |
| `ManualCancel` | Explicit cancel flow |

## Signals Referenced but Not Implemented

The following signal names appear in comments, risk-engine checks, or earlier docs, but no current controller emits them:

| Signal | Status | Notes |
|--------|--------|-------|
| `OpenHedge` | NOT IMPLEMENTED | Mentioned as risk-reducing only |
| `AdjustHedge` | NOT IMPLEMENTED | No emitter |
| `CloseHedge` | NOT IMPLEMENTED | Referenced by risk engines, not emitted |
| `PauseStrategy` | NOT IMPLEMENTED | No emitter |
| `Cooldown` | NOT IMPLEMENTED | No emitter |
| `FlattenPosition` | NOT IMPLEMENTED | Referenced by risk engines, not emitted |

### `CancelGrid`

`CancelGrid` is handled by both position managers and appears in risk-engine logic, but it is not emitted by `GridController` today. It is effectively a reserved/secondary signal type used by execution flows rather than active strategy output.

## Runtime Behavior Notes

- Signals are currently in-memory objects only.
- There is no implemented persisted signal table.
- There is no implemented lifecycle store for `Generated -> Validated -> Approved -> Executed` states.
- Audit-like behavior is handled elsewhere, such as backtest collectors and order persistence, not a dedicated signal ledger.

## Extending Signal Contracts

1. Add the new signal emission point in the relevant controller.
2. Update `IRiskEngine` implementations if the signal affects risk-reducing logic or portfolio heat.
3. Update both `BacktestPositionManager` and `LivePositionManager` to execute the signal consistently.
4. Update any backtest audit or persistence projections that depend on signal semantics.

## Future Recommendations

- Replace string `SignalType` values with typed contracts or discriminated payload models.
- Add persistent signal audit storage if compliance or operator debugging requires it.
- Implement hedge signal types only when there is an end-to-end controller, execution, and accounting story for them.
- Add signal analytics and history views once persistence exists.