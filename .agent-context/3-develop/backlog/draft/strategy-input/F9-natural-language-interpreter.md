# PBI Specification: F9 — Natural Language Strategy Interpreter

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-04-02
**PRD:** [02-strategy-input-pipeline.md](../../../prd-draft/02-strategy-input-pipeline.md)
**Reference:** [17-llm-context-sentiment-architecture.md](../../0-knowledge/17-llm-context-sentiment-architecture.md), [strategy-builder-ui-detailed.md](../../1-discover/prd/strategy-builder-ui-detailed.md)
**Implementation Phase:** 2 (Natural Language Authoring)
**Risk Level:** High
**Depends On:** F1 (Extensible Strategy Schema), F2 (Strategy Builder UI)
**Independent Of:** F5 (Condition Evaluator), F7 (EMA Handler), F8 (MACD Handler) — the interpreter generates schema-valid JSON but does not evaluate conditions
**Merged:** F10 (Natural Language Authoring UI) merged into this PBI

---

## Summary

Enable traders to describe strategies in plain English via a text input in the Strategy Builder UI. An LLM interprets the text and maps it directly to the canonical `StrategyConfig` schema — no AST intermediate layer.

The interpreter produces a `StrategyIntentDto` containing the mapped config, a confidence score, and a list of assumptions. The UI displays these results and loads the generated config into the existing Strategy Builder form for review and editing.

> **Note:** The interpreter only produces JSON config — it never evaluates conditions. This means F9 can ship before or in parallel with F5/F7/F8. Generated configs for condition types whose handlers haven't shipped yet (e.g. `price_vs_ema`, `macd_cross`) are still schema-valid and can be saved; they evaluate once the corresponding handlers land.

This is the **"describe then edit"** flow: natural language is the entry point, the form-based builder is the editor.

### User Story

> As a **trader**, I want to **describe my strategy in plain text and see the generated configuration in the form builder** so that **I can quickly create strategies and fine-tune the details**.

### Business Value

Lowers the barrier to strategy creation. Traders who don't want to learn the form-based builder can describe what they want in natural language and get a working starting point in seconds. Encourages experimentation and reduces time-to-first-strategy.

---

## Requirements

### Functional Requirements

#### LLM Provider

- [ ] Abstract LLM access behind `ILlmClient` with `CompleteAsync(string systemPrompt, string userMessage, CancellationToken)` so the provider is swappable
- [ ] **Primary provider:** **Google Gemini 2.0 Flash** via Google AI Studio API — free tier (15 RPM / 1,500 requests/day / 1M tokens/day), good structured JSON output
- [ ] **Fallback / offline:** **Ollama** running locally (`llama3.1:8b`) — zero cost, no API key, useful for offline dev or prompt iteration without burning API quota
- [ ] Gemini exposes an OpenAI-compatible endpoint at `https://generativelanguage.googleapis.com/v1beta/openai/` — use a single OpenAI-compatible HTTP client with configurable base URL and model name
- [ ] Ollama also serves OpenAI-compatible API at `http://localhost:11434/v1/` — same client code, different config
- [ ] Provider configuration via `appsettings.json`: `LlmProvider` (Gemini | Ollama), `BaseUrl`, `ModelName`, `ApiKey` (required for Gemini, omit for Ollama)
- [ ] API key stored in user secrets during development (`dotnet user-secrets set "Llm:ApiKey" "<key>"`), environment variable in production

#### Interpreter Service

- [ ] `IStrategyInterpreter` service with `InterpretAsync(string userText, CancellationToken)` returning `StrategyIntentDto`
- [ ] `StrategyIntentDto` contains: `StrategyConfig config`, `decimal confidence`, `List<Assumption> assumptions`, `string? clarificationNeeded`
- [ ] Maps directly to canonical `StrategyConfig` — no AST intermediate representation
- [ ] Uses `ILlmClient` abstraction (Gemini 2.0 Flash primary, Ollama offline fallback)
- [ ] Prompt engineering: system prompt includes the schema definition, valid condition types, operator enums, and example configs
- [ ] Confidence calculation: based on how many fields the LLM populated vs. how many were ambiguous
- [ ] Lives in new **TradingApp.AI** project alongside `ILlmClient` and provider implementations

#### Interpretation Rules

- [ ] Generates conditions for **all schema-defined condition types** (RSI, Price vs EMA, MACD, Grid) — the interpreter maps to the schema, not to what handlers are currently registered
- [ ] If the described strategy references indicators **not in the schema** (e.g. Ichimoku, Bollinger), sets `clarificationNeeded` with explanation
- [ ] Recognises `strategyMode` from context: "grid" keywords (grid, levels, spacing) → grid mode; signal keywords (when, cross, above, condition) → signal mode
- [ ] Defaults to reasonable values where unspecified (e.g. RSI period 14, TP 2%, SL 1.5%) and records each default as an `Assumption`

#### NL Text Storage

- [ ] Store the original NL text on `StrategyConfig` (new `sourceText` field) so users can see what they originally described
- [ ] When editing a previously NL-created strategy, the saved text is pre-loaded into the NL text area
- [ ] User can modify the saved text and re-interpret — UI shows a diff/confirmation before overwriting the form

#### API Endpoint

- [ ] `POST /api/strategies/interpret` — accepts `{ text: string }`, returns `StrategyIntentDto`
- [ ] No authentication required (POC phase) — will be gated by auth in a future PBI
- [ ] Rate limited: max 10 requests per minute per user (by IP in POC phase)
- [ ] Input sanitized — no prompt injection via user text reaching LLM (instruction hierarchy, input/output separation)
- [ ] Maximum input length: 500 characters
- [ ] Reject empty/whitespace-only text with 400 Bad Request

#### UI — NL Input Component

- [ ] Text area component at the top of Strategy Builder (collapsible section)
- [ ] Placeholder: "Describe your strategy in plain English, e.g. 'Buy ETH when RSI drops below 30 with a 2% take profit'"
- [ ] Character counter (max 500)
- [ ] "Generate" button — calls `POST /api/strategies/interpret`
- [ ] Loading spinner during interpretation
- [ ] Error state: shows message if interpreter fails or is rate limited

#### UI — Assumptions Display

- [ ] After generation, show assumptions panel listing each assumption: field name, assumed value, reason
- [ ] Example: "RSI Period — assumed 14 (standard default)"
- [ ] Each assumption has an "Accept" (auto-selected) or "Edit" action
- [ ] "Edit" scrolls to the relevant field in the form below

#### UI — Confidence Badge

- [ ] Confidence score displayed as badge: High (≥ 0.8, green), Medium (0.5–0.79, amber), Low (< 0.5, red)
- [ ] Low confidence shows warning: "The system wasn't confident about this interpretation. Please review carefully."
- [ ] If `clarificationNeeded` is set, display the clarification message prominently
- [ ] Warn but always allow save — user decides whether to proceed with low-confidence results

#### UI — Form Population

- [ ] Generated `StrategyConfig` from the interpreter loads into the reactive form model (F2)
- [ ] Strategy mode toggle (grid/signal) set automatically from interpreted config
- [ ] All fields editable — NL is a starting point, not a lock
- [ ] Form validation runs after population; any validation errors highlighted

#### UI — Iteration

- [ ] User can edit text and re-generate — replaces current form values (with confirmation dialog)
- [ ] When editing a strategy created via NL, saved `sourceText` is pre-loaded and editable
- [ ] Re-interpret shows what changed vs. the current form values before applying
- [ ] "Clear" button resets both NL text and form

#### Non-Functional

- [ ] LLM call timeout: 30 seconds (Ollama may be slower on first call due to model warm-up; Gemini typically responds in 1-3s)
- [ ] Graceful fallback: if LLM is unavailable, return error with `clarificationNeeded = "Service temporarily unavailable"`
- [ ] Logging: log user text (sanitized), confidence, and processing time for quality improvement
- [ ] No PII in logs — user text may contain asset names but no personal data

---

## User Flow

### Happy Path

1. Trader opens Strategy Builder
2. NL input section is visible at the top (collapsible, expanded by default for new strategies)
3. Trader types: "Buy ETH when RSI drops below 30 with 2% take profit"
4. Trader clicks "Generate"
5. Loading spinner shown while LLM processes (~1-3s with Gemini)
6. Response arrives — confidence badge shown (e.g. green "High: 0.92")
7. Assumptions panel lists defaults: "RSI Period — assumed 14", "Stop Loss — assumed 1.5%"
8. Strategy Builder form below is populated: signal mode, ETH asset, RSI condition, TP 2%
9. Trader reviews, clicks "Edit" on an assumption to adjust, or edits the form directly
10. Trader saves — `StrategyConfig` persisted with `sourceText` field containing the original NL input

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Empty or whitespace-only text submitted | 400 Bad Request; UI shows inline validation "Please enter a strategy description" |
| Text exceeds 500 characters | UI prevents submission (character counter); API returns 400 if bypassed |
| Prompt injection attempt detected | User text is sandboxed in data section; LLM output validated against schema; malformed output rejected with error message |
| LLM service unavailable | Error message: "Strategy interpreter is temporarily unavailable. Please try again or use the form builder." |
| Rate limit exceeded (11th request/min) | HTTP 429; UI shows "Too many requests. Please wait a moment." |
| Ambiguous input (confidence < 0.5) | Red badge + warning; user can still save but is warned |
| Non-schema indicator referenced | `clarificationNeeded` explains which indicator is not yet supported |

---

## Acceptance Criteria

### Backend

- [ ] **Given** "Buy ETH when RSI drops below 30 with 2% take profit", **When** interpreted, **Then** returns signal mode config with RSI condition (period 14, operator lt, threshold 30) and TP 2%
- [ ] **Given** "Set up a 5-level grid on BTC with 0.5% spacing", **When** interpreted, **Then** returns grid mode config with gridLevels 5, gridSpacing 0.5
- [ ] **Given** ambiguous text "trade BTC", **When** interpreted, **Then** confidence < 0.5 and `clarificationNeeded` populated
- [ ] **Given** text referencing unsupported indicator "Ichimoku cloud", **When** interpreted, **Then** `clarificationNeeded` explains this condition type is not yet supported
- [ ] **Given** 11th request within 1 minute, **When** submitted, **Then** HTTP 429 returned
- [ ] **Given** LLM unavailable, **When** interpreted, **Then** appropriate error response, no unhandled exception
- [ ] **Given** empty or whitespace-only text, **When** submitted, **Then** HTTP 400 returned
- [ ] **Given** strategy saved after NL generation, **When** config persisted, **Then** `sourceText` field contains original NL input

### UI

- [ ] **Given** user types "Buy ETH when RSI < 30, take profit at 3%", **When** Generate clicked, **Then** form populated with signal mode, RSI condition, TP 3%, and assumptions shown
- [ ] **Given** interpretation returns confidence 0.4, **When** displayed, **Then** red badge and warning message visible, save still allowed
- [ ] **Given** assumption "RSI Period — assumed 14", **When** "Edit" clicked, **Then** view scrolls to RSI period field in the form
- [ ] **Given** form already has values, **When** user re-generates, **Then** confirmation dialog shown before overwriting
- [ ] **Given** interpreter returns error (rate limit), **When** displayed, **Then** error message shown, form unchanged
- [ ] **Given** editing a strategy with saved `sourceText`, **When** Strategy Builder opens, **Then** NL text area pre-loaded with saved text
- [ ] **Given** user modifies saved NL text and re-interprets, **When** result returned, **Then** changes vs. current form highlighted before applying

### Security Considerations

- [ ] User text is placed in a **data** section of the prompt, never as system instructions
- [ ] LLM output is validated against schema before returning — malformed JSON rejected
- [ ] Rate limiting prevents abuse of Gemini API quota (15 RPM free tier limit; app-level limit of 10 RPM provides headroom)

### Release Notes Information

- **Heading**: Natural Language Strategy Authoring
- **Release note type**: Feature
- **Release Note Summary**: Describe your strategy in plain English to auto-generate a configuration. Review assumptions, adjust confidence and defaults in the form builder, then save.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Technical Considerations

### Bounded Contexts

- **TradingApp.AI** (new project): `ILlmClient`, `GeminiLlmClient`, `OllamaLlmClient`, `IStrategyInterpreter`, `StrategyInterpreter`, `StrategyIntentDto`, prompt templates
- **TradingApp.Api**: `StrategiesController` interpret endpoint, rate limiting middleware
- **TradingApp.Domain**: `StrategyConfig.SourceText` field addition
- **frontend/trading-ui**: NL input component, assumptions panel, confidence badge

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/strategies/interpret` | Accepts `{ text: string }`, returns `StrategyIntentDto` |

---

## Out of Scope

- Authentication/authorization on the interpret endpoint (POC phase — future PBI)
- Multi-turn conversation ("make it more aggressive") — single-shot interpretation only
- Voice input
- Non-English language support
- Persisting interpretation history/audit log (beyond the saved `sourceText` on the config)
- LLM fine-tuning or custom model training
