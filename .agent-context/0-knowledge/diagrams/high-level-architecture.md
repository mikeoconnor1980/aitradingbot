# TradePilot - High-Level Architecture

The deployed system is no longer a single bot host. It is a split architecture with an API control plane, a browser UI, and a client-side Windows execution agent that holds the private key and talks to Hyperliquid directly.

```mermaid
flowchart LR
    subgraph Users["Users"]
        Browser["Browser / Angular UI"]
        Operator["Trader / Operator"]
    end

    subgraph ControlPlane["Cloud Control Plane"]
        API["TradePilot.Api\nREST API + MarketDataHub"]
        APIJobs["BacktestProcessorService\nOptimizationProcessorService\nMacroCalendarSyncWorker"]
        SignalR["SignalR / Azure SignalR"]
    end

    subgraph Agent["Client Execution Agent"]
        Worker["TradePilot.Worker\nTradePilot.ExecutionAgent"]
        CheckIn["AgentCheckInService\nheartbeat every 5s"]
        Session["TradingSession\nCandleClock\nStrategyScheduler\nGridController / SignalController\nLiveRiskEngine\nLivePositionManager\nLiveExecutionEngine"]
    end

    subgraph External["External Services"]
        Hyperliquid["Hyperliquid\nWebSocket + REST"]
        Llm["AI / LLM Services\noptional"]
        Binance["Binance Historical Data"]
    end

    subgraph Persistence["Persistence"]
        GridCycleStore["GridCycle"]
        OrderStore["LiveOrder / LiveFill"]
        MarketStore["Candle / FundingRate"]
        RunStore["BacktestRun / OptimizationRun"]
    end

    Operator --> Browser
    Browser -->|REST| API
    Browser <-->|SignalR| SignalR
    API --> SignalR

    Worker --> CheckIn
    CheckIn -->|POST /api/agent/heartbeat| API
    API -->|HeartbeatResponse\nPendingCommands / MustShutdown / Update metadata| CheckIn
    CheckIn --> Session

    Session -->|REST /exchange| Hyperliquid
    Hyperliquid -->|trades / userEvents WebSocket| Session
    Session -->|real-time updates when configured| SignalR

    API --> GridCycleStore
    API --> OrderStore
    API --> MarketStore
    API --> RunStore
    Session --> GridCycleStore
    Session --> OrderStore
    Session --> MarketStore

    Binance --> MarketStore
    APIJobs --> RunStore
    MarketStore --> APIJobs
    APIJobs --> SignalR

    Llm -. interpretation / review / market context .-> API
    Llm -. optional live context overlay .-> Session
```

## Notes

- Browser traffic is split between REST calls to `TradePilot.Api` and SignalR subscriptions through `MarketDataHub` or Azure SignalR.
- Worker-to-API control is poll-based: the worker heartbeats every five seconds and receives queued commands in the heartbeat response.
- The worker, not the API, owns live exchange connectivity, order signing, and direct Hyperliquid order placement.
- Persistence names align to the current domain model: `GridCycle`, `LiveOrder`, `LiveFill`, `Candle`, `FundingRate`, `BacktestRun`, and `OptimizationRun`.
- AI integrations remain optional overlays rather than autonomous trade execution.

## Future Recommendations

- Add a companion deployment diagram that distinguishes local SignalR hosting from Azure SignalR publishing.
- Add a separate control-plane sequence diagram for agent update rollout and kill-switch flows.