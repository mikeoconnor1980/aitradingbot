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
| Q1 | **Which MCP server should be the primary integration target?** Three options evaluated: `atilaahmettaner/tradingview-mcp` (30+ tools, Python), `fiale-plus/tradingview-mcp-server` (12 tools, TypeScript), `ertugrul59/tradingview-chart-mcp` (chart scraping, Python). | **Decided: `atilaahmettaner/tradingview-mcp` as primary.** Richest toolset, no auth required, most aligned with crypto focus and multi-timeframe analysis. `fiale-plus` as optional secondary for equity screening if needed. `ertugrul59` not recommended (fragile, limited value). |
| Q2 | **Should the MCP server run as a sidecar process or be called via HTTP?** MCP supports stdio (child process) and HTTP transport modes. The .NET backend would need an MCP client adapter. | **Decided: Independent HTTP service.** The MCP server runs as an independent containerised process alongside the API (Docker Compose sidecar on VPS; Azure Container Apps sidecar in production). The .NET API calls it over HTTP via `ITradingViewClient` (contract in Application, implementation in Infrastructure). HTTP allows sharing across API replicas, independent scaling, and health checking. This is control-plane infrastructure — unrelated to the per-subscriber Worker process. |
| Q3 | **How should TradingView TA signals be integrated into `MarketContext`?** Options: (a) new `TradingViewContext` property on `MarketContext`, (b) feed into existing `LlmContext` alongside sentiment, (c) separate context provider registered with `MarketContextBuilder`. | **Decided: Option (c) — Separate context provider.** New `ITradingViewContextProvider` registered with `MarketContextBuilder`, producing a `TradingViewContext` model on `MarketContext` alongside `LlmContext`. Follows existing pluggable provider pattern. Either provider can fail/be disabled independently. |
| Q4 | **Should TV screening results directly suggest `StrategyConfig` parameters, or should they be passed to the LLM interpreter for conversion?** The interpreter already handles NL → `StrategyIntentDto`. TV data could enrich the LLM prompt rather than creating a parallel conversion path. | **Decided: Enrich the existing LLM interpreter.** TV screening results and TA signals are passed as enriched prompt context to the existing LLM strategy interpreter. The LLM generates a `StrategyIntentDto` grounded in real market data. Single conversion pipeline to maintain. No parallel TV → StrategyConfig path. |
| Q5 | **What is the polling/refresh cadence for TV TA signals in the market context?** The candle-close-driven architecture means indicators update on candle close. Should TV signals follow the same cadence (e.g., refresh on 15m candle close) or be independent? | **Decided: Candle-close timing.** TV TA signals refresh on candle close, aligned with the existing `CandleClock` scheduling. Maintains deterministic execution semantics — signals don't change mid-candle. |
| Q6 | **Does TradingView's public scanner API have rate limits that affect multi-tenant usage?** The MCP servers include configurable rate limiting (default 10 RPM for `fiale-plus`). With N subscribers, the platform may need a shared cache layer. | **Deferred.** Requires empirical testing during implementation. A shared cache layer with TTL will be included in the architecture to mitigate this risk. |
| Q7 | **Should the MCP server's built-in backtesting (6 strategies) be exposed to users, or should all backtesting flow through the platform's own `BacktestRunner`?** The TV backtester uses different strategies (RSI, Bollinger, MACD, EMA cross, Supertrend, Donchian) than the platform's `GridStrategy`. | **Decided: Platform backtester only.** All backtesting flows through the platform's own `BacktestRunner`. The TV MCP server's built-in backtesting is not exposed to users. This avoids confusion between two backtesting systems with different strategy sets, execution models, and metrics. |
| Q8 | **What is the phasing relative to PRD-02?** Should this be a Phase 2.5 (after NL, before Pine Script) or an independent parallel workstream? | **Decided: Independent PRD.** NL input (PRD-02 Phase 2) is already implemented. This PRD supersedes the Pine Script import path (PRD-02 Phase 3) as the preferred next strategy input feature. Stands as its own feature with independent phasing. |

---

## 4. Scope

### In Scope

#### Phase 1 — MCP Server Deployment & Core Client

| Area | Deliverable |
|------|-------------|
| **MCP server deployment** | `atilaahmettaner/tradingview-mcp` running as an independent HTTP service. Containerised via Docker for local dev (Docker Compose) and production (Azure Container Apps sidecar). Health check endpoint exposed. |
| **`ITradingViewClient`** | Application-layer contract in `TradingApp.Application/Abstractions/Services/`. Methods: `ScreenCryptoAsync`, `GetTechnicalAnalysisAsync`, `GetMultiTimeframeAnalysisAsync`, `GetMarketSentimentAsync`, `GetFinancialNewsAsync`, `GetMarketSnapshotAsync`. |
| **`TradingViewMcpClient`** | Infrastructure implementation in `TradingApp.Infrastructure/Services/`. HTTP client calling the MCP server's HTTP transport. Includes retry logic, timeout handling, response deserialization. |
| **Configuration** | `TradingViewOptions` in `appsettings.json` — MCP server base URL, timeout, cache TTL, enabled flag. |
| **Shared response cache** | In-memory cache with configurable TTL (default 300s, matching the MCP server's own cache). Prevents redundant calls when multiple users request the same screening or TA data within the TTL window. |
| **API endpoints** | `TradingViewController` exposing: `POST /api/tradingview/screen` (crypto screening), `GET /api/tradingview/ta/{symbol}` (TA summary), `GET /api/tradingview/snapshot` (market overview). Rate-limited. |
| **Basic Angular integration** | Screening results displayed in a new "Market Scanner" page. TA summary shown as buy/sell/neutral badges on the asset dashboard. |

#### Phase 2 — MarketContext Enrichment

| Area | Deliverable |
|------|-------------|
| **`TradingViewContext` model** | Domain model: `TaSummary` (buy/sell/neutral per timeframe), `OscillatorScore`, `MovingAverageScore`, `CandlestickPatterns`, `SentimentScore` (Reddit), `NewsHeadlines`. Sits alongside `LlmContext` on `MarketContext`. |
| **`ITradingViewContextProvider`** | Pluggable provider registered with `MarketContextBuilder`. Calls `ITradingViewClient` on candle close (aligned with `CandleClock`). Populates `TradingViewContext` on each evaluation cycle. |
| **Graceful degradation** | If the MCP server is unavailable, `TradingViewContext` is `null` on `MarketContext`. Strategy engine and risk engine continue operating on platform-calculated indicators and LLM context only. No hard dependency. |
| **Dashboard enhancement** | TradingView TA summary (buy/sell/neutral ratings per timeframe) displayed alongside platform indicator values on the strategy monitoring dashboard. |

#### Phase 3 — LLM-Grounded Strategy Authoring

| Area | Deliverable |
|------|-------------|
| **Enriched strategy interpretation** | When a user submits a natural language strategy description, the strategy interpreter first calls `ITradingViewClient` to retrieve relevant screening/TA data for the mentioned symbol(s). This data is injected into the LLM prompt as grounding context. |
| **TV-aware prompt engineering** | Extended `StrategyInterpreterPrompt` that includes TV TA signals, screening results, and current market conditions. The LLM can reference real data (e.g., "RSI is currently 28, confirming oversold condition — configuring grid entry near $X"). |
| **Screen → Strategy flow** | User flow: (1) screen crypto markets for a condition → (2) select a result → (3) describe a strategy for that asset → (4) LLM generates `StrategyIntentDto` grounded in TV data → (5) review and save → (6) backtest. |
| **Source metadata** | `StrategyConfig.Source` captures `EntryPoint: "TradingView"` with the screening criteria and TA snapshot that informed the strategy generation. Persisted in revision history. |

### Out of Scope

| Item | Rationale |
|------|-----------|
| Pine Script import | Superseded by this PRD as the preferred next strategy input feature. If required later, PRD-02 Phase 3 can be revisited |
| TV MCP built-in backtesting | All backtesting uses the platform's `BacktestRunner`. TV backtester not exposed |
| Chart screenshot scraping | `ertugrul59/tradingview-chart-mcp` not integrated. Frontend already has TradingView charting widgets |
| TradingView user authentication | No login/credentials required — public scanner API only |
| Equity/ETF/Forex trade execution | Screening may return non-crypto results, but execution stays on Hyperliquid |
| Real-time TV data streaming | MCP is request/response. TV signals refresh on candle close, not continuously |
| `fiale-plus/tradingview-mcp-server` | Optional secondary for equity screening. Deferred unless the platform expands beyond crypto |
| TV signals overriding platform indicators | TV data is advisory context only. Platform-calculated indicators remain the execution source of truth |

### Future Considerations

| Item | How This PRD Informs It |
|------|------------------------|
| **Multi-asset screening & execution** | TV screening already supports stocks, ETFs, forex. When the platform adds exchanges beyond Hyperliquid, the screening infrastructure is ready |
| **Additional MCP server integration** | `ITradingViewClient` abstraction allows swapping or combining MCP servers without changing the application layer |
| **TV sentiment replacing LLM sentiment** | Reddit sentiment and RSS news from the TV MCP could complement or eventually replace parts of the LLM sentiment provider. Architecture supports both running in parallel |
| **User-defined screening presets** | Users may want to save custom screening criteria as reusable presets. The screening API supports this but the UI for managing presets is not in this scope |
| **Alerting on TV signal changes** | Future feature: notify users when TV TA summary flips from sell to buy on a watched asset. Not in scope but enabled by the polling infrastructure |

---

## 5. Technical Considerations

### Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Angular Frontend                      │
│  ┌──────────────┐  ┌─────────────┐  ┌────────────────┐ │
│  │ Market Scanner│  │  Dashboard  │  │ Strategy Editor │ │
│  │    (new)      │  │ (TA badges) │  │ (NL + TV data) │ │
│  └──────┬───────┘  └──────┬──────┘  └───────┬────────┘ │
└─────────┼─────────────────┼─────────────────┼───────────┘
          │ HTTP            │ HTTP            │ HTTP
┌─────────┼─────────────────┼─────────────────┼───────────┐
│         ▼                 ▼                 ▼           │
│  ┌──────────────────────────────────────────────────┐   │
│  │               .NET API (Control Plane)            │   │
│  │  ┌─────────────────┐  ┌────────────────────────┐ │   │
│  │  │TradingView      │  │StrategyInterpreter     │ │   │
│  │  │Controller (new) │  │(enriched with TV data)  │ │   │
│  │  └────────┬────────┘  └───────────┬────────────┘ │   │
│  │           │                       │              │   │
│  │  ┌────────▼───────────────────────▼────────────┐ │   │
│  │  │    ITradingViewClient (Application layer)    │ │   │
│  │  └────────────────────┬────────────────────────┘ │   │
│  │                       │                          │   │
│  │  ┌────────────────────▼────────────────────────┐ │   │
│  │  │  TradingViewMcpClient (Infrastructure)       │ │   │
│  │  │  + In-memory cache (TTL-based)               │ │   │
│  │  └────────────────────┬────────────────────────┘ │   │
│  └───────────────────────┼──────────────────────────┘   │
│                          │ HTTP                         │
│  ┌───────────────────────▼──────────────────────────┐   │
│  │    TradingView MCP Server (Python, sidecar)       │   │
│  │    atilaahmettaner/tradingview-mcp                 │   │
│  │    - 30+ tools (TA, screening, sentiment, news)   │   │
│  │    - Internal cache (300s TTL)                     │   │
│  │    - Rate limiting (configurable RPM)              │   │
│  └───────────────────────┬──────────────────────────┘   │
│                          │ HTTP                         │
└──────────────────────────┼──────────────────────────────┘
                           ▼
              TradingView Public Scanner API
```

### Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `ITradingViewClient` | `TradingApp.Application/Abstractions/Services/` | Application-layer contract for TV data retrieval |
| `TradingViewMcpClient` | `TradingApp.Infrastructure/Services/` | HTTP client implementation calling the MCP server |
| `TradingViewOptions` | `TradingApp.Application/Abstractions/Configuration/` | Configuration: base URL, timeout, cache TTL, enabled flag |
| `TradingViewContext` | `TradingApp.Domain/Models/` | Domain model for TV TA signals on `MarketContext` |
| `ITradingViewContextProvider` | `TradingApp.Application/Abstractions/Services/` | Pluggable provider for `MarketContextBuilder` |
| `TradingViewContextProvider` | `TradingApp.Infrastructure/Services/` | Implementation — calls `ITradingViewClient` on candle close |
| `TradingViewController` | `TradingApp.Api/Controllers/` | REST endpoints for screening, TA, market snapshot |
| `StrategyInterpreterPrompt` | `TradingApp.AI/Prompts/` | Extended prompt including TV data as grounding context |

### Integration Points

| Integration | Direction | Protocol | Notes |
|-------------|-----------|----------|-------|
| .NET API → MCP Server | Outbound | HTTP | `TradingViewMcpClient` → MCP HTTP transport |
| MCP Server → TradingView | Outbound | HTTP | Public scanner API, no auth |
| `MarketContextBuilder` → `ITradingViewContextProvider` | Internal | Method call | Registered alongside `ILlmContextProvider` |
| `StrategyInterpreter` → `ITradingViewClient` | Internal | Method call | Fetches TV data to enrich LLM prompt |
| Angular → .NET API | Inbound | HTTP | New controller endpoints |

### Deployment

| Environment | MCP Server Deployment | Configuration |
|-------------|----------------------|---------------|
| Local dev | `pip install tradingview-mcp-server` + run manually, or Docker Compose service | `TradingView:BaseUrl = http://localhost:8000` |
| VPS (Docker Compose) | Docker container alongside API and Worker | Internal Docker network, no public exposure |
| Azure (Container Apps) | Sidecar container in the same Container App | Internal endpoint only |

### Resilience

| Concern | Approach |
|---------|----------|
| MCP server unavailable | `TradingViewContext` is `null` on `MarketContext`. Strategy engine operates on platform indicators only. Screening API returns 503 with retry guidance. |
| TradingView API rate limits | Two-layer caching: MCP server internal cache (300s) + .NET in-memory cache (configurable TTL). Shared across all users. |
| Slow MCP responses | Configurable HTTP timeout on `TradingViewMcpClient`. Candle-close evaluation does not block on TV data — timeout produces `null` context. |
| MCP server crash/restart | Docker restart policy (`unless-stopped`). Health check endpoint monitored. No state is lost — MCP server is stateless. |

---

## 6. Technical Considerations — Data Models

### TradingViewContext (on MarketContext)

```csharp
public class TradingViewContext
{
    public TaSummary? TechnicalAnalysis { get; set; }
    public SentimentSnapshot? Sentiment { get; set; }
    public List<NewsHeadline> NewsHeadlines { get; set; } = [];
    public DateTime? FetchedAtUtc { get; set; }
}

public class TaSummary
{
    public string Symbol { get; set; } = "";
    public Dictionary<string, TaTimeframeSignal> Timeframes { get; set; } = new();
}

public class TaTimeframeSignal
{
    public string Timeframe { get; set; } = "";           // "60", "240", "1D", "1W"
    public string Signal { get; set; } = "Neutral";       // "StrongBuy", "Buy", "Neutral", "Sell", "StrongSell"
    public decimal OscillatorScore { get; set; }
    public decimal MovingAverageScore { get; set; }
}

public class SentimentSnapshot
{
    public string Source { get; set; } = "Reddit";
    public decimal BullishScore { get; set; }
    public decimal BearishScore { get; set; }
    public int PostCount { get; set; }
    public DateTime ScrapedAtUtc { get; set; }
}

public class NewsHeadline
{
    public string Title { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTime PublishedAtUtc { get; set; }
}
```

### Screening Request/Response

```csharp
public class CryptoScreenRequest
{
    public List<ScreeningFilter> Filters { get; set; } = [];
    public string? SortBy { get; set; }
    public string SortOrder { get; set; } = "desc";
    public int Limit { get; set; } = 20;
}

public class ScreeningFilter
{
    public string Field { get; set; } = "";       // "RSI", "SMA50", "volume", etc.
    public string Operator { get; set; } = "";    // "greater", "less", "in_range", "crosses_above", etc.
    public object? Value { get; set; }
}

public class CryptoScreenResult
{
    public List<ScreenedAsset> Assets { get; set; } = [];
    public int TotalMatches { get; set; }
    public DateTime FetchedAtUtc { get; set; }
}

public class ScreenedAsset
{
    public string Symbol { get; set; } = "";
    public decimal Price { get; set; }
    public decimal Change { get; set; }
    public decimal Volume { get; set; }
    public decimal? Rsi { get; set; }
    public decimal? Atr { get; set; }
    public string? TaSignal { get; set; }
}
```

---

## 7. Use Cases

### Personas

| Persona | Description |
|---------|-------------|
| **Alex (Active Trader)** | Experienced crypto trader who uses TradingView for charting and screening. Wants to quickly translate TV insights into executable grid strategies without manual configuration. |
| **Sam (Strategy Explorer)** | Semi-technical user who experiments with different market conditions. Uses screening to find opportunities and backtests before committing capital. |
| **Jordan (Platform Admin)** | Manages infrastructure and monitors system health. Needs to configure, enable/disable, and troubleshoot the TV MCP integration. |

### User Stories

#### Feature: Market Screening (Phase 1)

| ID | User Story | Acceptance Criteria |
|----|-----------|-------------------|
| US-1 | As Alex, I want to screen crypto markets using TradingView screening criteria so I can find assets matching my trading conditions | Given I specify filters (e.g., RSI < 30, volume > average), When I submit the screen, Then I see a ranked list of matching crypto assets with price, change, volume, RSI, and TA signal |
| US-2 | As Alex, I want to see TradingView's multi-timeframe TA summary for a specific symbol so I can gauge overall technical sentiment | Given I select a symbol, When I view its TA summary, Then I see buy/sell/neutral ratings across 1H, 4H, 1D, and 1W timeframes with oscillator and MA scores |
| US-3 | As Alex, I want to see a market snapshot showing major crypto assets, indices, and sentiment indicators | Given I open the Market Scanner page, When I request a snapshot, Then I see current prices, changes, and sentiment indicators for key assets (BTC, ETH, major indices) |
| US-4 | As Jordan, I want to configure the TV MCP server connection (URL, timeout, cache TTL) and enable/disable it without redeployment | Given I update `TradingViewOptions` in configuration, When the API restarts, Then the TV integration uses the new settings. If disabled, all TV endpoints return 503. |

#### Feature: MarketContext Enrichment (Phase 2)

| ID | User Story | Acceptance Criteria |
|----|-----------|-------------------|
| US-5 | As Alex, I want TradingView TA signals to appear alongside platform indicators on my strategy dashboard so I have a unified view of market conditions | Given a strategy is monitoring BTC-PERP, When a candle closes, Then the dashboard shows both platform indicators (EMA, RSI, ATR) and TV TA ratings (buy/sell/neutral per timeframe) |
| US-6 | As Sam, I want the platform to continue operating normally if the TV MCP server is down | Given the MCP server is unavailable, When a candle closes, Then `TradingViewContext` is null, platform indicators still update, strategy execution continues unaffected, and the dashboard indicates TV data is unavailable |
| US-7 | As Alex, I want TV sentiment data (Reddit, news) to be visible on the dashboard alongside LLM sentiment | Given TV sentiment is enabled, When I view the dashboard, Then I see Reddit bullish/bearish scores and recent news headlines alongside the LLM-generated sentiment classification |

#### Feature: LLM-Grounded Strategy Authoring (Phase 3)

| ID | User Story | Acceptance Criteria |
|----|-----------|-------------------|
| US-8 | As Sam, I want to describe a TradingView-style strategy in natural language and get a generated StrategyConfig that's grounded in real market data | Given I type "set up a grid strategy for BTC when RSI is oversold on the 1H", When I submit, Then the LLM receives current TV TA data (RSI value, TA summary, current price) and generates a `StrategyIntentDto` with assumptions referencing actual market values |
| US-9 | As Alex, I want to screen for a condition, select a result, and generate a strategy for that asset in one flow | Given I screen crypto for "RSI < 30 AND golden cross", When I select a result (e.g., ETH-PERP), Then I can enter a natural language strategy description for that asset, and the LLM uses the screening data + TA signals as grounding context |
| US-10 | As Sam, I want the source of TV-grounded strategies to be recorded so I can see what market data informed the generation | Given I generate a strategy grounded in TV data, When I view the strategy revision history, Then the source metadata shows `EntryPoint: "TradingView"`, the screening criteria used, and a snapshot of the TA signals at generation time |

---

## 8. Open Questions (Remaining)

| # | Question | Status |
|---|----------|--------|
| Q6 | **TradingView public scanner API rate limits under multi-tenant load.** Shared cache mitigates this, but empirical testing is needed to determine safe RPM for N concurrent users. | Deferred — test during Phase 1 implementation. |
| Q9 | **Should the MCP server's Reddit sentiment be used as-is, or should it be processed through the platform's own LLM sentiment provider for normalisation?** TV sentiment returns a raw bullish/bearish score. The LLM provider returns structured `MarketSentiment` classifications. | Unanswered — consider during Phase 2 implementation. |
| Q10 | **Should screening results be persistable?** Users may want to save screening criteria as "watchlists" or "scan presets." This is a UX concern that may warrant a future feature. | Deferred — future consideration. |

---

*Sections 9 (Timeline & Milestones) is optional per the PRD template. Available to draft on request.*

---

## 10. Design & UX

### Design Language

All new screens follow the existing TradePilot dark theme design language:

- **Background**: Dark radial gradient (`#0d1b1d` → `#071114` → `#04090b`)
- **Accent**: Teal (`#79cfc3`) for interactive elements, active states, and highlights
- **Profit/Loss**: Green (`#3bc9a8`) / Pink (`#e07a8f`) for directional indicators
- **Typography**: Roboto, 14px base
- **Framework**: Angular Material (dark cyan theme)
- **Cards**: Glass-morphism style with `rgba(8, 20, 22, 0.92)` background and `rgba(121, 207, 195, 0.16)` borders

### TA Signal Badges

TradingView TA signals use a consistent badge component across all screens:

| Signal | Background | Text Colour |
|--------|-----------|-------------|
| Strong Buy | `rgba(59, 201, 168, 0.12)` | `#3bc9a8` |
| Buy | `rgba(59, 201, 168, 0.08)` | `#6ddec9` |
| Neutral | `rgba(255, 255, 255, 0.04)` | `#7f9d99` |
| Sell | `rgba(224, 122, 143, 0.08)` | `#d4899a` |
| Strong Sell | `rgba(224, 122, 143, 0.12)` | `#e07a8f` |

### New Navigation Item

A **Market Scanner** entry is added to the sidebar navigation between "Market Data" and "Strategies":

```
Dashboard
Market Data
Market Scanner  ← new (icon: radar)
Strategies
Backtesting
...
```

### Wireframes

Three wireframes have been created matching the TradePilot design language. Open in any browser to view.

#### Screen 1: Market Scanner (Phase 1)

**File:** [mockup_market_scanner.html](../1-discover/wireframes/mockup_market_scanner.html)

**Key elements:**
- **Market snapshot cards** — BTC, ETH, Reddit sentiment, MCP server status across the top
- **Screening filter panel** — Field/operator/value dropdowns with "Add Filter" and "Screen" buttons. Active filters shown as removable chips below
- **Results table** — Ranked list of matching assets with price, 24h change, volume, RSI, and TA signal badges per timeframe (1H, 4H, 1D). Each row has a "Build Strategy →" action link
- **Data freshness** — "fetched 12s ago" indicator on results header

#### Screen 2: Dashboard TA Enrichment (Phase 2)

**File:** [mockup_dashboard_ta_enrichment.html](../1-discover/wireframes/mockup_dashboard_ta_enrichment.html)

**Key elements:**
- **Two-column layout** — Platform indicators (left) alongside TradingView context (right)
- **Left column**: Existing candle-derived indicators (EMA, RSI, ATR, trend, bias) and LLM context (sentiment, regime, event risk, confidence). Each section tagged with source ("Candle-derived", "AI-generated")
- **Right column (new)**:
  - **TV TA Summary** — 4-column grid showing buy/sell/neutral badges per timeframe (1H, 4H, 1D, 1W) with oscillator and MA sub-scores. Tagged with green status dot + "TV MCP"
  - **TV Sentiment** — Bullish/bearish bar chart with Reddit score, post count, freshness
  - **Financial News** — RSS headlines from Reuters, CoinDesk, CoinTelegraph with timestamps
- **Graceful degradation** — Status dot on TV source tags turns red/grey if MCP server is unavailable

#### Screen 3: Screen → Strategy Flow (Phase 3)

**File:** [mockup_screen_to_strategy_flow.html](../1-discover/wireframes/mockup_screen_to_strategy_flow.html)

**Key elements:**
- **Stepper** — 5-step flow: Screen → Select Asset → Describe Strategy → Review & Save → Backtest. Steps show complete (green ✓), active (teal), and pending (grey) states
- **Selected asset banner** — Shows the asset selected from screening results with price, change, TA badges, and the screening criteria that matched
- **Left panel**:
  - **NL input card** — Textarea for natural language strategy description (reuses existing `nl-input-card` pattern). Button labelled "Generate (TV-Grounded)" to indicate TV enrichment
  - **Grounding context panel** — Shows the TV data that will be injected into the LLM prompt: current price, RSI, TA signals per timeframe, ATR, Reddit sentiment. Blue-tinted card to distinguish from user input
- **Right panel**:
  - **Generated strategy card** — Confidence badge (high/medium colour-coded), assumptions list with "Edit →" links to relevant form fields, config summary table (type, direction, levels, spacing, anchor, TP, hedge), and action buttons (Save, Backtest, Edit in Builder)
  - **Source metadata** — Records `EntryPoint: "TradingView"`, screening criteria, and TV TA snapshot at generation time

### UX Principles

| Principle | Application |
|-----------|------------|
| **Transparency** | TV data sources are always labelled (source tags, status dots). Users know which data comes from the platform vs. TradingView |
| **Graceful degradation** | If TV MCP is unavailable, the dashboard shows grey status dots and "TV data unavailable" message. No features break |
| **Progressive disclosure** | Screening starts simple (field/operator/value). Advanced users can chain multiple filters. Grounding context is visible but collapsed by default |
| **Familiar patterns** | NL input card reuses the existing `nl-input-card` component pattern. Strategy generation flow matches the existing NL → review → save workflow |
| **Data provenance** | Every TV-grounded strategy records source metadata. Users can trace what market data informed the generation |
