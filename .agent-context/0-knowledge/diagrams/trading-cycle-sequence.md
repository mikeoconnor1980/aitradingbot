## `0 Knowledge/diagrams/trading-cycle-sequence.md`
```md
# AITradingBot - Trading Cycle Sequence

```mermaid
sequenceDiagram
    autonumber

    participant MDS as Market Data Service
    participant CC as CandleClock
    participant SS as StrategyScheduler
    participant ORCH as Bot Orchestrator
    participant SE as Strategy Engine
    participant AI as AI/LLM Support (Optional)
    participant RM as Risk Manager
    participant PM as Portfolio/Position Manager
    participant EXE as Execution Engine
    participant EX as Exchange Connector
    participant DB as State & Orders Store
    participant LOG as Decision Log / Observability

    Note over MDS,LOG: One full cycle begins when a market candle is considered complete

    MDS->>CC: New market data arrives
    CC->>CC: Validate timeframe boundary\nand confirm candle is closed
    CC-->>SS: CandleClosed(symbol, timeframe, closeTime)

    SS->>SS: Resolve eligible strategies\nfor symbol/timeframe/tenant
    SS-->>ORCH: RunStrategy(strategyId, marketContext)

    ORCH->>DB: Load bot state / open positions / config
    DB-->>ORCH: Current state snapshot

    ORCH->>SE: Evaluate strategy with closed candle + context
    SE->>SE: Calculate indicators / features

    opt Optional AI enrichment
        SE->>AI: Request contextual enrichment / scoring
        AI-->>SE: AI signal context
    end

    SE-->>ORCH: Proposed signal\n(Buy / Sell / Exit / Hold)

    ORCH->>LOG: Record raw strategy decision
    ORCH->>RM: Check risk on proposed signal

    RM->>DB: Read limits, exposure, cooldowns,\nopen orders, drawdown state
    DB-->>RM: Risk context
    RM->>PM: Check current portfolio / position exposure
    PM-->>RM: Position summary

    alt Signal blocked by risk
        RM-->>ORCH: Rejected(reason)
        ORCH->>LOG: Record rejection reason
        ORCH-->>SS: Strategy cycle complete - no trade
    else Signal approved
        RM-->>ORCH: Approved(order intent)
        ORCH->>PM: Build desired position transition
        PM-->>ORCH: Execution plan\n(open / add / reduce / close)

        ORCH->>EXE: Execute approved plan
        EXE->>EXE: Normalize order sizes,\nprices, slippage, precision rules
        EXE->>EX: Place / amend / cancel order(s)

        EX-->>EXE: Order acknowledgement
        EXE->>LOG: Record submitted order event
        EXE->>DB: Persist order intent + ack

        alt Immediate fill or partial fill received
            EX-->>EXE: Fill / partial fill event
            EXE->>PM: Apply fill to position state
            PM->>DB: Persist updated position / PnL / exposure
            EXE->>LOG: Record fill event
        else Order remains working
            EX-->>EXE: Working order status
            EXE->>DB: Persist open order state
            EXE->>LOG: Record working status
        end

        EXE-->>ORCH: Execution result
        ORCH->>DB: Persist cycle outcome / heartbeat / timestamps
        ORCH->>LOG: Record completed trading cycle
        ORCH-->>SS: Strategy cycle complete
    end