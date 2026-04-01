# PRD: Strategy Authoring, Parsing & Compilation Pipeline

**Status:** Draft  
**Priority:** TBD  
**Date:** 2026-04-01  
**Author:** PRD Writer Agent  

---

## 1. Background & Context

### Problem Statement

The platform currently supports a single strategy input method: users manually configure strategy parameters through structured JSON configuration stored in `StrategyConfig`. While effective for technically proficient users, this approach:

- Requires users to understand the JSON schema (trend, bias, entry, grid, exit, hedge, risk sections)
- Limits accessibility to users who can map their trading ideas to specific parameter values
- Offers no path for users who think in natural language ("buy dips with 10 levels, 0.5% spacing") or who have existing Pine Script strategies they want to port
- Provides no compilation step between user-authored config and runtime execution — the engine consumes raw JSON directly

### Current State

- **Strategy definition**: `ITradingStrategy` plugin interface with `GridStrategy` as the sole implementation
- **Configuration**: JSON-based `StrategyConfig` stored per user, validated at API save time against the schema defined in [13-strategy-config-schema.md](../../0-knowledge/13-strategy-config-schema.md)
- **Execution pipeline**: `StrategyEngine` → `GridController` → `RiskEngine` → `PositionManager` → `ExecutionEngine` — all deterministic, candle-close-driven
- **LLM role**: Currently scoped as a context/sentiment provider only (see [17-llm-context-sentiment-architecture.md](../../0-knowledge/17-llm-context-sentiment-architecture.md)); never places trades or generates strategy definitions
- **No intermediate representation**: There is no AST layer between user input and the persisted config — validation is flat schema checks only

### Opportunity

Introducing a structured authoring and compilation pipeline with multiple entry points (UI selectors, natural language, Pine Script import) that all converge through an internal AST into a validated, versioned canonical JSON model would:

- Lower the barrier to entry for non-technical users
- Enable users to rapidly prototype strategies from natural language descriptions
- Allow migration of existing Pine Script strategies from TradingView
- Introduce a formal compilation step (canonical JSON → typed runtime objects) that enables richer validation, explainability, and diffing
- Maintain the deterministic execution guarantee — the engine always receives the same compiled plan regardless of how the strategy was authored

### Reference Architecture

The proposed architecture is described in [trading_strategy_e2e_architecture.md](../1-discover/prd/trading_strategy_e2e_architecture.md) and detailed in [trading_strategy_architecture_requirements.md](../1-discover/prd/trading_strategy_architecture_requirements.md):

```
UI Selectors / Natural Language / Pine Script Import
        ↓
     Input Adapters (per entry point)
        ↓
     StrategyIntentDto (for NL) / direct mapping (for UI/Pine)
        ↓
     Internal AST (source-agnostic)
        ↓
     Validation + Normalisation (multi-level)
        ↓
     Canonical JSON (persisted, versioned)
        ↓
     Typed Runtime Compilation
        ↓
     Backtest / Paper / Live Trading Engine
```

### Pine Script Import — Candidate Technology

[PyneCore](https://github.com/PyneSys/pynecore) (Apache 2.0, v6.4.0, actively maintained) is identified as a candidate for the Pine Script import path:

- **What it does**: Python framework that brings Pine Script semantics to Python via AST transformations at import time. Includes a comprehensive TA indicator library, Series/Persistent variable system, and bar-by-bar execution model.
- **Pine Script compilation**: `pyne compile my_indicator.pine` converts Pine Script to Python. **Requires a paid PyneSys API key** (pynesys.io) — the core runtime is open source but the compiler is a hosted service.
- **Proposed integration**: Pine Script → PyneSys API compiles to PyneCore Python → Python sidecar extracts strategy parameters (levels, spacing, TP/SL, etc.) → returns structured JSON to .NET backend → normal AST → canonical JSON pipeline continues.
- **Vendor dependency**: The Pine Script compilation step relies on an external paid API. This is a Phase 3 concern and should be evaluated for cost, reliability, and fallback options.

### Key Design Decisions (from requirements document)

- **AST is internal** — a compilation artefact, never user-facing or persisted
- **Canonical JSON is the persisted source of truth** — versioned, schema-versioned, suitable for typed deserialisation
- **LLM output is never executed directly** — the LLM returns a `StrategyIntentDto`; deterministic .NET code converts it to AST → canonical JSON
- **Trading engine consumes compiled runtime objects only** — not raw JSON
- **YAML may be used for export/import** — but JSON is the internal contract
- **Discriminated union for strategy types** — `entry.mode` determines which fields are valid (e.g., `GridEntryPlanNode` vs. `DcaPlanNode`). Each strategy type has its own entry/exit sub-shape within the AST and canonical JSON. Indicators remain in named context sections (`trend`, `bias`) managed by `MarketContextBuilder`, not as user-composable expression conditions. Composable indicator expressions may be added in a future phase when indicator-based strategies are delivered.

---

## 2. Goals & Objectives

### Business Goals

| ID | Goal | Success Metric |
|----|------|---------------|
| BG-1 | Expand addressable user base beyond technically proficient traders | ≥ 50% of new strategies created via UI selectors or natural language within 3 months of launch |
| BG-2 | Reduce strategy creation time | Average time from "new strategy" to "first backtest" drops by ≥ 40% compared to manual JSON editing |
| BG-3 | Enable Pine Script migration as a user acquisition channel | ≥ 1 supported Pine Script construct set (grid-compatible subset) importable end-to-end |
| BG-4 | Make strategies explainable, versioned, and auditable | Every strategy revision is persisted with source metadata; users can diff between versions |

### User Goals

| ID | Goal | Description |
|----|------|-------------|
| UG-1 | Create a strategy without writing JSON | User selects parameters via UI dropdowns, toggles, and numeric inputs that map directly to the strategy config schema |
| UG-2 | Describe a strategy in plain English | User types a natural language description; the system extracts a `StrategyIntentDto`, shows what was understood (including assumptions and confidence score), and lets the user confirm or edit before saving |
| UG-3 | Import a Pine Script strategy | User pastes a Pine Script snippet; the system extracts supported constructs and maps them to a strategy configuration, flagging any unsupported features |
| UG-4 | Understand what was generated | Regardless of input method, the user sees the resulting canonical JSON summary and can edit parameters before saving |
| UG-5 | Trust that execution is faithful | The strategy that runs (live or backtest) is compiled from the exact validated canonical JSON — no hidden interpretation differences between input methods |
| UG-6 | See assumptions made by the system | For natural language input, the user sees explicit assumptions (e.g., "Anchor price assumed to be current market price") and the system's confidence score |

### Non-Goals

| ID | Non-Goal | Rationale |
|----|----------|-----------|
| NG-1 | LLM-driven trade execution | The LLM must never place trades or bypass the RiskEngine. It only assists in strategy *definition* via structured intent extraction |
| NG-2 | Full Pine Script compatibility | Only grid-compatible constructs will be supported initially. Unsupported features are flagged and rejected with user-readable messages |
| NG-3 | New strategy types beyond GridStrategy | The pipeline supports `grid`, `dca`, and `indicator` as strategy type enums for future extensibility, but only `grid` will be implemented in this scope |
| NG-4 | Real-time strategy modification during live trading | Strategies must be stopped, reconfigured, and restarted. Hot-swapping config mid-execution is not in scope |
| NG-5 | AST as a user-facing or persisted concept | The AST is an internal compilation artefact. Users interact with canonical JSON or UI representations only |
| NG-6 | LLM explanations or commentary in output | The LLM returns structured JSON only (no markdown, no commentary). Explanations are generated deterministically from the canonical JSON |

---

## 3. Open Questions

| # | Question | Status |
|---|----------|--------|
| Q1 | **Should the natural language parser use the same LLM provider planned for sentiment/context, or a dedicated model?** The sentiment architecture uses periodic LLM calls with different prompt engineering needs. Strategy parsing is a one-shot structured-output request. | **Dedicated model.** The NL strategy parser will use a dedicated LLM instance/model, separate from the sentiment/context provider. This allows independent tuning of prompts, model selection, and rate limits for structured-output extraction. |
| Q2 | **What is the priority order of the three input methods?** The requirements doc suggests Phase 1 = UI selectors + AST/JSON core, Phase 2 = natural language, Phase 3 = Pine Script. Should this phasing be adopted as-is? | **Adopted as-is.** Phase 1: UI selectors + AST model + canonical JSON schema + validators + runtime compiler. Phase 2: Natural language extraction via LLM + DTO validation + assumptions/confidence UI. Phase 3: Pine Script import via Python sidecar (PyneCore). |
| Q3 | **Does the canonical JSON schema need to evolve beyond the current strategy config schema?** The requirements doc proposes a richer schema (with `schemaVersion`, `source` metadata, `positionManagement`, structured `spacing` objects). The existing schema ([13-strategy-config-schema.md](../../0-knowledge/13-strategy-config-schema.md)) is flatter. Which becomes the target? | **Evolve to the richer schema.** The canonical JSON will adopt the structure from the requirements doc: `schemaVersion`, `source` metadata (entry point, summary), structured `entry`/`exit`/`positionManagement` objects, and typed `spacing` with `type`/`value`/`direction`. The existing flat schema will be superseded. Backward compatibility mapping may be needed for existing saved strategies. |
| Q4 | **What subset of Pine Script constructs maps to the grid strategy config?** The parser needs a defined boundary of what's supported vs. flagged as unsupported. | Skipped — Phase 3 concern. Parser technology is not yet decided (PyneCore is one candidate, not confirmed). Construct mapping will be defined when the Pine Script input path is scoped. |
| Q5 | **What validation guardrails should exist for LLM-generated configs?** Beyond schema validation, should there be stricter "sanity" limits (e.g., max grid levels, min spacing) for AI-generated configs than for manually authored ones? | **No special guardrails.** LLM-generated configs go through the same validation pipeline as all other input methods. No additional or stricter limits for AI-authored strategies. |
| Q6 | **Should the confidence threshold be configurable?** The `StrategyIntentDto` includes a `confidence` field (0–1). Below what threshold should the system refuse to generate a strategy and ask the user for clarification? | **Yes, configurable.** The confidence threshold will be a platform-configurable setting. Below the threshold, the system prompts the user for clarification rather than generating a strategy. Default value TBD. |
| Q7 | **How should `unknown` enum values be handled in the StrategyIntentDto?** The schema allows `unknown` for direction, spacingDirection, and references. Should these block strategy creation, or prompt the user for disambiguation? | **Block.** Any `unknown` enum values in the StrategyIntentDto will prevent strategy creation. The user must resolve ambiguities before the config can be saved. |
| Q8 | **Is the PyneSys API dependency acceptable for Pine Script compilation?** PyneCore's core runtime is Apache 2.0, but `pyne compile` (Pine → Python) requires a paid PyneSys API key. Alternatives: (a) accept the vendor dependency, (b) build a limited in-house parser for the grid-compatible subset, (c) defer Pine Script import entirely until the dependency is evaluated. | **Deferred.** Decision will be made during Phase 3 scoping. PyneCore is one candidate, not a commitment. |
| Q9 | **How should different strategy types and indicators influence the AST/JSON design?** Discriminated union (strategy-type-specific sub-shapes) vs. fully composable conditions. And whether indicators are user-authored conditions or platform-provided context. | **Discriminated union + indicators as context.** `entry.mode` discriminates strategy-type-specific fields. Indicators remain in named context sections (`trend`, `bias`) managed by `MarketContextBuilder`. Composable expression conditions deferred to future phase when indicator-based strategies ship. |

---

## 4. Scope

### In Scope

#### Phase 1 — AST Core, Canonical JSON & UI Selectors

| Area | Deliverable |
|------|-------------|
| **AST model** | Structural nodes: `StrategyNode`, `GridEntryPlanNode`, `DcaPlanNode`, `ExitPlanNode`, `SizingRuleNode`, `RiskRuleNode`. Expression nodes (`AndNode`, `OrNode`, `ComparisonNode`, `CrossesAboveNode`, `IndicatorCallNode`, `ConstantNode`, `MarketSeriesReferenceNode`) are defined in the AST model but **not evaluated in Phase 1** — they exist to support future composable indicator strategies. Phase 1 uses strategy-type-specific structural nodes only (discriminated union). |
| **Canonical JSON schema** | Versioned schema (`schemaVersion`), structured `entry`/`exit`/`positionManagement`/`spacing` objects, `source` metadata (entry point, summary). Supersedes the existing flat strategy config schema. |
| **Validators** | Multi-level validation: schema validation, business rule validation, AST validation, canonical JSON validation, runtime compilation validation. Machine-readable errors + user-readable messages. |
| **Runtime compiler** | `IStrategyCompiler` — compiles canonical JSON into typed runtime execution plan objects consumed by the trading engine. Replaces direct JSON deserialization. |
| **UI selector path** | Angular form-based strategy authoring (dropdowns, toggles, numeric inputs) → direct mapping to AST → canonical JSON. Grid strategy parameters only. |
| **Strategy versioning** | Canonical JSON persisted with revision history and source metadata per save. |
| **Backward compatibility** | Migration path for existing `StrategyConfig` JSON to the new canonical schema. |

#### Phase 2 — Natural Language Input

| Area | Deliverable |
|------|-------------|
| **`StrategyIntentDto`** | Strict schema with `intent` (strategyType, direction, grid, entry, exit), `assumptions`, and `confidence` fields. Enum values: `grid`/`dca`/`indicator`/`unknown` for type; `long`/`short`/`unknown` for direction. |
| **LLM integration** | Dedicated LLM model (separate from sentiment provider). System prompt enforces structured JSON-only output, no markdown or commentary. |
| **DTO → AST conversion** | Deterministic .NET code converts validated `StrategyIntentDto` to internal AST. LLM output is never executed directly. |
| **`unknown` handling** | Any `unknown` enum values block strategy creation; user must resolve before saving. |
| **Confidence threshold** | Configurable platform setting. Below threshold, system refuses to generate and asks for clarification. |
| **Assumptions UI** | User sees explicit assumptions (e.g., "Anchor price assumed to be current market price") and confidence score before confirming. |

#### Phase 3 — Pine Script Import (scope deferred)

| Area | Deliverable |
|------|-------------|
| **Python sidecar** | Service that receives Pine Script, extracts strategy parameters, returns structured JSON to .NET backend. |
| **Parser technology** | TBD — PyneCore is one candidate; decision deferred to Phase 3 scoping. |
| **Unsupported feature handling** | Unsupported constructs flagged with user-readable messages; strategy creation blocked until resolved. |

### Out of Scope

| Item | Rationale |
|------|-----------|
| New strategy plugins (TrendBreakout, MeanReversion) | Only `GridStrategy` is implemented; pipeline supports future types via enums but does not deliver them |
| LLM-driven trade execution | LLM assists in authoring only; never touches the execution path |
| Hot-swap of strategy config during live trading | Stop → reconfigure → restart is the required flow |
| AST persistence or user-facing AST | AST is an internal compilation artefact only |
| Full Pine Script compatibility | Grid-compatible subset only; full language support is not a goal |

### Future Considerations

- **DCA and indicator strategy types**: The AST and canonical JSON schema include `DcaPlanNode` and `indicator` type enum to enable future strategy plugins without schema changes. Expression nodes are defined in the AST model but not evaluated until indicator-based strategies are delivered.
- **Composable indicator conditions**: When indicator strategies ship, expression nodes (`IndicatorCallNode`, `CrossesAboveNode`, etc.) will enable user-authored entry/exit conditions composed from indicators. This will require an expression evaluation engine.
- **YAML export/import**: JSON is the internal contract; YAML may be offered as a user-friendly export format later.
- **AI-assisted grid optimisation**: LLM could suggest parameter adjustments based on backtest results — not in scope but enabled by the architecture.
- **Multi-symbol strategies**: Schema could extend to support portfolio-level strategy definitions.

---

## 5. Technical Considerations

### Architecture

The strategy authoring pipeline introduces a new vertical slice within the existing clean architecture. All new services live in `TradingApp.Application`; no changes to `TradingApp.Domain` entities are required unless the `StrategyConfig` storage model changes.

```
TradingApp.Api
  └─ Controllers/StrategyAuthoringController  (new — receives UI/NL/Pine input)

TradingApp.Application
  ├─ StrategyAuthoring/
  │   ├─ Ast/                   (AST node types — internal, not persisted)
  │   ├─ Models/                (StrategyIntentDto, CanonicalStrategyJson)
  │   ├─ Services/
  │   │   ├─ IStrategyAuthoringService      (orchestrates input → AST → JSON → persist)
  │   │   ├─ IAstBuilder                    (builds AST from any input adapter result)
  │   │   ├─ IStrategyValidator             (multi-level validation)
  │   │   ├─ IStrategyNormalizer            (defaults, format standardisation)
  │   │   ├─ IStrategyCompiler              (canonical JSON → typed runtime plan)
  │   │   ├─ INaturalLanguageStrategyInterpreter  (Phase 2 — LLM → StrategyIntentDto)
  │   │   └─ IPineImportService             (Phase 3 — Pine Script → structured JSON)
  │   └─ Validators/            (schema, business rule, AST, JSON, compilation validators)
  └─ Abstractions/
      └─ Repositories/
          └─ IStrategyRepository            (extended — revision history, source metadata)

TradingApp.Infrastructure
  └─ Services/
      └─ LlmStrategyInterpreter            (Phase 2 — implements INaturalLanguageStrategyInterpreter)
      └─ PineImportSidecarClient            (Phase 3 — HTTP client to Python sidecar)

TradingApp.Persistence
  └─ Repositories/
      └─ StrategyRepository                 (extended — canonical JSON + revision storage)
```

### Integration Points

| Existing Component | Integration |
|-------------------|-------------|
| `ITradingStrategy` / `GridStrategy` | Currently consumes `StrategyConfig.ConfigJson` directly. After Phase 1, the worker loads canonical JSON and compiles it via `IStrategyCompiler` into typed runtime objects before passing to the strategy plugin. |
| `StrategyScheduler` | No change — receives `strategyConfigJson` string as today. The upstream change is that this string is now canonical JSON (richer schema) rather than the flat config. |
| `GridController.ProcessAsync` | No change — receives `strategyConfigJson` parameter. The compiled runtime plan is consumed upstream by `StrategyEngine`. |
| `MarketContextBuilder` | No change — indicator configuration (`trend`, `bias` sections) remains in the canonical JSON context sections, interpreted by the builder as today. |
| `RiskEngine` | No change — continues to validate signals post-strategy-evaluation. Risk limits from canonical JSON `risk` section are read at compilation time. |
| `BacktestRunner` | Consumes `BacktestConfig.StrategyConfigJson` — this becomes canonical JSON. The runtime compiler is invoked at backtest start. |

### Canonical JSON Schema (target structure)

```json
{
  "schemaVersion": 1,
  "name": "10 Grid Long",
  "strategyType": "grid",
  "direction": "long",
  "market": { "symbol": "BTC-USD", "timeframe": "15m" },
  "entry": {
    "mode": "grid",
    "anchorPrice": { "type": "market" },
    "levels": 10,
    "spacing": { "type": "percent", "value": 0.5, "direction": "down" }
  },
  "positionManagement": {
    "averaging": { "enabled": true, "method": "weighted_average_fill_price" }
  },
  "exit": {
    "takeProfit": {
      "enabled": true,
      "reference": "average_entry_price",
      "offset": { "type": "percent", "value": 2, "direction": "above" },
      "applyTo": "full_position"
    },
    "stopLoss": {
      "enabled": true,
      "reference": "average_entry_price",
      "offset": { "type": "percent", "value": 6, "direction": "below" },
      "applyTo": "full_position"
    }
  },
  "trend": {
    "emaFast": 20,
    "emaSlow": 50,
    "emaTrend": 200
  },
  "bias": {
    "rsiLength": 14,
    "rsiThreshold": 50
  },
  "hedge": {
    "enabled": true,
    "percent": 0.3
  },
  "risk": {
    "maxExposure": 2,
    "dailyLossLimitPercent": 2,
    "cooldownMinutes": 30
  },
  "source": {
    "entryPoint": "ui_selector",
    "summary": "Long grid with 10 levels, 0.5% spacing, TP 2%, SL 6%"
  }
}
```

### StrategyIntentDto Schema (NL output — Phase 2)

```json
{
  "intent": {
    "strategyType": "grid | dca | indicator | unknown",
    "direction": "long | short | unknown",
    "grid": {
      "levels": 0,
      "spacingPercent": 0,
      "spacingDirection": "down | up | unknown",
      "anchorPrice": "market_price | unknown"
    },
    "entry": {
      "conditions": []
    },
    "exit": {
      "takeProfit": {
        "reference": "average_entry_price | market_price | unknown",
        "offsetPercent": 0,
        "direction": "above | below | unknown"
      },
      "stopLoss": {
        "reference": "average_entry_price | market_price | unknown",
        "offsetPercent": 0,
        "direction": "above | below | unknown"
      }
    }
  },
  "assumptions": [],
  "confidence": 0.0
}
```

### LLM System Prompt (Phase 2)

```
You are a trading strategy parser.
Your job is to convert natural language descriptions of trading strategies
into a strictly defined JSON structure called StrategyIntent.

You MUST:
- Return valid JSON only (no markdown, no commentary)
- Follow the schema exactly
- Use only allowed enum values
- Never invent fields
- Never guess silently — if uncertain, mark as "unknown" and add an assumption
- Include assumptions explicitly
- Include a confidence score between 0 and 1

You are NOT executing the strategy.
You are NOT explaining.
You are extracting structured intent.

If the input is unclear or incomplete:
- Fill what you can
- Mark unknown fields
- Add assumptions

Supported strategy types: grid, dca, indicator
Supported directions: long, short, unknown
Supported references: average_entry_price, market_price, unknown
```

### Validation Pipeline

Validation occurs at five levels, each producing machine-readable errors and user-readable messages:

```
1. Schema validation      — JSON structure conforms to StrategyIntentDto or canonical JSON schema
2. Business validation    — values within safe limits (e.g., levels > 0, spacing > 0)
3. AST validation         — well-formed tree, required nodes present, no invalid nesting
4. Canonical JSON validation — all required fields resolved (no "unknown"), schema version present
5. Runtime compilation    — compiled plan is internally consistent (e.g., TP > entry, SL < entry for long)
```

### Constraints

| Constraint | Detail |
|-----------|--------|
| **Tech stack** | All authoring services in C#/.NET. Python sidecar only for Phase 3 Pine Script import. LLM integration via HTTP client in Infrastructure layer. |
| **Determinism** | The compilation pipeline (AST → canonical JSON → runtime plan) is fully deterministic. Only the NL input adapter involves a non-deterministic LLM call; all downstream processing is code-driven. |
| **Security** | LLM prompt injection mitigated by strict JSON-only output contract and schema validation. User-provided Pine Script sanitised before forwarding to sidecar. No user input reaches the trading engine unvalidated. |
| **Tenant isolation** | All strategies remain tenant-scoped by `UserId`. The authoring pipeline inherits the existing multi-tenant model. |
| **Backward compatibility** | Existing `StrategyConfig.ConfigJson` entries must be migratable to the new canonical schema. A one-time migration or lazy upgrade path is required. |
| **Database** | Strategy revisions stored in SQLite (POC) / Azure SQL (production) via EF Core. Canonical JSON stored as a column on a versioned strategy table. |

### Suggested .NET Services

| Service | Layer | Phase |
|---------|-------|-------|
| `IStrategyAuthoringService` | Application | 1 |
| `IAstBuilder` | Application | 1 |
| `IStrategyValidator` | Application | 1 |
| `IStrategyNormalizer` | Application | 1 |
| `IStrategyCompiler` | Application | 1 |
| `IStrategyRepository` (extended) | Application (interface) / Persistence (impl) | 1 |
| `INaturalLanguageStrategyInterpreter` | Application (interface) / Infrastructure (impl) | 2 |
| `IPineImportService` | Application (interface) / Infrastructure (impl) | 3 |

---

## 6. Use Cases

### Personas

| Persona | Description | Primary Input Method |
|---------|-------------|---------------------|
| **Technical Trader (Tom)** | Experienced crypto trader comfortable with JSON and quantitative parameters. Wants full control over every grid setting. Currently the only supported user type. | UI selectors (Phase 1) |
| **Casual Trader (Casey)** | Trades crypto regularly but has no programming background. Thinks in natural language ("long grid, 10 levels, 0.5% spacing"). Wants to describe a strategy and have the system figure out the rest. | Natural language (Phase 2) |
| **TradingView Migrator (Maya)** | Has Pine Script strategies on TradingView and wants to run them on this platform without rewriting. Expects a "paste and import" experience. | Pine Script import (Phase 3) |

---

### Feature: Strategy Authoring via UI Selectors (Phase 1)

**US-1.1** As Tom, I want to create a new grid strategy by selecting parameters from structured form controls, so that I don't have to write raw JSON.

**Acceptance Criteria:**
- Form presents grid-specific fields: levels, spacing (type/value/direction), anchor price, TP, SL, hedge, risk limits, trend filter, bias filter
- Form fields are driven by the canonical JSON schema — `entry.mode = "grid"` determines which fields appear
- On submit, the system builds an AST, validates, normalises, and persists canonical JSON
- User sees the resulting canonical JSON summary before confirming save

**US-1.2** As Tom, I want to edit an existing strategy and see what changed compared to the previous version, so that I can track my parameter tuning.

**Acceptance Criteria:**
- Each save creates a new revision with `source` metadata
- User can view revision history and diff between any two versions
- Only the active version is used by the trading engine

**US-1.3** As Tom, I want the system to reject invalid configurations with clear error messages, so that I don't accidentally deploy a broken strategy.

**Acceptance Criteria:**
- Validation errors are presented per-field with user-readable messages
- Errors surface from all five validation levels (schema, business, AST, JSON, compilation)
- Submit is blocked until all errors are resolved

**US-1.4** As Tom, I want my existing strategies (saved under the old flat schema) to continue working after the schema upgrade.

**Acceptance Criteria:**
- Existing `StrategyConfig.ConfigJson` entries are migrated (or lazily upgraded) to the new canonical schema
- No data loss on migration
- Migrated strategies pass full validation pipeline

---

### Feature: Natural Language Strategy Authoring (Phase 2)

**US-2.1** As Casey, I want to type a plain English description of a grid strategy and have the system generate a valid configuration, so that I don't need to know the parameter schema.

**Acceptance Criteria:**
- Text input field accepts natural language (e.g., "Long grid with 10 levels, 0.5% spacing, take profit at 2% above average entry, stop loss at 6% below")
- System calls the dedicated LLM with the strategy parser prompt
- LLM returns a `StrategyIntentDto` with `intent`, `assumptions`, and `confidence`
- DTO is schema-validated; any `unknown` values block creation and prompt the user to clarify
- If confidence is below the configurable threshold, the system asks for more detail

**US-2.2** As Casey, I want to see the assumptions the system made and the confidence score before saving, so that I can verify the system understood my intent.

**Acceptance Criteria:**
- Assumptions are displayed as a bullet list (e.g., "Anchor price assumed to be current market price")
- Confidence score is displayed (e.g., "95% confidence")
- User can accept, edit parameters, or discard and retry with different wording

**US-2.3** As Casey, I want to edit the generated parameters before saving, so that I can correct anything the system got wrong.

**Acceptance Criteria:**
- After NL generation, the UI switches to the same form view as UI selectors (US-1.1) pre-populated with the generated values
- User can modify any field; the result goes through the same validation pipeline
- `source.entryPoint` is recorded as `"natural_language"`

**US-2.4** As Casey, I want the system to tell me clearly when my description is too vague, so that I know what to add.

**Acceptance Criteria:**
- If confidence < threshold, the system displays which fields could not be determined
- If `unknown` enum values are present, the system lists them and asks the user to specify
- The system does not silently invent values — it either extracts or marks as unknown

---

### Feature: Pine Script Import (Phase 3)

**US-3.1** As Maya, I want to paste a Pine Script snippet and have the system extract a grid strategy configuration, so that I can reuse my TradingView strategies.

**Acceptance Criteria:**
- Text input accepts Pine Script code
- System forwards to the Python sidecar, which parses and returns structured JSON
- Supported constructs are mapped to canonical JSON; unsupported constructs are listed with user-readable messages
- User sees the resulting configuration and can edit before saving
- `source.entryPoint` is recorded as `"pine_import"`

**US-3.2** As Maya, I want to see which parts of my Pine Script were not supported, so that I can decide whether the import is usable.

**Acceptance Criteria:**
- Unsupported constructs are listed per-line or per-block with explanations
- Strategy creation is blocked if critical constructs (e.g., entry logic) cannot be mapped
- Non-critical unsupported features (e.g., plot statements) are flagged as warnings, not blockers

---

### Feature: Strategy Compilation & Execution (Phase 1 — cross-cutting)

**US-4.1** As a platform operator, I want all strategies to be compiled from canonical JSON into typed runtime objects before execution, so that the engine never consumes raw JSON.

**Acceptance Criteria:**
- `IStrategyCompiler` produces a typed execution plan from canonical JSON
- The worker invokes the compiler at strategy load time (both live and backtest)
- Compilation failures are logged and prevent strategy activation
- The compiled plan is the sole input to `IStrategyEngine`

**US-4.2** As Tom, I want to run a backtest on a strategy created via any input method and get identical results to a manually authored strategy with the same parameters.

**Acceptance Criteria:**
- A strategy created via UI selectors, NL, or Pine import produces the same canonical JSON for the same parameters
- Backtests run against canonical JSON produce deterministic, reproducible results
- No difference in backtest output based on input method

---

*Awaiting feedback on Section 6 (Use Cases) before finalising the document.*

---

## 7. References

| Document | Path |
|----------|------|
| End-to-End Trading Strategy Architecture | [trading_strategy_e2e_architecture.md](../1-discover/prd/trading_strategy_e2e_architecture.md) |
| Strategy Architecture Requirements | [trading_strategy_architecture_requirements.md](../1-discover/prd/trading_strategy_architecture_requirements.md) |
| LLM JSON vs AST Rationale | [llm_json_vs_ast.md](../1-discover/prd/llm_json_vs_ast.md) |
| Strategy Config Schema | [13-strategy-config-schema.md](../../0-knowledge/13-strategy-config-schema.md) |
| Strategy Customisation | [12-strategy-customisation.md](../../0-knowledge/12-strategy-customisation.md) |
| Strategy Runtime Model | [14-strategy-runtime-model.md](../../0-knowledge/14-strategy-runtime-model.md) |
| Grid Controller | [15-grid-controller.md](../../0-knowledge/15-grid-controller.md) |
| Signal Contracts | [16-signal-contracts.md](../../0-knowledge/16-signal-contracts.md) |
| LLM Context & Sentiment Architecture | [17-llm-context-sentiment-architecture.md](../../0-knowledge/17-llm-context-sentiment-architecture.md) |
| Backtesting Architecture | [18-backtesting-architecture.md](../../0-knowledge/18-backtesting-architecture.md) |
| Scheduling Architecture | [19-scheduling-architecture.md](../../0-knowledge/19-scheduling-architecture.md) |
| Architecture Decisions | [10-architecture-decisions.md](../../0-knowledge/10-architecture-decisions.md) |
| Domain Model | [04-domain-model.md](../../0-knowledge/04-domain-model.md) |
| PyneCore (Pine Script candidate) | [github.com/PyneSys/pynecore](https://github.com/PyneSys/pynecore) |
