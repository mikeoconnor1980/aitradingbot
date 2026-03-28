# 0-knowledge — Table of Contents

Project knowledge files for the AI Grid Trading Bot. These are the source of truth
for architecture, domain model, trading strategy, infrastructure, and business decisions.

---

## Foundation

| # | Document | Description |
|---|---|---|
| 00 | [Project Overview](00-project-overview.md) | What this project is, goals, business model options |
| 01 | [Trading Strategy](01-trading-strategy.md) | Grid strategy, timeframes, entry/exit logic |
| 02 | [Hyperliquid Integration](02-hyperliquid-integration.md) | Exchange API, authentication, multi-tenant connections |
| 03 | [Infrastructure Architecture](03-infrastructure-architecture.md) | Phased deployment (VPS → Azure), components |
| 04 | [Domain Model](04-domain-model.md) | Core entities (User, Strategy, Order, Position, etc.) |
| 05 | [Feature Specification](05-feature-specification.md) | Features including subscription and admin |
| 06 | [Project Structure](06-project-structure.md) | Solution layout, project names |

## Frontend & UI

| # | Document | Description |
|---|---|---|
| 07 | [UI Design](07-ui-design.md) | Dashboard and strategy configuration UI |
| 09 | [Charting Library](09-charting-library.md) | TradingView Lightweight Charts |
| 11 | [Angular Instructions](11-angular-instructions.md) | Angular coding standards and folder structure |

## Strategy & Execution

| # | Document | Description |
|---|---|---|
| 12 | [Strategy Customisation](12-strategy-customisation.md) | How users create and configure strategy instances |
| 13 | [Strategy Config Schema](13-strategy-config-schema.md) | Complete JSON schema for strategy configuration |
| 14 | [Strategy Runtime Model](14-strategy-runtime-model.md) | How strategies execute per-subscriber |
| 15 | [Grid Controller](15-grid-controller.md) | Grid lifecycle state machine |
| 16 | [Signal Contracts](16-signal-contracts.md) | Signal types and lifecycle |
| 17 | [LLM Context & Sentiment](17-llm-context-sentiment-architecture.md) | LLM integration as context provider |
| 18 | [Backtesting Architecture](18-backtesting-architecture.md) | Replay engine, simulated execution |
| 19 | [Scheduling Architecture](19-scheduling-architecture.md) | CandleClock, StrategyScheduler, per-user fan-out |

## Architecture & Planning

| # | Document | Description |
|---|---|---|
| 08 | [Development Plan](08-development-plan.md) | Phased build roadmap |
| 10 | [Architecture Decisions](10-architecture-decisions.md) | ADRs (language, database, multi-tenancy, auth, etc.) |

## Business Model

| # | Document | Description |
|---|---|---|
| 20 | [Business Model Options](20-business-model-options.md) | Options A/B/C for subscription delivery model |
| 21 | [Business Model — Legal](21-business-model-options-legal.md) | Legal considerations (placeholder) |

## Operations

| # | Document | Description |
|---|---|---|
| 22 | [Pre-Launch Checklist](22-prelaunch-checklist.md) | Required audit checks before launching as a paid SaaS product |

## Market Data

| # | Document | Description |
|---|---|---|
| 23 | [Binance Integration](23-binance-integration.md) | Binance USDⓈ-M Futures as historical data source — klines, mark price klines, funding rates |

---

## Subfolders

| Folder | Description |
|---|---|
| [data-model/](data-model/) | Entity relationship diagrams ([ERD](data-model/data-model-erd.md)) |
| [diagrams/](diagrams/) | Architecture and sequence diagrams |
| [innovative-features/](innovative-features/) | Differentiating features — Replay Debugger, NL Explanations, Adversarial Stress Testing |
