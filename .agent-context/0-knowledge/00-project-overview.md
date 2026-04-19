# AI Grid Trading System

## Overview

TradePilot is a multi-tenant algorithmic trading platform for Hyperliquid perpetuals. It combines a deterministic strategy engine, a browser-based control plane, a client-side execution agent, reusable indicator libraries, AI-assisted tooling, and a backtesting and optimization stack built around the same strategy primitives used in live trading.

The current implementation is centered on BTC perpetual trading, but the project structure is intended to support additional strategies, indicators, assets, and deployment targets over time.

## Business Model

The implemented model is Option C: Split Architecture.

Under Option C:

- the API and Angular UI act as the control plane
- the execution agent is the `TradePilot.ExecutionAgent` Windows Service built from `src/TradePilot.Worker`
- wallet addresses are stored in the platform database, but private keys never touch the server
- order signing happens locally on the execution agent through `MutableSignerProvider` and live execution services

This model was chosen to preserve centralized strategy management and monitoring without storing customer private keys in the cloud.

## Core Priorities

The codebase is organized around a few non-negotiable priorities:

- deterministic candle-close execution
- shared trading logic across live trading and backtesting
- strong separation between strategy, risk, position management, and execution
- tenant-scoped data access by `UserId`
- secure key handling through client-side execution

## Core System Components

| Component | Purpose |
|-----------|---------|
| `TradePilot.Domain` | Core entities such as users, strategies, market data, runs, orders, and optimization records |
| `TradePilot.Application` | CQRS handlers, trading pipeline abstractions, scheduling, macro calendar, subscriptions, and optimization orchestration |
| `TradePilot.Infrastructure` | Hyperliquid, Binance, auth, SignalR, signing, and external service implementations |
| `TradePilot.Persistence` | EF Core context, migrations, and repository implementations |
| `TradePilot.Api` | ASP.NET Core control plane for auth, strategies, market data, backtesting, optimization, profile, and agent coordination |
| `TradePilot.AI` | Strategy interpretation, AI review, and other OpenAI-compatible LLM integrations |
| `TradePilot.Indicators` | Standalone technical indicator library with ATR, Bollinger, EMA, MACD, RSI, Support/Resistance, and incremental calculators |
| `TradePilot.Worker` | Builds the `TradePilot.ExecutionAgent` Windows Service used for client-side execution |
| `frontend/trading-ui` | Angular control plane UI for trading, strategy authoring, auth, optimizer, macro calendar, and agent management |

## Strategy and Execution Model

The current live strategy stack is a grid-oriented trading pipeline driven by confirmed candle closes.

At a high level:

Confirmed candles
-> `CandleClock`
-> `StrategyScheduler`
-> `IStrategyEngine`
-> `ISignalController`
-> `IRiskEngine`
-> `IPositionManager`
-> `IExecutionEngine`

The same core architecture is reused in backtesting so that the code path for signal generation, risk evaluation, and grid lifecycle handling stays aligned across simulation and live execution.

## AI, Context, and Risk Gating

AI is used as a context provider and authoring aid, not as an autonomous trading engine.

Implemented AI-adjacent capabilities include:

- strategy interpretation from natural language into strategy configuration
- AI review of strategy revisions
- optional LLM market-context enrichment for live trading
- synthetic fallback behavior when live LLM context is not configured

Macro events are also part of the trading gate. The macro calendar subsystem syncs economic events and blocks trading during configurable pre-event and post-event windows for high-impact events.

## Backtesting and Optimization

Backtesting reuses the strategy pipeline and persists run metadata for later analysis.

The platform also includes a strategy optimizer with:

- parameter sweep execution
- evolutionary search
- walk-forward out-of-sample validation
- fitness scoring based on metrics such as Sharpe, Sortino, Calmar, Kelly, and profit factor

This makes the platform more than a single trading bot. It is a research and execution environment for strategy design, validation, and controlled live rollout.

## Authentication and User Model

Authentication is fully implemented in the control plane.

Current auth capabilities include:

- email and password registration
- JWT access and refresh tokens
- Google OAuth sign-in
- claim-based identity resolution through `HttpContext`

The project does not currently use Azure AD B2C or Auth0.

## Technology Stack

| Area | Current Choice |
|------|----------------|
| Backend | .NET 10 / C# |
| Frontend | Angular standalone application |
| Local data store | SQLite |
| Production relational store | Azure SQL via Bicep infrastructure |
| Exchange integration | Hyperliquid for live trading, Binance for historical data ingestion |
| Real-time browser push | SignalR, with Azure SignalR in Azure deployments |
| AI integration | OpenAI-compatible HTTP clients for interpretation, review, and market context |

## Project Status

The project is beyond the original proof-of-concept stage in code shape, but it is still an actively evolving system. The implemented surface area now includes live trading control-plane features, a client-side execution agent, auth, strategy authoring, backtesting, optimization, macro calendar gating, and real-time browser updates.

## Future Recommendations

- Add an admin dashboard for operational monitoring, tenant diagnostics, and support tooling.
- Add Stripe or equivalent billing integration for paid subscription tiers.
- Add additional strategy families such as TrendBreakout, MeanReversion, and FundingArbitrage.
- Add strategy sharing or marketplace capabilities for reusable templates and revisions.
- Add a dedicated mobile app once the web control plane stabilizes.

- additional strategies
- multi-exchange support
- automated parameter optimisation
- portfolio-level risk management

---

# Disclaimer

This project is for research and experimentation only.

Algorithmic trading involves significant financial risk and no guarantee of profitability.