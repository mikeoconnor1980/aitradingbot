# LLM Context And Sentiment Architecture

The platform uses three separate LLM integrations, each with a different interface, configuration section, and runtime responsibility. This document covers the market-context path, where AI augments regime classification and narrative context but never places trades or bypasses the risk pipeline.

## LLM Integration Modes

| Capability | Interface | Primary implementation | Config section | Purpose |
|------------|-----------|------------------------|----------------|---------|
| Strategy interpretation | `ILlmClient` | `OpenAiCompatibleLlmClient` | `Llm` | Convert natural language into `StrategyConfig` |
| Strategy review | `IReviewLlmClient` | `ReviewLlmClient` | `LlmReview` | Critique saved strategy revisions |
| Market context | `ILlmContextClient` | `LlmContextClient` | `LlmContext` | Produce qualitative market context and regime guidance |

All three use the OpenAI-compatible HTTP protocol implemented in `src/TradingApp.AI`, but they are configured and registered independently so one feature can be enabled without forcing the others to share a model or runtime profile.

The important runtime rule is that the live trading path does not depend on an external LLM being available. `SyntheticRegimeProvider` remains the always-on classifier, and the market-context LLM is an optional overlay.

## Purpose

The market-context pipeline enriches `MarketContext` with:

- market sentiment
- macro regime narrative
- event-risk classification
- derived trading regime
- human-readable summary text

The trading system still enters and exits only through `IStrategyEngine`, `IGridController`, `ISignalController`, `IRiskEngine`, and `IPositionManager`.

## Context Model

`LlmContext` lives in `src/TradingApp.Application/Trading/Models/LlmContext.cs`.

| Field | Type | Notes |
|------|------|-------|
| `MarketSentiment` | `string` | Qualitative market tone such as Bullish, Bearish, or Neutral |
| `MacroRegime` | `string` | Narrative regime string. Current prompt constrains this to `Bullish`, `Bearish`, or `Neutral` |
| `EventRisk` | `string` | Qualitative event-risk label such as Low, Medium, or High |
| `Confidence` | `decimal` | Model confidence score |
| `DerivedRegime` | `MarketRegime` | Primary regime used by strategy gating: `Aggressive`, `Normal`, `Defensive`, `RiskOff` |
| `Summary` | `string` | Free-text explanation |
| `GeneratedAtUtc` | `long` | Unix milliseconds, not `DateTime` |

`DerivedRegime` is the important field for execution. `GridStrategyEngine` uses `context.LlmContext?.DerivedRegime ?? MarketRegime.Normal` when deciding whether setups are tradable.

## Runtime Flow

```
Indicators -> SyntheticRegimeProvider -> optional ILlmContextProvider -> MarketContext
          -> StrategyEngine -> GridController / SignalController -> RiskEngine
```

In practice the flow works like this:

1. `LiveMarketContextBuilder` and `BacktestMarketContextBuilder` compute indicator state.
2. `SyntheticRegimeProvider` always evaluates a baseline `LlmContext`.
3. If an `ILlmContextProvider` is available, live mode can ask it for richer context and optional macro-event interpretation.
4. If the LLM call fails or no provider is registered, the synthetic result remains authoritative.
5. The final `LlmContext` is attached to `MarketContext` and consumed by the strategy pipeline.

## SyntheticRegimeProvider Is The Primary Classifier

`SyntheticRegimeProvider` in `src/TradingApp.Application/Trading/Services/SyntheticRegimeProvider.cs` is the always-available regime classifier.

It is rule-based and derives regime from the indicator snapshot, including:

- EMA stack alignment
- ATR percentile and volatility state
- RSI context

This means the system does not depend on an external LLM to classify markets. The synthetic provider is the default runtime path in both backtest and live execution, with the LLM acting as an optional overlay.

## LLM Context Client Architecture

| Client | Location | Request characteristics |
|--------|----------|-------------------------|
| `OpenAiCompatibleLlmClient` | `src/TradingApp.AI/Services/OpenAiCompatibleLlmClient.cs` | Strategy interpretation, OpenAI-compatible chat completions |
| `ReviewLlmClient` | `src/TradingApp.AI/Services/ReviewLlmClient.cs` | Uses `LlmReviewOptions`, `Temperature = 0.4`, text response |
| `LlmContextClient` | `src/TradingApp.AI/Services/LlmContextClient.cs` | Uses `LlmContextOptions`, `Temperature = 0.2`, JSON response |

The context-specific provider stack is:

| Component | Purpose |
|-----------|---------|
| `ILlmContextClient` / `LlmContextClient` | Raw OpenAI-compatible market-context HTTP client |
| `ILlmContextProvider` / `LlmContextProvider` | Builds prompts, sends requests, parses JSON, normalizes output |
| `MarketContextPrompt` | Constrains the expected JSON schema and allowed values |
| `MacroEventListItemDto` | Optional upcoming macro-event context passed into the prompt |

`LlmContextProvider.GetContextAsync` accepts optional `IReadOnlyCollection<MacroEventListItemDto>` so upcoming calendar events can be embedded in the context request.

## Data Sources: Implemented Vs Aspirational

The current implementation primarily sends indicator-derived inputs and optional macro-calendar events.

### Implemented Inputs

- `EmaFast`
- `EmaSlow`
- `EmaTrend`
- `Rsi`
- `Atr`
- optional macro-calendar events from the macro calendar services

### Not Yet Wired

- crypto news feeds
- social sentiment feeds
- curated commentary pipelines
- external multi-source market intelligence aggregation

Those remain future work and should not be treated as live dependencies of the current system.

## Registration Patterns

There are two registration patterns in the codebase today.

### API Host

`TradingApp.Api` calls `builder.Services.AddAI(builder.Configuration)`, which wires all three AI clients and registers `ILlmContextProvider` through the shared AI extension.

### Worker Host

`TradingApp.Worker` uses conditional registration for the market-context provider. It only wires `ILlmContextClient` and `ILlmContextProvider` when the `LlmContext:ApiKey` setting is present. Otherwise:

- no LLM context provider is registered
- `LiveMarketContextBuilder` receives `null` for `ILlmContextProvider`
- `SyntheticRegimeProvider` remains the active classifier

That is the important runtime fallback behavior for live execution. The API host can still register the broader AI stack for control-plane features, but the Worker is where conditional registration matters for trading safety.

## Persistence And Audit

Live market-context snapshots are persisted through `LlmContextSnapshot` with the current fields:

- `Symbol`
- `MarketSentiment`
- `MacroRegime`
- `EventRisk`
- `Confidence`
- `Summary`
- `DerivedRegime`
- `GeneratedAtUtc`

These snapshots support API queries, historical review, and operator visibility without giving the model any direct execution authority.

## Safety Rules

The market-context LLM must not:

- place trades
- emit exchange actions
- bypass `IRiskEngine`
- override `SyntheticRegimeProvider` availability guarantees

Its role is advisory context that influences regime-aware strategy behavior.

## Related Knowledge

- [01-trading-strategy.md](01-trading-strategy.md)
- [14-strategy-runtime-model.md](14-strategy-runtime-model.md)
- [24-strategy-interpreter-architecture.md](24-strategy-interpreter-architecture.md)
- [28-macro-calendar.md](28-macro-calendar.md)

## Future Recommendations

- Add real crypto-news and social-sentiment inputs before describing the system as multi-source sentiment analysis.
- Add multi-provider aggregation and confidence blending so the LLM context path is not tied to a single model response.
- Track sentiment trend deltas over time instead of only storing point-in-time snapshots.
- Add richer prompt inputs for volatility regimes, funding trends, and market breadth once those datasets are persisted.