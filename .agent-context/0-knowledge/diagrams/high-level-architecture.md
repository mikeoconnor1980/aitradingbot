# AITradingBot - High-Level Architecture

```mermaid
flowchart LR

    subgraph Users["Users / Operators"]
        U1["Trader / Admin"]
    end

    subgraph Config["Configuration & Control"]
        CFG["Bot Config
        - tenant settings
        - strategy params
        - risk limits
        - exchange credentials refs"]
        FEAT["Feature Flags / Kill Switches"]
    end

    subgraph App["AITradingBot Application"]
        API["Control API / Admin UI"]
        ORCH["Bot Orchestrator"]
        CC["CandleClock"]
        SS["StrategyScheduler"]
        SE["Strategy Engine"]
        RM["Risk Manager"]
        PM["Portfolio / Position Manager"]
        EXE["Execution Engine"]
        EVT["Event Bus / Internal Messages"]
    end

    subgraph Data["Market & External Services"]
        MDS["Market Data Service
        - candles
        - trades
        - order book"]
        AI["AI / LLM Decision Support
        optional"]
        EX["Exchange Connector
        - Hyperliquid / other venues"]
    end

    subgraph Persistence["Persistence"]
        STATE["Bot State Store"]
        ORD["Orders / Fills Store"]
        MDH["Market Data Cache / History"]
        DEC["Strategy Decision Log"]
    end

    subgraph Ops["Observability & Safety"]
        LOG["Structured Logging"]
        MET["Metrics / Monitoring"]
        ALT["Alerts / Notifications"]
        AUD["Audit Trail"]
    end

    subgraph Test["Simulation / Research"]
        BT["Backtest Runner"]
        REPLAY["Historical Replay Engine"]
    end

    U1 --> API
    API --> CFG
    API --> FEAT
    API --> ORCH

    CFG --> ORCH
    FEAT --> ORCH

    ORCH --> CC
    CC --> SS
    SS --> EVT

    MDS --> EVT
    EVT --> SE
    SE --> RM
    RM --> PM
    PM --> EXE
    EXE --> EX
    EX --> ORD
    EX --> PM

    SE --> DEC
    RM --> DEC
    PM --> STATE
    EXE --> AUD

    ORCH --> LOG
    ORCH --> MET
    ORCH --> ALT
    EXE --> LOG
    RM --> LOG
    SE --> LOG

    MDS --> MDH

    REPLAY --> BT
    MDH --> REPLAY
    BT --> SE
    BT --> RM
    BT --> PM
    BT --> DEC

    AI -. optional signal enrichment .-> SE