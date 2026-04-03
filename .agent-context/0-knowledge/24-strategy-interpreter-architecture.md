# Strategy Interpreter Architecture

Natural language strategy interpretation allows users to describe trading intent in plain English. An OpenAI-compatible LLM converts it to a valid `StrategyConfig` with confidence scoring and assumption tracking.

---

## Pipeline

```
User text → POST /api/strategies/interpret
  → InterpretStrategyCommand (CQRS)
  → StrategyInterpreter (IStrategyInterpreter)
  → OpenAiCompatibleLlmClient (HTTP)
  → LLM provider (Gemini / Ollama / OpenAI-compatible)
  → Parse structured JSON response
  → StrategyIntentDto (config + confidence + assumptions)
  → User reviews in Strategy Builder UI
  → Save → SourceMetadata.SourceText persisted
```

---

## Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `ILlmClient` | `src/TradingApp.Application/Abstractions/Services/ILlmClient.cs` | Application-layer LLM client contract |
| `OpenAiCompatibleLlmClient` | `src/TradingApp.AI/Services/OpenAiCompatibleLlmClient.cs` | HTTP client for any OpenAI-compatible endpoint |
| `IStrategyInterpreter` | `src/TradingApp.Application/Abstractions/Services/IStrategyInterpreter.cs` | Application-layer interpretation contract |
| `StrategyInterpreter` | `src/TradingApp.AI/Services/StrategyInterpreter.cs` | Calls LLM, parses response, stamps source metadata |
| `StrategyInterpreterPrompt` | `src/TradingApp.AI/Prompts/StrategyInterpreterPrompt.cs` | System prompt with schema, constraints, and examples |
| `InterpretStrategyCommand` | `src/TradingApp.Application/StrategyAuthoring/Commands/InterpretStrategyCommand.cs` | CQRS command + handler |
| `LlmOptions` | `src/TradingApp.Application/Abstractions/Configuration/LlmOptions.cs` | Configuration (provider, base URL, model, API key, timeout) |
| `StrategyIntentDto` | `src/TradingApp.Application/StrategyAuthoring/Models/StrategyIntentDto.cs` | Response DTO: config, confidence, assumptions, clarification |

---

## API Contract

**Endpoint:** `POST /api/strategies/interpret` (rate-limited: 10 req/min/IP)

**Request:** `{ "text": "Buy ETH when RSI drops below 30 with 2% take profit" }` (max 500 chars)

**Response (200):** `StrategyIntentDto` with populated `StrategyConfig`, confidence score (0–1), assumptions list, and optional clarification message.

**Error Responses:** 400 (empty/whitespace/too long input), 429 (rate limit exceeded with `Retry-After` header)

---

## LLM Configuration

Configured via `appsettings.json` section `Llm` (bound to `LlmOptions`):

| Field | Description |
|-------|-------------|
| `Provider` | Label (e.g. `"Gemini"`, `"Ollama"`) |
| `BaseUrl` | OpenAI-compatible endpoint URL |
| `ModelName` | Model identifier |
| `ApiKey` | API key (required) |
| `TimeoutSeconds` | HTTP timeout (default 30) |

---

## SourceText Persistence

When a strategy is saved after interpretation:
- `StrategyConfig.Source.SourceText` stores the original user input
- Frontend pre-loads `sourceText` when opening the strategy editor for re-interpretation
- Persisted in the strategy revision history for audit

---

## Frontend Integration

| Component | Purpose |
|-----------|---------|
| `nl-input-card` | Textarea with character counter, generate button, loading state |
| `assumptions-panel` | Displays LLM assumptions with edit links to relevant form fields |
| `confidence-badge` | Colour-coded confidence indicator with clarification warnings |

Form population uses `patchValue()` and `ConditionFactoryService` to map `StrategyIntentDto.Config` into the reactive form. Confirm-before-overwrite dialog shown when re-interpreting over existing form values.

---

## Extending Interpretation

To add a new entry condition type (e.g., Bollinger Bands):

1. Add type to `EntryConditionType` enum and create typed params record
2. Update `StrategyInterpreterPrompt.SystemPrompt` to document the new condition
3. Update `EntryConditionParamsConverter` for polymorphic serialization
4. Add condition factory method in `ConditionFactoryService` (frontend)
5. Test via `StrategyInterpreterTests`
