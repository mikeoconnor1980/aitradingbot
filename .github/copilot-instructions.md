# Copilot Instructions

## Project Knowledge

Before working on this project, read the knowledge files in `.agent-context/0-knowledge/`.
These files define the project architecture, domain model, trading strategy, infrastructure,
and business model decisions. They are the source of truth.

Key files:

- `00-project-overview.md` — what this project is, business model options
- `01-trading-strategy.md` — grid strategy, timeframes, entry/exit logic
- `02-hyperliquid-integration.md` — exchange API, authentication, multi-tenant connections
- `03-infrastructure-architecture.md` — phased deployment (VPS → Azure), components
- `04-domain-model.md` — core entities (User, Strategy, Order, Position, etc.)
- `05-feature-specification.md` — features including subscription and admin
- `06-project-structure.md` — solution layout, project names
- `10-architecture-decisions.md` — ADRs (language, database, multi-tenancy, auth, etc.)
- `14-strategy-runtime-model.md` — how strategies execute per-subscriber
- `15-grid-controller.md` — grid lifecycle state machine
- `16-signal-contracts.md` — signal types and lifecycle
- `17-llm-context-sentiment-architecture.md` — LLM integration as context provider
- `18-backtesting-architecture.md` — replay engine, simulated execution
- `19-scheduling-architecture.md` — CandleClock, StrategyScheduler, per-user fan-out
- `20-business-model-options.md` — Options A/B/C for subscription delivery model

Review files in `.agent-context/1-discover/review/` contain competitive analysis.

## Tech Stack

- Backend: C# / .NET
- Frontend: Angular
- Database: SQLite (POC), Azure SQL (production)
- Deployment: Docker Compose (VPS), Azure Container Apps (cloud)
- Exchange: Hyperliquid (DEX perpetuals)

## Key Principles

- Strategies execute on confirmed candle closes only (deterministic execution)
- All orders pass through the RiskEngine — strategies never bypass risk checks
- Backtesting reuses the same StrategyEngine, GridController, and RiskEngine as live trading
- The LLM provides context signals (sentiment, regime, event risk) — it never places trades
- All data is tenant-scoped by UserId in a multi-tenant architecture
- Signal contracts (DeployGrid, TakeProfit, OpenHedge, etc.) define the boundary between strategy logic and execution
