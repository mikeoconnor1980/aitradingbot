# Pine Script Pattern Extractor

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-31T12:00:00Z
**Epic:** Pine Script Indicator Integration (Option F)

## User Story

As a **trader**, I want to **paste a TradingView Pine Script indicator and have the system automatically extract the indicator functions, parameters, plot configuration, and alert conditions** so that **the system can compute and display those indicators without executing arbitrary Pine Script code**.

### Business Value

This is the foundation for the entire Pine Script integration epic. By extracting structured indicator configuration from pasted Pine Script, we avoid the complexity and license risk of full Pine Script execution (AGPL-licensed PineTS) while covering ~70-80% of popular TradingView indicators. The extractor enables users to bring their own indicators from TradingView's massive library rather than being limited to our built-in set.

### Prerequisite Research

See `.agent-context/0-knowledge/25-pine-script-integration-research.md` for the full ecosystem research, approach comparison, and license analysis.

---

## Requirements

### Functional Requirements

- [ ] **Pine Script version detection** — Parse `//@version=N` header to identify Pine Script v4/v5/v6. Support v5 as primary, v4 as best-effort, v6 experimental
- [ ] **`indicator()` / `strategy()` header extraction** — Extract indicator name, `overlay` setting (true = price overlay, false = separate pane), `shorttitle`, `format`, `precision`
- [ ] **`input()` parameter extraction** — Extract all `input.*()` calls: name, type (`int`, `float`, `bool`, `string`, `source`), default value, min/max constraints, options list. These become user-configurable parameters
- [ ] **`ta.*` function extraction** — Identify all `ta.*` calls and their arguments. Supported functions: `ta.sma`, `ta.ema`, `ta.wma`, `ta.vwma`, `ta.rsi`, `ta.macd`, `ta.atr`, `ta.bbands`, `ta.stoch`, `ta.cci`, `ta.mfi`, `ta.obv`, `ta.crossover`, `ta.crossunder`, `ta.highest`, `ta.lowest`, `ta.change`, `ta.roc`
- [ ] **`plot()` / `hline()` extraction** — Extract: series variable being plotted, title, color, linewidth, style (line/histogram/area/stepline), offset. Extract `hline()` for reference lines (e.g., RSI 30/70)
- [ ] **`plotshape()` / `plotchar()` extraction** — Extract condition expression, location (abovebar/belowbar), shape, color, text. These represent visual signal markers
- [ ] **`alertcondition()` extraction** — Extract condition expression, title, message. These are the primary signal triggers for strategy integration
- [ ] **Condition expression parsing** — Parse simple boolean expressions used in `alertcondition()` and `plotshape()`: comparisons (`>`, `<`, `>=`, `<=`, `==`), logical operators (`and`, `or`, `not`), function calls (`ta.crossover`, `ta.crossunder`), variable references
- [ ] **Variable resolution** — Track variable assignments to resolve what indicator a variable refers to (e.g., `fast = ta.ema(close, 9)` → variable `fast` is EMA(close, 9))
- [ ] **Source parameter mapping** — Map Pine Script source parameters (`close`, `open`, `high`, `low`, `hl2`, `hlc3`, `ohlc4`, `volume`) to OHLCV data fields
- [ ] **Unsupported construct detection** — Identify and report constructs that cannot be extracted: `for`/`while` loops, custom functions (`f_myFunc()`), array/matrix operations, `request.security()` (MTF), `strategy.*()` order functions, `line.new()`/`label.new()` drawing, `var` persistent variables with mutation, string manipulation
- [ ] **Validation result model** — Return a structured result: `{ isValid: bool, indicators: [...], plots: [...], alerts: [...], inputs: [...], unsupportedConstructs: [...], warnings: [...] }`

### Non-Functional Requirements

- [ ] Extraction completes in < 100ms for scripts up to 500 lines
- [ ] No execution of user-provided code — extraction is static analysis only (security critical)
- [ ] Clear, user-friendly validation error messages for unsupported constructs
- [ ] Extraction logic is pure (no side effects, no I/O) — easily unit-testable

---

## User Flow

### Happy Path

1. User pastes Pine Script into the Custom Indicator editor (see PBI: Custom Indicator CRUD)
2. System calls the pattern extractor
3. Extractor parses the script and returns:
   - 3 indicators: EMA(close, 9), EMA(close, 50), RSI(close, 14)
   - 2 plots: EMA 9 (blue line, overlay), EMA 50 (red line, overlay)
   - 1 alert: "Buy Signal" triggered when `ta.crossover(ema9, ema50) and rsi < 30`
   - 1 input: `rsiThreshold` (int, default 30, min 10, max 90)
4. UI shows extracted configuration with green validation status
5. User can adjust input parameters before saving

### Partially Supported Script

1. User pastes a script that uses `ta.ema()` (supported) and `for` loop (unsupported)
2. Extractor returns:
   - Extracted indicators for the supported `ta.*` calls
   - `unsupportedConstructs: [{ line: 12, construct: "for loop", description: "Custom loop calculations are not supported" }]`
   - `warnings: ["This script contains unsupported constructs. Extracted indicators may not fully match the original script's behavior."]`
3. UI shows amber warning with the list of unsupported constructs
4. User decides whether the extracted subset is sufficient

### Fully Unsupported Script

1. User pastes a script that is entirely custom math with no `ta.*` calls
2. Extractor returns `isValid: false` with explanation
3. UI shows red validation error: "This script uses no standard indicator functions. Custom calculations are not currently supported."

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Empty input | Return validation error "No Pine Script provided" |
| Not Pine Script (random text) | Return validation error "Could not detect Pine Script version header" |
| Pine Script v3 or earlier | Return warning "Pine Script v3 is not supported. Please update to v5" |
| Syntax errors in the pasted script | Best-effort extraction of what can be parsed; report unparseable sections as warnings |
| Script exceeds 500 lines | Return warning "Large scripts may have reduced extraction accuracy" but still attempt |

---

## Technical Considerations

### Bounded Context

**Context:** Application layer — new `PineScript` namespace. The extractor is a pure function with no domain dependencies.

### New Components

#### Backend (C#)

| Component | Layer | Action |
|-----------|-------|--------|
| `PineScriptExtractor` | Application/PineScript/Services | **New** — Static class with `Extract(string pineSource)` method returning `ExtractionResult` |
| `ExtractionResult` | Application/PineScript/Models | **New** — `{ IsValid, Indicators[], Plots[], Alerts[], Inputs[], UnsupportedConstructs[], Warnings[] }` |
| `ExtractedIndicator` | Application/PineScript/Models | **New** — `{ Function (e.g., "ta.ema"), Source, Parameters (period, etc.), VariableName }` |
| `ExtractedPlot` | Application/PineScript/Models | **New** — `{ VariableName, Title, Color, LineWidth, Style, IsOverlay, PaneIndex }` |
| `ExtractedAlert` | Application/PineScript/Models | **New** — `{ Title, Message, Condition (structured expression tree) }` |
| `ExtractedInput` | Application/PineScript/Models | **New** — `{ Name, Type, DefaultValue, MinValue, MaxValue, Options[] }` |
| `ConditionExpression` | Application/PineScript/Models | **New** — Simple expression tree: `{ Type (Comparison/Logical/FunctionCall/VariableRef), Left, Right, Operator, FunctionName, Arguments }` |

#### Frontend (TypeScript)

No frontend components in this PBI — the extractor runs server-side. Frontend integration is in PBI: Custom Indicator CRUD.

### Implementation Approach

The extractor is **not** a full parser. It uses a combination of:

1. **Line-by-line regex scanning** for top-level constructs (`//@version`, `indicator(...)`, `input.*(...)`, `plot(...)`, `alertcondition(...)`)
2. **Simple expression tokenizer** for condition expressions in `alertcondition()` and `plotshape()`
3. **Variable tracking dictionary** to resolve what indicator each variable refers to
4. **`ta.*` function registry** mapping function names to indicator types and expected parameter signatures

This avoids the need for a full recursive-descent parser while handling the flat structure of most indicator scripts.

### Supported `ta.*` Function Registry

| Pine Function | Parameters | Maps To |
|---------------|-----------|---------|
| `ta.sma(source, length)` | source, period | SMA |
| `ta.ema(source, length)` | source, period | EMA |
| `ta.wma(source, length)` | source, period | WMA |
| `ta.vwma(source, length)` | source, period | VWMA |
| `ta.rsi(source, length)` | source, period | RSI |
| `ta.macd(source, fast, slow, signal)` | source, 3 periods | MACD (line, signal, histogram) |
| `ta.atr(length)` | period | ATR |
| `ta.bb(source, length, mult)` | source, period, stddev | Bollinger Bands (upper, middle, lower) |
| `ta.stoch(close, high, low, length)` | 3 sources, period | Stochastic |
| `ta.cci(source, length)` | source, period | CCI |
| `ta.mfi(source, length)` | source, period | MFI |
| `ta.obv` | none | OBV |
| `ta.crossover(a, b)` | 2 series | Crossover event (boolean) |
| `ta.crossunder(a, b)` | 2 series | Crossunder event (boolean) |
| `ta.highest(source, length)` | source, period | Highest value |
| `ta.lowest(source, length)` | source, period | Lowest value |
| `ta.change(source, length)` | source, period | Change |
| `ta.roc(source, length)` | source, period | Rate of Change |

---

## Dependencies

- None — this PBI is the foundation with no external dependencies

---

## Out of Scope

- Execution of extracted indicators (see PBI: Indicator Computation Pipeline)
- Persistence of extracted configuration (see PBI: Custom Indicator CRUD)
- Chart rendering (see PBI: Chart Indicator Overlay Rendering)
- Alert-to-signal mapping (see PBI: Alert Condition → Signal Mapping)
- `request.security()` multi-timeframe support
- `strategy.*()` order/position functions
- Drawing functions (`line.new`, `label.new`, `box.new`)
- Pine Script v6 `method` declarations
- Full Pine Script execution fallback via PineTS (future epic upgrade)

---

## Acceptance Criteria

- [ ] Extractor correctly parses `//@version=5` and `//@version=4` headers
- [ ] Extractor extracts `indicator()` metadata: name, overlay, shorttitle
- [ ] Extractor extracts all supported `ta.*` function calls with correct parameters
- [ ] Extractor resolves variable assignments (e.g., `fast = ta.ema(close, 9)` → variable `fast` maps to EMA indicator)
- [ ] Extractor extracts `input.*()` calls with name, type, default, min, max
- [ ] Extractor extracts `plot()` calls with title, color, style, overlay/pane info
- [ ] Extractor extracts `alertcondition()` with parsed condition expression tree
- [ ] Extractor extracts `plotshape()` with parsed condition and visual config
- [ ] Extractor identifies and reports unsupported constructs (loops, custom functions, arrays, MTF)
- [ ] Extractor returns clear validation errors for empty input, non-Pine text, unsupported versions
- [ ] Extraction of a 100-line script completes in < 50ms
- [ ] All extraction logic is covered by unit tests with known Pine Script samples
- [ ] No user code is ever executed — extraction is pure static analysis
- [ ] Given a typical TradingView EMA crossover script, extractor produces correct `ExtractionResult` matching expected indicators, plots, and alerts

### Release Notes Information

- **Heading**: Pine Script Indicator Import — Pattern Extractor
- **Release note type**: Feature (internal foundation)
- **Release Note Summary**: Foundation for Pine Script indicator import — parses TradingView Pine Script source and extracts indicator functions, parameters, plot configuration, and alert conditions without executing user code.
- **Release Notes Audience**: Technical
- **Breaking Change**: No
