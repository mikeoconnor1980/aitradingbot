# 0-knowledge - Table of Contents

Project knowledge files for TradingApp. These documents are the source of truth for the implemented architecture, domain model, strategy runtime, control plane, and research tooling.

---

## Foundation

| # | Document | Description |
|---|---|---|
| 00 | [Project Overview](00-project-overview.md) | Product overview, Option C split architecture, auth, optimizer, and core runtime priorities |
| 01 | [Trading Strategy](01-trading-strategy.md) | Implemented strategy engine behavior, regime gating, drawdown tiers, and signal paths |
| 02 | [Hyperliquid Integration](02-hyperliquid-integration.md) | Hyperliquid REST/WebSocket integration, signing model, routing, and exchange-facing services |
| 03 | [Infrastructure Architecture](03-infrastructure-architecture.md) | Local and Azure runtime topology, SignalR behavior, Bicep infrastructure, and secret boundaries |
| 04 | [Domain Model](04-domain-model.md) | Current entity model including `GridCycle`, `LiveOrder`, `LiveFill`, optimization, macro, and review entities |
| 05 | [Feature Specification](05-feature-specification.md) | Current shipped product surface and clearly marked non-implemented features |
| 06 | [Project Structure](06-project-structure.md) | Solution layout, project roles, and major feature folders |

## Frontend & UI

| # | Document | Description |
|---|---|---|
| 07 | [UI Design](07-ui-design.md) | Current route map, dashboard composition, strategy flows, auth pages, and agents UI |
| 09 | [Charting Library](09-charting-library.md) | Three-pane candlestick and equity chart architecture with indicators and fill markers |
| 11 | [Angular Instructions](11-angular-instructions.md) | Angular app structure, guards, interceptors, theme tokens, and frontend conventions |

## Strategy & Execution

| # | Document | Description |
|---|---|---|
| 12 | [Strategy Customisation](12-strategy-customisation.md) | Strategy creation, revisioning, review, sizing enums, and authoring terminology |
| 13 | [Strategy Config Schema](13-strategy-config-schema.md) | Current strategy configuration model, condition types, and known schema quirks |
| 14 | [Strategy Runtime Model](14-strategy-runtime-model.md) | Runtime orchestration across scheduler, controller, risk, and execution services |
| 15 | [Grid Controller](15-grid-controller.md) | Implemented grid lifecycle, state handling, and emitted signals |
| 16 | [Signal Contracts](16-signal-contracts.md) | Current live signal contracts, payloads, and not-yet-implemented signal types |
| 17 | [LLM Context & Sentiment](17-llm-context-sentiment-architecture.md) | LLM market-context architecture and synthetic regime fallback |
| 18 | [Backtesting Architecture](18-backtesting-architecture.md) | Queued replay engine, simulated execution, shared runtime services, and progress updates |
| 19 | [Scheduling Architecture](19-scheduling-architecture.md) | `CandleClock`, `StrategyScheduler`, state updates, and live/backtest scheduling flow |
| 24 | [Backtesting Grid Engine Explained](24-backtesting-grid-engine-explained.md) | How `GridStrategyEngine`, `IRiskEngine`, and `GridController` interact during backtesting |
| 24 | [Strategy Interpreter Architecture](24-strategy-interpreter-architecture.md) | Natural-language strategy interpretation and its relationship to review and context services |
| 31 | [ATR Calculation](31-atr-calculation.md) | ATR smoothing, stop logic, and exchange-native trigger placement behavior |
| 33 | [Risk Management & Trade Sizing](33-risk-management-and-trade-sizing.md) | Position sizing modes, drawdown tiers, portfolio heat, and risk-engine behavior |
| 35 | [Strategy Optimizer](35-strategy-optimizer.md) | Parameter sweep, evolutionary search, walk-forward OOS validation, and fitness metrics |

## Architecture & Planning

| # | Document | Description |
|---|---|---|
| 08 | [Development Plan](08-development-plan.md) | Build roadmap versus delivered features and remaining gaps |
| 10 | [Architecture Decisions](10-architecture-decisions.md) | ADRs for the implemented system, including post-build ADRs |

## Business Model

| # | Document | Description |
|---|---|---|
| 20 | [Business Model Options](20-business-model-options.md) | Options A/B/C - **Option C chosen** |
| 21 | [Business Model - Legal](21-business-model-options-legal.md) | Placeholder legal review checklist for launch readiness |

## Operations

| # | Document | Description |
|---|---|---|
| 22 | [Pre-Launch Checklist](22-prelaunch-checklist.md) | Launch-readiness checklist with completed controls and open operational gaps |
| 28 | [Macro Calendar](28-macro-calendar.md) | Economic calendar integration for trade-blocking during high-impact events |
| 29 | [Control Plane → Agent Architecture](29-control-plane-agent-architecture.md) | Heartbeat protocol, command routing, kill switch, and update flow between API and worker |
| 30 | [Worker Execution Pipeline](30-worker-execution-pipeline.md) | End-to-end live execution flow from trade ticks to fills, persistence, and agent services |
| 34 | [Google SSO Authentication](34-google-sso-authentication.md) | Google OAuth integration via Google Identity Services |
| 36 | [Notification Architecture](36-notification-architecture.md) | Unified notification dispatch (backend) and facade (frontend), channel routing, and extension guide |
| 37 | [TradingView Webhooks](37-tradingview-webhooks.md) | Pro-tier webhook ingress, TradingView setup steps, payload contract, and agent-routing behavior |

## Market Data

| # | Document | Description |
|---|---|---|
| 23 | [Binance Integration](23-binance-integration.md) | Binance USD-M futures ingestion for historical candles and funding rates |

## Architecture Quality

| # | Document | Description |
|---|---|---|
| 26 | [Architecture Review](26-architecture-review.md) | Strengths, risks, and mitigations for the current architecture |

---

## Subfolders

| Folder | Description |
|---|---|
| [data-model/](data-model/) | Data model documentation including the [ERD](data-model/data-model-erd.md) |
| [diagrams/](diagrams/) | High-level architecture and sequence diagrams for the implemented runtime |
| [innovative-features/](innovative-features/) | Research and exploratory differentiators that are not part of the main knowledge index |
