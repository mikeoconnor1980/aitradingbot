# Strategy Interpreter Architecture

The strategy interpreter converts natural-language trading intent into a structured `StrategyConfig`. It is one of three AI features in the codebase and is intentionally separate from both the market-context provider and the strategy-review system.

## Pipeline

```
User text -> POST /api/strategies/interpret
  -> InterpretStrategyCommand
  -> IStrategyInterpreter / StrategyInterpreter
  -> ILlmClient / OpenAiCompatibleLlmClient
  -> OpenAI-compatible provider
  -> Structured JSON response
  -> StrategyIntentDto
  -> Strategy Builder UI review and save flow
```

The interpreter is an authoring aid only. It does not place orders and it is not used during live trading.

## Key Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `ILlmClient` | `src/TradingApp.Application/Abstractions/Services/ILlmClient.cs` | LLM contract for interpretation |
| `OpenAiCompatibleLlmClient` | `src/TradingApp.AI/Services/OpenAiCompatibleLlmClient.cs` | OpenAI-compatible HTTP client |
| `IStrategyInterpreter` | `src/TradingApp.Application/Abstractions/Services/IStrategyInterpreter.cs` | Interpretation abstraction |
| `StrategyInterpreter` | `src/TradingApp.AI/Services/StrategyInterpreter.cs` | Prompt orchestration and response parsing |
| `StrategyInterpreterPrompt` | `src/TradingApp.AI/Prompts/StrategyInterpreterPrompt.cs` | Schema and behavior prompt |
| `InterpretStrategyCommand` | `src/TradingApp.Application/StrategyAuthoring/Commands/InterpretStrategyCommand.cs` | CQRS entry point |
| `StrategyIntentDto` | `src/TradingApp.Application/StrategyAuthoring/Models/StrategyIntentDto.cs` | Parsed config, confidence, assumptions, clarification |

## Default Provider Shape

The default configuration shape comes from `LlmOptions` in `src/TradingApp.Application/Abstractions/Configuration/LlmOptions.cs`.

| Field | Default |
|------|---------|
| `Provider` | `Gemini` |
| `BaseUrl` | `https://generativelanguage.googleapis.com/v1beta/openai/` |
| `ModelName` | `gemini-2.0-flash` |
| `TimeoutSeconds` | `30` |

Runtime configuration can override those defaults in `appsettings.json`, but the interpreter is built around an OpenAI-compatible provider contract rather than a provider-specific SDK. The option-class defaults are still the right baseline for planning because they describe the interpreter's fallback contract even when environments override the model name.

## API Contract

The interpreter endpoint is `POST /api/strategies/interpret`.

It accepts a short natural-language description and returns a `StrategyIntentDto` containing:

- populated `StrategyConfig`
- confidence score
- assumptions list
- optional clarification guidance

The UI can then patch those values into the strategy builder and let the user confirm or edit them before saving.

## Persistence Boundary

When an interpreted strategy is saved, the original text is preserved in `StrategyConfig.Source.SourceText`. This makes the interpretation flow auditable and allows users to reopen and re-interpret the strategy later.

## Relationship To Other AI Systems

The interpreter should be understood as only one AI subsystem.

### Strategy Reviewer

The review feature is separate from interpretation.

| Component | Role |
|-----------|------|
| `IStrategyReviewer` / `StrategyReviewer` | Reviews saved strategy revisions |
| `IReviewLlmClient` / `ReviewLlmClient` | Dedicated review client |
| `LlmReviewOptions` | Separate review configuration |
| `StrategyReviewPrompt` | Review-specific prompt |
| `RequestStrategyReviewCommand` | CQRS entry point for review generation |

### Market Context Provider

The live/backtest context system is also separate.

| Component | Role |
|-----------|------|
| `ILlmContextClient` / `LlmContextClient` | Market-context LLM client |
| `ILlmContextProvider` / `LlmContextProvider` | Prompt building and context parsing |
| `MarketContextPrompt` | Market regime and event-risk prompt |

See [17-llm-context-sentiment-architecture.md](17-llm-context-sentiment-architecture.md) for the broader three-client AI split and the runtime context flow.

## Extending The Interpreter

When adding new strategy schema features:

1. Update the relevant enum or config model.
2. Update `StrategyInterpreterPrompt` so the model can emit the new shape.
3. Update serialization or polymorphic converters if needed.
4. Update the Angular form-mapping logic.
5. Add tests that cover both parsing and UI hydration.

## Related Knowledge

- [12-strategy-customisation.md](12-strategy-customisation.md)
- [13-strategy-config-schema.md](13-strategy-config-schema.md)
- [17-llm-context-sentiment-architecture.md](17-llm-context-sentiment-architecture.md)

## Future Recommendations

- Add provider-specific validation and health reporting so users can see when interpretation failures are caused by model configuration rather than prompt quality.
- Expand prompt examples for newer condition types and signal-mode strategies.
- Consider storing interpretation diagnostics for failed generations so prompt tuning is easier over time.
