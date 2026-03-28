# Scheduling Architecture (CandleClock + StrategyScheduler)

This document describes the scheduling architecture used to trigger strategy evaluation.
The goal is to ensure the trading system runs strategies exactly once per closed candle
and works identically in both live trading and backtesting environments.

---

# Design Goals

The scheduling system must:

• Trigger strategies only on confirmed candle closes  
• Avoid duplicate executions  
• Work with both websocket market data and replayed historical data  
• Separate time management from strategy logic  
• Prevent timing bugs caused by partial candles  

---

# Core Components

The scheduling system consists of two main components:

CandleClock  
StrategyScheduler  

These work together to trigger strategy execution safely.

---

# CandleClock

The CandleClock detects when a candle has officially closed and emits an event.

Responsibilities:

• track latest candles for each timeframe  
• detect candle close transitions  
• emit a CandleClosedEvent exactly once  
• prevent duplicate emissions after reconnects or replay  

The CandleClock does not know about strategies or trading logic.

---

# CandleClosedEvent Model

Example event structure:

public class CandleClosedEvent
{
    public string Symbol { get; init; }
    public string Timeframe { get; init; }
    public long OpenTimeUtc { get; init; }
    public long CloseTimeUtc { get; init; }
    public Candle Candle { get; init; }
}

This event becomes the canonical trigger for the system.

---

# StrategyScheduler

The StrategyScheduler subscribes to CandleClock events and determines
whether strategies should run.

Responsibilities:

• listen for CandleClosedEvent  
• filter for trigger timeframe (15m for the grid strategy)  
• build MarketContext (shared across all subscribers)  
• fan out execution to all active subscribers  
• for each subscriber: invoke StrategyEngine → RiskEngine → PositionManager → ExecutionEngine  

Market data and indicator calculation are shared.
Strategy evaluation and order execution are per-subscriber.

---

# Strategy Trigger Timeframe

For the current trading strategy:

4H trend filter  
1H bias  
15m pullback entry  

The strategy should execute on:

15m candle close

Higher timeframes are inputs to the strategy but do not trigger execution.

---

# Live Trading Flow

Hyperliquid WebSocket  
↓  
MarketStateStore (shared)  
↓  
CandleBuilder  
↓  
CandleClock  
↓  
StrategyScheduler  
↓  
For each active subscriber:  
  StrategyEngine  
  ↓  
  RiskEngine  
  ↓  
  PositionManager  
  ↓  
  ExecutionEngine (using subscriber's keys)  

Websocket updates continuously update the MarketStateStore.
Strategy evaluation occurs only when a candle closes.
Market data is shared; execution is per-subscriber.

---

# Backtesting Flow

HistoricalDataProvider  
↓  
ReplayEngine  
↓  
CandleClock  
↓  
StrategyScheduler  
↓  
StrategyEngine  
↓  
RiskEngine  
↓  
PositionManager  
↓  
SimulatedExecutionEngine  

The replay engine feeds candles sequentially to the CandleClock,
which emits the same CandleClosedEvent used in live trading.

This ensures identical behaviour in both environments.

---

# Duplicate Execution Protection

The scheduler must ensure strategies run once per candle.

Recommended approach:

Store a checkpoint of the last processed candle.

Example model:

public class StrategyExecutionCheckpoint
{
    public string UserId { get; set; }
    public string Symbol { get; set; }
    public string Timeframe { get; set; }
    public long LastProcessedCloseTimeUtc { get; set; }
}

This checkpoint prevents duplicate runs after restarts.
Checkpoints are per-subscriber.

---

# Continuous vs Scheduled Tasks

The system should distinguish between:

Event-driven operations (continuous)
• order updates
• fills
• position updates

Scheduled operations (candle close)
• strategy evaluation
• indicator updates
• risk checks

This separation prevents unnecessary strategy executions.

---

# Implementation Example

Simplified CandleClock logic:

public class CandleClock
{
    private readonly Dictionary<string,long> _lastClosed = new();

    public event Func<CandleClosedEvent,Task>? CandleClosed;

    public async Task ProcessCandleAsync(Candle candle)
    {
        // Candle domain entity uses Interval (not Timeframe) and Timestamp (open time, Unix ms)
        // CloseTimeUtc is derived — it does not exist as a property on Candle
        var key = $"{candle.Symbol}:{candle.Interval}";
        var closeTimeUtc = candle.Timestamp + GetIntervalMs(candle.Interval);

        if (_lastClosed.TryGetValue(key, out var last) && last >= closeTimeUtc)
            return;

        _lastClosed[key] = closeTimeUtc;

        if (CandleClosed is not null)
        {
            await CandleClosed.Invoke(new CandleClosedEvent
            {
                Symbol = candle.Symbol,
                Timeframe = candle.Interval,    // CandleClosedEvent.Timeframe ← Candle.Interval
                OpenTimeUtc = candle.Timestamp,  // Candle.Timestamp is the open time
                CloseTimeUtc = closeTimeUtc,
                Candle = candle
            });
        }
    }
}

---

# StrategyScheduler

`src/TradingApp.Application/Scheduling/StrategyScheduler.cs`

**Constructor** takes the five pipeline services plus `string strategyConfigJson` and optional `string triggerTimeframe` (default `"15m"`).

**Key methods:**

```csharp
// Called by CandleClock; latestOneHourCandle and latestFourHourCandle are resolved
// by the caller (BacktestRunner or live worker) before invoking
public async Task HandleCandleClosedAsync(
    CandleClosedEvent evt,
    Candle? latestOneHourCandle,
    Candle? latestFourHourCandle,
    CancellationToken cancellationToken = default)

// State management — caller updates position/grid state before each candle event
public void UpdateState(GridState gridState, PositionState positionState)
public GridState GetGridState()
```

The scheduler filters on `evt.Timeframe == triggerTimeframe`, then drives the pipeline:
`IMarketContextBuilder.Build` → `IStrategyEngine.EvaluateAsync` → `IGridController.ProcessAsync` → `IRiskEngine.ValidateAsync` → `IPositionManager.ExecuteSignalsAsync`.

---

# Benefits of this Architecture

• consistent timing between live trading and backtesting  
• eliminates double strategy execution  
• avoids trading on partially formed candles  
• clean separation between time management and trading logic  

---

# Folder Structure

```
src/TradingApp.Application/
└── Scheduling/
    ├── CandleClock.cs          # Emits CandleClosedEvent; deduplicates per candle
    ├── StrategyScheduler.cs    # Drives pipeline on trigger timeframe candle close
    └── Models/
        └── CandleClosedEvent.cs  # { Symbol, Timeframe, OpenTimeUtc, CloseTimeUtc, Candle }
```

Note: `ICandleClock`, `IStrategyScheduler`, and `StrategyExecutionCheckpoint` are not yet implemented.

---

# Summary

The CandleClock + StrategyScheduler pattern ensures reliable timing for the trading system.

CandleClock handles candle close detection.

StrategyScheduler decides when strategies should run.

Together they create a deterministic scheduling system that works
in both live trading and historical replay environments.