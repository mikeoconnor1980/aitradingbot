# Architecture Review and Known Risks

This review aligns the risk picture with the implemented split-architecture system rather than the older single-host assumptions.

## Architecture Strengths

| Strength | Detail |
|---|---|
| Deterministic scheduling | Strategy execution still runs on confirmed candle closes through `CandleClock` and `StrategyScheduler` |
| Shared research and execution primitives | Backtesting, optimization, and live trading reuse the same strategy and risk concepts |
| Split control plane and execution plane | The API manages orchestration and visibility while the execution agent keeps key custody local |
| Operational control surface | `AgentCommandStore`, `AgentController`, the Agents page, and the kill switch provide a real control-plane layer |
| Managed real-time path in Azure | Azure SignalR is the production push layer instead of a self-managed Redis design |
| Fallback-aware AI usage | `SyntheticRegimeProvider` gives the system a non-LLM fallback for regime classification |

## Updated Risk Register

| Risk | Current Status | Notes |
|---|---|---|
| 1. SQLite concurrent write contention | Still valid | Local SQLite remains a practical bottleneck risk until Azure SQL is the dominant runtime |
| 2. `MarketDataStreamService` hosted in API | Partially resolved | The service now exists in both runtime modes: locally in the API, and conditionally in the worker for Azure SignalR deployments |
| 3. LLM latency on critical path | Partially resolved | LLM context remains optional and `SyntheticRegimeProvider` provides an always-available fallback |
| 4. No full decision event sourcing | Still valid | Backtesting now has richer audit output, but live decision-state persistence is still lighter than full event sourcing |
| 5. Per-user key security | Resolved by architecture choice | Option C removes server-side private-key custody from the API platform |
| 6. Worker fan-out concurrency model | Reframed | The main live model is now one user per execution-agent deployment, so the earlier multi-subscriber server-worker fan-out concern is reduced |

## New Architectural Components Worth Calling Out

The risk profile changed because the architecture changed. The biggest additions are:

- `AgentCommandStore` as the in-memory control mechanism for agent registration, pending commands, and kill-switch state
- `TradingApp.ExecutionAgent` as the Windows Service delivery target for live execution
- `UpdateCheckerService` for agent update distribution and safe apply behavior
- Azure SignalR publishing for cloud-hosted browser updates
- execution-agent installer and checksum distribution pipeline

## Risk Commentary

### SQLite Contention

This remains a real limitation in local and small-footprint modes. It is less a design mystery now and more a known scaling boundary.

### Market Data Hosting

The earlier critique that streaming lived only in the API is no longer fully accurate. The implementation now supports API-hosted local streaming and worker-hosted Azure streaming. That lowers, but does not eliminate, operational complexity.

### LLM Dependency

The risk is no longer simply "LLM outage blocks trading." The existence of `SyntheticRegimeProvider` makes the system materially safer than that older description suggested.

### Key Security

This is the largest resolved item. The old risk assumed server-side custody of trading credentials. The current control-plane comments, worker packaging, and signer placement show that this is no longer the model.

### Fan-Out and Capacity

The live execution model is not a central worker iterating every subscriber. It is a fleet of single-tenant or low-cardinality execution agents coordinated by the control plane. That shifts scale concerns from scheduler fan-out to fleet management, update rollout, agent health, and command durability.

## Priority Order

For production hardening, the most important remaining architecture work is:

1. Add durable command and update-operability guarantees beyond the current in-memory control-plane queue.
2. Move secret management to a managed secret store.
3. Add stronger live decision auditability for debugging and post-incident review.
4. Finish operational controls such as emergency flatten and fuller circuit-breaker automation.
5. Define agent-fleet operational tooling, capacity expectations, and rollout policies.

## Future Recommendations

- Add an architecture decision and review pass specifically for command durability and agent authentication.
- Add a live-trading audit trail that captures enough context for post-trade explanation without full event-sourcing overhead.
- Add production observability standards covering correlation IDs, metrics, alerts, and update rollout visibility.
