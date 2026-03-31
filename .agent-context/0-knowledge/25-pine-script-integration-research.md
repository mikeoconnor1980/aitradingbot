# Pine Script Indicator Integration — Research

> **Date**: 2026-03-31  
> **Status**: Research complete — Option F (extraction + indicator library) recommended, PBI creation pending  
> **Epic**: Pine Script Indicator Integration

## Overview

Enable users to paste TradingView Pine Script indicator source, compute it against candle data, render results on the market data chart, and map alert conditions to trading signals that execute through the existing strategy pipeline.

## Ecosystem Research

### Approach Options Evaluated

| Approach | Complexity | Fidelity | Verdict |
|----------|-----------|----------|---------|
| **A. Full Pine Script interpreter** (build from scratch) | Very High | High | Months of effort, not viable for POC |
| **B. Transpile Pine → C#** via AST parsing | High | Medium-High | No ANTLR grammar exists; would need custom parser |
| **C. Transpile Pine → JS** and run in Jint or browser | High | Medium-High | JS ecosystem has actual Pine parsers |
| **D. Supported subset via PineTS** (recommended) | Medium | Medium-High | Best cost/value — mature OSS library exists |
| **E. TradingView Charting Library** (embed widget) | Low-Medium | Perfect | Requires TradingView license ($$$), replaces lightweight-charts |
| **F. Extraction + indicator library** (recommended) | Low-Medium | Medium | Zero license risk, ~80% coverage, fastest delivery |

**Selected approach: F — Extraction + indicator library (with Option D as future upgrade path)**

### Key Finding: No ANTLR Grammar Exists

The ANTLR grammars-v4 repository (~400+ languages) has no Pine Script grammar. The language listing goes from `pike` to `pl0` with nothing for Pine. Building a parser from scratch would be a significant effort.

### Key Finding: No NuGet Packages Exist

NuGet search returned zero relevant Pine Script parsing results. `TeknikAnaliz.NET` is a C# TA calculation library ported from Pine Script formulas, but it is not a parser/runtime.

---

## Recommended Libraries

### 1. PineTS (Primary — Parser & Runtime)

| Property | Value |
|----------|-------|
| **Repository** | [github.com/QuantForgeOrg/PineTS](https://github.com/QuantForgeOrg/PineTS) |
| **Package** | `npm install pinets` (v0.9.8) |
| **Language** | TypeScript (98.3%) |
| **Stars** | 293 stars, 57 forks, 44 releases |
| **Last commit** | Active (within days of research date) |
| **License** | **AGPL-3.0** (free for internal use) / Commercial license available |
| **Pine Script versions** | v5, v6 (experimental) |

**Capabilities:**
- Full transpiler + runtime — parses native Pine Script, transpiles to JS, executes against candle data
- 60+ `ta.*` functions: `ta.sma`, `ta.ema`, `ta.rsi`, `ta.macd`, `ta.atr`, `ta.bbands`, `ta.stoch`, `ta.crossover`, `ta.crossunder`, and more
- Custom data source support (pass OHLCV arrays directly)
- `plot()` / `plotchar()` output extraction
- `request.security()` multi-timeframe support
- Real-time streaming mode
- `input()` parameter extraction
- Series/lookback variable semantics (`close[1]`, `close[2]`)
- `var` keyword for persistent variables across bars
- Tuple destructuring (`[a, b, c] = ta.macd(...)`)
- Array, matrix, and map types

**Gaps:**
- Strategy backtesting engine is "in progress" (not needed — we have our own)
- `alertcondition()` extraction coverage unclear (mitigated by `plotshape()` boolean series + crossover detection)
- Pre-1.0 version (mitigated by active maintenance and 44 releases)

**Output format:** Named plot series with `{ time, value }` arrays — maps directly to `lightweight-charts` `addSeries().setData()`.

### 2. lightweight-charts-indicators (Companion — Chart Rendering)

| Property | Value |
|----------|-------|
| **Repository** | [github.com/deepentropy/lightweight-charts-indicators](https://github.com/deepentropy/lightweight-charts-indicators) |
| **Package** | `npm install lightweight-charts-indicators` (v0.4.0) |
| **License** | **MIT** |
| **Last publish** | Active (within weeks of research date) |

**Capabilities:**
- 446 pre-built indicators (82 standard, 317 community, 44 candlestick patterns)
- Direct `lightweight-charts` integration (our app already uses this library)
- Output format: `{ metadata: { overlay: boolean }, plots: { plot0: [{time, value}] } }` — exact format needed
- `indicatorRegistry` enables dynamic UI for selecting/toggling indicators
- Depends on `oakscriptjs` (Pine Script v6-compatible TA function library)

### 3. OakScriptJS (Lower-level TA Library)

| Property | Value |
|----------|-------|
| **Package** | `npm install oakscriptjs` (v0.2.7) |
| **License** | **MIT** |

PineScript v6-compatible `ta.*` function library in TypeScript with `Series` type for time-series data. Used internally by `lightweight-charts-indicators`. Useful if we need to implement individual indicators without full Pine parsing.

---

## Architecture Decision

### Execution Location

| Option | Pros | Cons |
|--------|------|------|
| **Browser-side (Angular)** | Avoids AGPL network-use clause; zero backend changes for indicator rendering | Cannot generate server-side signals for automated trading |
| **Node.js sidecar** | Server-side signal generation; integrates with .NET API via HTTP/gRPC | Additional deployment component; AGPL implications for SaaS |
| **Hybrid** (recommended) | Browser renders chart overlays; server evaluates for signal generation | Two execution contexts to maintain |

**Recommended: Hybrid approach**
- **Frontend**: PineTS runs in browser for interactive chart indicator rendering. `lightweight-charts-indicators` handles the rendering layer. No license concerns (browser execution).
- **Backend**: Small Node.js sidecar service executes Pine Script for signal generation on candle close events. Called by the .NET API's scheduling pipeline. Results map to `TradingSignal` types for the existing execution chain.

### Integration with Existing Pipeline

```
CandleClock (candle close)
  → StrategyScheduler (fan-out per user)
    → MarketContextBuilder (fetches candles, computes built-in indicators)
      → [NEW] PineScriptEvaluator (calls Node.js sidecar, returns indicator values + alert events)
        → MarketContext (now includes CustomIndicatorResults)
          → StrategyEngine / CustomIndicatorStrategy
            → TradingSignal (DeployGrid, TakeProfit, IndicatorAlert, etc.)
              → RiskEngine → ExecutionEngine
```

### Signal Mapping

Pine Script alert-producing constructs:
- `alertcondition(condition, title, message)` → map to named signal events
- `plotshape(condition, ...)` → boolean series, detect true transitions
- `ta.crossover(a, b)` / `ta.crossunder(a, b)` → crossover events

These map to a new `IndicatorAlert` signal type or directly to existing signal types (DeployGrid, TakeProfit) via user-configurable mapping.

---

## License Analysis

| Library | License | SaaS Impact |
|---------|---------|-------------|
| PineTS | AGPL-3.0 | If executed server-side in a SaaS product, AGPL requires source disclosure. **Mitigations**: (1) client-side only execution, (2) purchase commercial license, (3) keep sidecar isolated |
| lightweight-charts-indicators | MIT | No concerns |
| oakscriptjs | MIT | No concerns |
| lightweight-charts (existing) | Apache-2.0 | No concerns |

**Recommendation**: Start with browser-only execution (no AGPL concern). Add server-side sidecar in a later phase, evaluating commercial license if needed for SaaS distribution.

---

## Risk Assessment

| Risk | Severity | Mitigation |
|------|----------|------------|
| AGPL license for SaaS distribution | Medium | Browser-only execution initially; commercial license available |
| PineTS is pre-1.0 (v0.9.8) | Medium | Actively maintained, 44 releases, 60+ functions, passing tests |
| No `alertcondition()` extraction | Low-Medium | Extract `plotshape()` boolean series + detect crossover conditions from plot data |
| JS-only runtime (no C# equivalent) | Low | Node.js sidecar or browser-side execution — both architecturally sound |
| Pine Script incompleteness | Low | Most popular TradingView indicators use basic `ta.*` functions which are well-supported |
| PineTS API breaking changes | Low | Pin version, wrap in adapter interface |

---

## Option F Revisited: Indicator Library + Pine Script Extraction

### Why Option F is now viable

With the discovery of `oakscriptjs` (MIT) and `lightweight-charts-indicators` (MIT), Option F transforms from "manually select from built-in indicators" to "automatically extract indicator configuration from pasted Pine Script and execute natively."

### How it works

1. User pastes Pine Script
2. A **lightweight pattern extractor** (regex + simple parse, not a full transpiler) identifies:
   - `ta.*` function calls + parameters (e.g., `ta.ema(close, 21)`)
   - `input()` declarations (user-configurable parameters)
   - `plot()` calls (what to draw, colors, line styles)
   - `alertcondition()` / `plotshape()` (signal triggers)
   - Crossover/threshold conditions (`ta.crossover(fast, slow)`, `rsi > 70`)
3. Extracted functions map to **oakscriptjs** (frontend, MIT) or **C# TA library** (backend)
4. Chart renders via **lightweight-charts-indicators** (MIT, 446 pre-built indicators, native `lightweight-charts` format)
5. Alert conditions map to signal triggers in the existing strategy pipeline

### What most indicators actually look like

The vast majority of popular TradingView indicators follow this pattern:

```pine
//@version=5
indicator("My Setup", overlay=true)
ema21 = ta.ema(close, 21)
ema50 = ta.ema(close, 50)
rsi = ta.rsi(close, 14)

plot(ema21, color=color.blue)
plot(ema50, color=color.red)

alertcondition(ta.crossover(ema21, ema50) and rsi < 30, "Buy Signal")
```

This decomposes entirely into: **2 EMA indicators + 1 RSI + crossover condition + threshold**. No Pine execution needed — just parameter extraction and indicator library calls.

### What wouldn't work under Option F

Scripts with custom math, loops, array manipulation, or novel formulas:

```pine
// Requires actual Pine execution — Option F cannot handle this
myCustom = 0.0
for i = 0 to 9
    myCustom += close[i] * math.pow(0.9, i)
```

Estimated coverage: **~70-80% of popular TradingView indicators** use standard `ta.*` functions that are fully decomposable.

### Option D vs Option F Comparison

| Concern | Option D (PineTS full runtime) | Option F (extraction + indicator library) |
|---------|-------------------------------|------------------------------------------|
| **License** | AGPL-3.0 (SaaS risk) | MIT only (zero risk) |
| **Complexity** | Full transpiler + runtime integration | Pattern extraction + indicator library lookup |
| **Security** | Executes arbitrary user code in browser/sidecar | No code execution — parameter extraction only |
| **Chart rendering** | Build custom series output from PineTS | `lightweight-charts-indicators` ready-made |
| **Backend indicators** | Need Node.js sidecar for server-side | C# TA library stays in-process (.NET native) |
| **Coverage** | ~95% of Pine scripts | ~70-80% of Pine scripts (standard patterns) |
| **Failure mode** | Obscure runtime errors on unsupported features | Clear "unsupported construct" validation message |
| **Delivery effort** | ~7 PBIs, medium-high complexity | ~5-6 PBIs, medium complexity (~40% less work) |

### Hybrid Path (Recommended)

Start with **Option F** for fast, safe delivery. Add **PineTS execution as a fallback** later for scripts that fail extraction:

1. **Try extraction first** → if successful, use built-in indicator library (fast, safe, MIT)
2. **If extraction fails** → show "This script uses advanced features" with clear list of unsupported constructs
3. **Future phase** → offer PineTS execution for complex scripts (AGPL/commercial license sorted by then)

This gives immediate value with zero license risk, and a clean upgrade path.

### PBI Impact Under Option F

| PBI | Option D scope | Option F scope |
|-----|---------------|----------------|
| **PBI 1: Parser** | Full AST transpiler via PineTS | Lightweight pattern extractor (regex + simple parse) |
| **PBI 2: Runtime** | PineTS integration + Node.js sidecar | **Eliminated** — use oakscriptjs (frontend) + C# TA library (backend) |
| **PBI 3: CRUD** | Same | Same |
| **PBI 4: Pipeline** | PineTS sidecar integration | Simpler — run standard indicator functions in-process |
| **PBI 5: Chart** | Build series output from PineTS | Use `lightweight-charts-indicators` directly |
| **PBI 6: Alerts** | Extract from PineTS execution output | Extract from parsed conditions (same complexity) |
| **PBI 7: Strategy** | Same | Same |

**Net effect**: PBI 1 simpler, PBI 2 eliminated, PBI 5 mostly off-the-shelf. ~40% less work, zero license risk, at the cost of ~20-30% coverage gap for exotic scripts.

---

## Proposed PBI Breakdown

### Option D approach (full PineTS runtime)

1. **Pine Script Parser & AST** — Parse Pine Script v5/v6 via PineTS, validate, extract metadata (inputs, plots, alerts)
2. **Pine Script Runtime Engine** — Execute parsed script against candle series, return named plot series and alert events
3. **Custom Indicator CRUD** — Domain entity, API endpoints, UI for paste/validate/save (tenant-scoped)
4. **Indicator Computation Pipeline** — Integrate Pine runtime into MarketContextBuilder, extend IndicatorSnapshot
5. **Chart Indicator Overlay Rendering** — Render custom indicator series on price chart via lightweight-charts-indicators
6. **Alert Condition → Signal Mapping** — Map Pine alert constructs to TradingSignal events with user-configurable mapping
7. **Custom Indicator Strategy Plugin** — New IStrategyEngine implementation that evaluates Pine alert conditions

### Option F approach (extraction + indicator library) — Recommended

1. **Pine Script Pattern Extractor** — Lightweight parser extracts `ta.*` calls, `input()` params, `plot()` config, `alertcondition()`/`plotshape()` triggers. Returns structured indicator config. Validates and reports unsupported constructs clearly.
2. **Custom Indicator CRUD** — Domain entity `CustomIndicator` (UserId, Name, PineSource, ExtractedConfig JSON, IsValid, ValidationErrors). API endpoints. Angular UI: paste, validate, save. Tenant-scoped.
3. **Indicator Computation Pipeline** — Run extracted indicator configs through oakscriptjs (frontend) and C# TA library (backend MarketContextBuilder). Extend IndicatorSnapshot with custom indicator results.
4. **Chart Indicator Overlay Rendering** — Integrate `lightweight-charts-indicators` into PriceChartComponent. Overlay (on price) and separate-pane (RSI, MACD) indicators. Toggle indicators on/off.
5. **Alert Condition → Signal Mapping** — Map extracted `alertcondition()`/crossover conditions to TradingSignal events. User-configurable mapping UI (which alert → which signal type).
6. **Custom Indicator Strategy Plugin** — New IStrategyEngine implementation evaluating indicator alert conditions. Reuses GridController/RiskEngine/ExecutionEngine downstream.

See conversation history for detailed PBI descriptions.
