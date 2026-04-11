# PRD: TradingView MCP Integration

**Status:** Draft  
**Priority:** Medium (extends existing strategy authoring and market context pipelines)  
**Date:** 2026-04-11  
**Author:** PRD Writer Agent  
**Depends on:** PRD-02 (Strategy Input Pipeline) for the authoring path; PRD-04 (Backtesting) for simulation  
**Depended on by:** None currently  

---

## 1. Background & Context

### Problem Statement

The platform currently supports two strategy authoring entry points:

1. **Form-based** — structured UI selectors mapped directly to `StrategyConfig` JSON
2. **Natural language** — LLM interprets plain English into a `StrategyIntentDto` → AST → canonical JSON

Both entry points require the user to *know what strategy they want*. There is no path for users who start from external market analysis — particularly from TradingView, the dominant retail charting and screening platform — and want to translate those insights into an executable strategy within TradePilot.

Additionally, the `MarketContextBuilder` currently relies on candle-derived indicators and the LLM sentiment provider for market context. It has no integration with external technical analysis services that offer pre-computed multi-timeframe signals, screening results, or sentiment aggregation.

### Current State

- **Strategy authoring**: PRD-02 defines a structured pipeline: Input Adapters → AST → Canonical JSON → Runtime Compiler → Engine. UI selectors (Phase 1) and natural language (Phase 2) are the two defined input paths. Pine Script import is deferred to Phase 3.
- **Market context**: `MarketContextBuilder` aggregates candle-based indicators (EMA, RSI, ATR, etc.) and optional `LlmContext` (sentiment, macro regime, event risk). See [17-llm-context-sentiment-architecture.md](../0-knowledge/17-llm-context-sentiment-architecture.md).
- **LLM integration**: Three LLM patterns exist — context provider, strategy interpreter, strategy reviewer. All use OpenAI-compatible HTTP clients. See [24-strategy-interpreter-architecture.md](../0-knowledge/24-strategy-interpreter-architecture.md).
- **Backtesting**: PRD-04 defines replay-based backtesting and paper trading. Both share the same pipeline as live trading.
- **No external TA integration**: The platform does not consume pre-computed technical analysis from any external service.

### Opportunity

TradingView MCP (Model Context Protocol) servers expose TradingView's market screener API, technical analysis signals, and screening capabilities as structured tool calls. Several open-source implementations exist:

| MCP Server | Tools | Focus | Auth |
|---|---|---|---|
| `atilaahmettaner/tradingview-mcp` (1.7k stars) | 30+ | TA indicators, backtesting (6 strategies), Yahoo Finance prices, Reddit sentiment, RSS news, candlestick patterns, multi-timeframe analysis | None |
| `fiale-plus/tradingview-mcp-server` (21 stars) | 12 | Screener (stocks/crypto/forex/ETF), 80+ fundamental fields, 14 preset screening strategies, TA buy/sell/neutral summaries, symbol search | None |
| `ertugrul59/tradingview-chart-mcp` (86 stars) | 2 | Chart image scraping via Selenium browser automation | TV session cookies |

Integrating TradingView MCP into the platform would:

1. **Add a third strategy authoring entry point**: users describe a TradingView-style approach ("screen crypto for RSI oversold with golden cross") and the system maps screening results and TA signals into a `StrategyConfig`
2. **Enrich `MarketContext`** with TradingView's multi-timeframe technical analysis summaries (buy/sell/neutral ratings, oscillator scores, moving average alignment)
3. **Ground the LLM strategy interpreter** with real-time market data when interpreting natural language strategy descriptions
4. **Enable TradingView-aligned backtesting** where users can screen for setups, build a strategy, and immediately backtest — all within TradePilot

### Important Constraint

**TradingView does not expose Pine Script strategies through any public API.** None of the MCP servers can extract or import Pine Script strategy logic from TradingView. Pine Script import is a separate concern covered in PRD-02 Phase 3 (PyneCore / parser approach). This PRD focuses on consuming TradingView's *screening and technical analysis data*, not its strategy scripting language.

### MCP Server Evaluation

**Recommended primary: `atilaahmettaner/tradingview-mcp`**

- Richest toolset (30+ tools) across TA, sentiment, and market data
- Built-in backtesting with 6 strategies (RSI, Bollinger, MACD, EMA cross, Supertrend, Donchian)
- Reddit sentiment and RSS news feeds — complements the existing LLM sentiment provider
- Multi-timeframe analysis (15m → Weekly) aligning with the platform's 4H/1H/15m pipeline
- No credentials required — uses TradingView's public scanner API
- Python-based, installable via `pip install tradingview-mcp-server`
- Most actively maintained (1.7k stars, regular releases)

**Recommended secondary (optional): `fiale-plus/tradingview-mcp-server`**

- Complementary screener with deeper fundamental fields (Piotroski F-Score, Altman Z-Score, Graham Number)
- 14 pre-built screening presets (value, growth, quality, momentum, GARP, deep value, etc.)
- Useful if the platform expands beyond crypto to equities/ETFs
- TypeScript/Node-based, installable via `npm install -g tradingview-mcp-server`
- No credentials required

**Not recommended: `ertugrul59/tradingview-chart-mcp`**

- Only provides chart screenshot scraping via Selenium — fragile browser automation
- Requires TradingView session cookies (credential management burden)
- The platform already integrates TradingView charting widgets in the Angular frontend
- Adds a ChromeDriver dependency for limited value

---

## 2. Goals & Objectives

### Business Goals

| ID | Goal | Success Metric |
|----|------|---------------|
| BG-1 | Add TradingView-sourced data as a third strategy authoring entry point alongside form and natural language | Users can describe a TradingView-style screening approach and receive a generated `StrategyConfig` |
| BG-2 | Enrich market context with externally sourced multi-timeframe technical analysis | `MarketContext` includes TradingView TA summary signals (buy/sell/neutral) per configured timeframe |
| BG-3 | Improve strategy authoring quality by grounding LLM interpretation with real market data | Strategy interpreter uses live screening/TA data when converting natural language to `StrategyIntentDto` |
| BG-4 | Enable a "screen → configure → backtest" workflow within the platform | Users can screen for setups, build a strategy from results, and run a backtest in a single session |

### User Goals

| ID | Goal | Description |
|----|------|-------------|
| UG-1 | Screen markets using TradingView criteria | User specifies screening filters (RSI oversold, golden cross, volume breakout, etc.) and sees matching symbols |
| UG-2 | Generate a strategy from a TradingView-style description | User describes a TradingView approach (e.g., "Bollinger Band squeeze breakout on BTC 1H") and receives a populated `StrategyConfig` with assumptions |
| UG-3 | See TradingView TA signals alongside platform indicators | Dashboard shows TradingView's multi-timeframe buy/sell ratings alongside the platform's own indicator values |
| UG-4 | Backtest a TV-sourced strategy | After generating a strategy from TradingView signals, user can immediately run a backtest |
| UG-5 | Use TV screening to identify assets for grid deployment | Users screen crypto markets for grid-suitable conditions and deploy a grid strategy to the best candidates |

### Non-Goals

| ID | Non-Goal | Rationale |
|----|----------|-----------|
| NG-1 | Import Pine Script strategies from TradingView | No public API exists for this. Pine Script import is covered in PRD-02 Phase 3 via parser/compiler approach |
| NG-2 | Replace internal indicator calculations with TradingView data | Platform indicators (EMA, RSI, ATR, etc.) remain the source of truth for strategy execution. TV signals are advisory context only |
| NG-3 | Allow TradingView signals to directly trigger trades | TV data feeds into `MarketContext` and strategy authoring. It never bypasses the `StrategyEngine` → `RiskEngine` → `PositionManager` execution path |
| NG-4 | Support TradingView chart embedding beyond current capability | The frontend already uses TradingView charting widgets; no additional chart integration is in scope |
| NG-5 | Real-time streaming of TradingView data | MCP tools are request/response. Integration is periodic polling or on-demand, not continuous streaming |
| NG-6 | Equity/ETF/Forex trading execution | Screening may return non-crypto results, but execution remains limited to Hyperliquid (crypto perpetuals) |
| NG-7 | Require TradingView user credentials | The recommended MCP servers use TradingView's public scanner API — no login required |

---

## 3. Open Questions

| # | Question | Status |
|---|----------|--------|
| Q1 | **Which MCP server should be the primary integration target?** Three options evaluated: `atilaahmettaner/tradingview-mcp` (30+ tools, Python), `fiale-plus/tradingview-mcp-server` (12 tools, TypeScript), `ertugrul59/tradingview-chart-mcp` (chart scraping, Python). | **Recommendation: `atilaahmettaner/tradingview-mcp` as primary.** Richest toolset, no auth required, most aligned with crypto focus and multi-timeframe analysis. `fiale-plus` as optional secondary for equity screening if needed. `ertugrul59` not recommended (fragile, limited value). **Awaiting confirmation.** |
| Q2 | **Should the MCP server run as a sidecar process or be called via HTTP?** MCP supports stdio (child process) and HTTP transport modes. The .NET backend would need an MCP client adapter. | Unanswered — architecture decision needed. Options: (a) stdio sidecar managed by the Worker process, (b) HTTP transport with the MCP server running independently, (c) bypass MCP protocol entirely and call TradingView's public scanner API directly from .NET (no Python dependency). |
| Q3 | **How should TradingView TA signals be integrated into `MarketContext`?** Options: (a) new `TradingViewContext` property on `MarketContext`, (b) feed into existing `LlmContext` alongside sentiment, (c) separate context provider registered with `MarketContextBuilder`. | Unanswered. |
| Q4 | **Should TV screening results directly suggest `StrategyConfig` parameters, or should they be passed to the LLM interpreter for conversion?** The interpreter already handles NL → `StrategyIntentDto`. TV data could enrich the LLM prompt rather than creating a parallel conversion path. | Unanswered. |
| Q5 | **What is the polling/refresh cadence for TV TA signals in the market context?** The candle-close-driven architecture means indicators update on candle close. Should TV signals follow the same cadence (e.g., refresh on 15m candle close) or be independent? | Unanswered. |
| Q6 | **Does TradingView's public scanner API have rate limits that affect multi-tenant usage?** The MCP servers include configurable rate limiting (default 10 RPM for `fiale-plus`). With N subscribers, the platform may need a shared cache layer. | Unanswered — requires testing. |
| Q7 | **Should the MCP server's built-in backtesting (6 strategies) be exposed to users, or should all backtesting flow through the platform's own `BacktestRunner`?** The TV backtester uses different strategies (RSI, Bollinger, MACD, EMA cross, Supertrend, Donchian) than the platform's `GridStrategy`. | Unanswered. |
| Q8 | **What is the phasing relative to PRD-02?** Should this be a Phase 2.5 (after NL, before Pine Script) or an independent parallel workstream? | Unanswered. |

---

*Sections 4–10 will be drafted after feedback on sections 1–3 is received and open questions are addressed.*
