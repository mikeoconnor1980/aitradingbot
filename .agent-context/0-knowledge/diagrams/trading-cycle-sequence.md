# TradePilot - Trading Cycle Sequence

This sequence reflects the current worker-driven live trading loop and the control path used by the API to start and stop that loop.

```mermaid
sequenceDiagram
    autonumber

    participant UI as Browser UI
    participant API as TradePilot.Api
    participant ACI as AgentCheckInService
    participant TS as TradingSession
    participant WS as HyperliquidWebSocketClient
    participant CB as CandleBuilder
    participant CC as CandleClock
    participant SS as StrategyScheduler
    participant GC as GridController
    participant SC as SignalController
    participant RM as LiveRiskEngine
    participant PM as LivePositionManager
    participant EXE as LiveExecutionEngine
    participant EX as Hyperliquid
    participant DB as TradePilotDbContext
    participant AI as ILlmContextClient (Optional)

    UI->>API: Start trading command

    loop Heartbeat every 5 seconds
        ACI->>API: POST /api/agent/heartbeat
        API-->>ACI: HeartbeatResponse(PendingCommands = Start/Stop/...)
    end

    API-->>ACI: Start command delivered in heartbeat response
    ACI->>TS: CreateSession(config) and start runtime

    TS->>WS: Connect to Hyperliquid trades stream
    WS-->>CB: Trade tick
    CB->>CB: Bucket tick into 15m / 1h / 4h accumulators

    alt First tick of next bucket arrives
        CB->>DB: Persist confirmed Candle
        CB->>CC: ProcessCandleAsync(confirmed candle)
        CC-->>SS: HandleCandleClosedAsync
    end

    SS->>DB: Load GridCycle, LiveOrder, LiveFill state

    opt Optional live context enrichment
        SS->>AI: Request market-context overlay
        AI-->>SS: Context snapshot / derived regime
    end

    alt StrategyMode = Grid
        SS->>GC: ProcessAsync(evaluation, context, gridState)
        GC-->>SC: TradingSignal[]
    else StrategyMode = Signal
        SS->>SC: ProcessAsync(evaluation, context, gridState, positionState)
    end

    Note over GC,SC: SignalController is the final signal boundary before risk and execution.

    SC->>RM: ValidateAsync(signals, context)

    alt Signals blocked
        RM-->>SS: Rejected signals / reasons
        SS->>DB: Persist state updates only
    else Signals approved
        RM-->>SC: Approved TradingSignal[]
        SC->>PM: ExecuteSignalsAsync(approved signals)
        PM->>EXE: Place / cancel / amend orders
        EXE->>EX: Signed REST /exchange request
        EX-->>EXE: Ack / fill / order status
        EXE->>DB: Persist LiveOrder / LiveFill / GridCycle changes
    end

    EX-->>TS: userEvents stream updates
    TS->>DB: Reconcile fills and position state
```

## Future Recommendations

- Add a companion sequence for agent update rollout through `UpdateCheckerService`.
- Add a separate failure-path diagram for reconnects, kill-switch shutdown, and command retry behavior.