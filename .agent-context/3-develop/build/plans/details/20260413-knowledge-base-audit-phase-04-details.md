<!-- markdownlint-disable-file -->

# Task Details: Knowledge Base Audit & Refresh

## Phase 4: Exchange, AI & Backtesting (02, 17, 18, 24-backtesting, 24-interpreter, 33)

## Standards and Knowledge References

- `.github/instructions/agent-knowledge.instructions.md` — documentation standards
- Cross-reference related knowledge docs where relevant

### Task 4.1: Update `02-hyperliquid-integration.md` {#task-41-update-hyperliquid-integration}

Fix component locations, signer pattern, and add new components.

- **Complexity**: High
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/02-hyperliquid-integration.md` — update
- **Success**:
  - `IHyperliquidAccountService` / `HyperliquidAccountService` paths corrected (moved to Application/Infrastructure)
  - `ISignerProvider` / `MutableSignerProvider` pattern documented (runtime-swappable, not config-loaded)
  - Retry policy updated (30s outer + Polly 5-attempt exponential backoff)
  - `HyperliquidAssetMapper` behavior corrected (lenient stripping, no throw)
  - New components added: UserEventClient, SignerProvider, AssetMetadataCache, ExecutionEngines, NetworkRouting
  - Future Recommendations section added

#### Changes Required

1. **Fix `IHyperliquidAccountService` location**: Moved to `src/TradePilot.Application/Abstractions/Services/IHyperliquidAccountService.cs`

2. **Fix `HyperliquidAccountService` location**: Moved to `src/TradePilot.Infrastructure/Services/HyperliquidAccountService.cs`

3. **Fix `IHyperliquidSigner` interface**: `SignTypedData` exists only on concrete `HyperliquidSigner`; interface exposes only `SignHash(byte[])` and `WalletAddress`.

4. **Fix signer registration**: API layer now uses `MutableSignerProvider` (implements `ISignerProvider : IHyperliquidSigner`). Key is NOT loaded from config at startup — configured at runtime via Profile UI or env var. `Program.cs` logs warning if not configured.

5. **Fix retry policy**: Now 30-second outer `HttpClient.Timeout` + 5-second per-attempt Polly timeout, with up to 5 exponential backoff retries (1s–60s). Handles 429 and 5xx.

6. **Fix `HyperliquidAssetMapper.ToCoin`**: Now leniently strips `-PERP`/`-USD` suffix or returns input as-is — no exception. `NotFoundException` only from `HyperliquidAssetMetadataCache.GetAsync`. Add `IsValidTimeframe`, `IsValidCoin`, `GetSupportedCoins`, `GetSupportedTimeframes` methods.

7. **Add new components table**:

| Component | Location | Purpose |
|---|---|---|
| `IHyperliquidUserEventClient` | `Application/Abstractions/Services/` | Per-wallet WebSocket for fills and order updates |
| `HyperliquidUserEventClient` | `Infrastructure/Services/` | Implementation with `SubscribeToUserEventsAsync`, `OnFillReceived`, `OnOrderUpdateReceived` |
| `ISignerProvider` | `Application/Abstractions/Services/` | Extends `IHyperliquidSigner` with `IsConfigured`, `Configure(key)`, `Clear()` |
| `MutableSignerProvider` | `Infrastructure/Services/` | Thread-safe, runtime-swappable signer |
| `IHyperliquidAssetMetadataCache` | `Api/Services/` | Lazy-loaded, 30-min TTL cache of `AssetMetadata(Index, SzDecimals, MaxLeverage)` |
| `HyperliquidExecutionEngine` | `Api/Services/` | API-side `IExecutionEngine` wrapping order service |
| `LiveExecutionEngine` | `Infrastructure/Services/` | Worker-side `IExecutionEngine` with direct signing |
| `INetworkProvider` / `UserNetworkProvider` | `Application/` / `Api/Infrastructure/` | Per-request mainnet/testnet resolution |
| `NetworkRoutingHandler` | `Api/Infrastructure/` | `DelegatingHandler` routing to per-user base URL |
| `GetCandleSnapshotsAsync` | `IHyperliquidRestClient` | Returns `List<CandleSnapshotDto>` with `NumTrades` |
| `GetUserFillsAsync` | `IHyperliquidRestClient` | Supports `userFills` and `userFillsByTime` |

8. **Add Future Recommendations**:
   - Multi-tenant WebSocket connection manager for user events
   - HIP-3 (RWA/stock perp) symbol support in AssetMapper
   - WebSocket reconnection with exponential backoff in consumer layer

---

### Task 4.2: Update `17-llm-context-sentiment-architecture.md` {#task-42-update-llm-context-architecture}

Fix model fields and document the actual LLM client architecture.

- **Complexity**: Medium
- **Risk Factors**: Must accurately represent the three-client pattern
- **Files**:
  - `.agent-context/0-knowledge/17-llm-context-sentiment-architecture.md` — update
- **Success**:
  - `DerivedRegime` field added to `LlmContext`
  - `GeneratedAtUtc` type corrected (`long` Unix ms, not `DateTime`)
  - `MacroRegime` values corrected (string: Bullish/Bearish/Neutral)
  - Three independent LLM clients documented with their config sections
  - Data sources marked as aspirational
  - `SyntheticRegimeProvider` documented as primary regime classifier
  - Conditional registration documented

#### Changes Required

1. **Add `DerivedRegime`**: Type `MarketRegime` enum (`Aggressive`, `Normal`, `Defensive`, `RiskOff`). This is the primary field used by `GridStrategyEngine` for regime gating.

2. **Fix `GeneratedAtUtc`**: Type is `long` (Unix milliseconds), not `DateTime`.

3. **Fix `MacroRegime` values**: String field using `"Bullish"`, `"Bearish"`, `"Neutral"`. It's `DerivedRegime` that maps to `RiskOff`/`Aggressive`/etc.

4. **Document three LLM clients**:

| Client | Interface | Config | Purpose | Temperature |
|---|---|---|---|---|
| `OpenAiCompatibleLlmClient` | `ILlmClient` | `LlmOptions` (section: `Llm`) | Strategy interpretation | — |
| `ReviewLlmClient` | `IReviewLlmClient` | `LlmReviewOptions` (section: `LlmReview`) | Strategy review | 0.4 |
| `LlmContextClient` | `ILlmContextClient` | `LlmContextOptions` (section: `LlmContext`) | Market context/regime | 0.2, JSON response |

5. **Mark data sources as aspirational**: The prompt only receives indicator data (EmaFast, EmaSlow, EmaTrend, Rsi, Atr) and optional macro calendar events. No crypto news, social sentiment, or external data feeds are wired.

6. **Add `SyntheticRegimeProvider`**: Rule-based regime classifier (EMA stack alignment + ATR 24h percentile + RSI). Used in both `BacktestMarketContextBuilder` and `LiveMarketContextBuilder` as the always-active fallback when no LLM is configured. This is the primary regime classifier in practice.

7. **Add conditional registration**: `LlmContextProvider` only registered when `ApiKey` present in config. Otherwise `ILlmContextProvider` resolves to `null` and `SyntheticRegimeProvider` handles regime classification.

8. **Add `MacroEventListItemDto`**: Optional parameter to `LlmContextProvider.GetContextAsync` for passing upcoming calendar events.

9. **Add Future Recommendations**: Crypto news feeds, social sentiment, multi-source aggregation, sentiment trending

---

### Task 4.3: Update `18-backtesting-architecture.md` {#task-43-update-backtesting-architecture}

Add async execution model and update risk engine description.

- **Complexity**: High
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/18-backtesting-architecture.md` — update
- **Success**:
  - `BacktestConfig.TriggerTimeframe` added
  - Missing `BacktestResult` fields added (CandlesReplayed, DrawdownBlockedSignalCount)
  - `BacktestRun` progress fields added (ElapsedMs, Progress, TotalCandles, CandlesReplayed)
  - RiskEngine description updated (NOT passthrough)
  - Async job queue architecture documented
  - BacktestExecutionContextAccessor documented
  - DrawdownTier gating documented

#### Changes Required

1. **Add `BacktestConfig.TriggerTimeframe`**: `string`, default `"15m"`. Backtest can run against any trigger timeframe.

2. **Add `BacktestResult` fields**: `CandlesReplayed` (int), `DrawdownBlockedSignalCount` (int).

3. **Add `BacktestRun` entity fields**: `ElapsedMs` (long), `Progress` (int), `TotalCandles` (int), `CandlesReplayed` (int).

4. **Fix RiskEngine**: NOT a passthrough. `BacktestRiskEngine` enforces portfolio heat blocking AND drawdown-tier adaptive gating (producing `HeatBlockedSignalCount` and `DrawdownBlockedSignalCount`).

5. **Add async job queue**: `BacktestJobQueue` + `BacktestCancellationManager` + `BacktestProcessorService` — backtests queued, processed by hosted service, per-job cancellation.

6. **Add `BacktestExecutionContextAccessor`**: Shared state holding current `SimulatedExecutionEngine` reference and `CurrentTimestampUtc`. Singleton bridging execution engine across pipeline.

7. **Add drawdown-tier gating**: `StrategyScheduler` constructed with `drawdownTiers` from `RiskLimitsConfig`.

8. **Add `IBacktestAuditCollector`/`NullBacktestAuditCollector`**: Null-object pattern when `EnableAuditLog=false`.

9. **Add `onProgress` callback**: Overload of `BacktestRunner.RunAsync` for real-time progress reporting.

10. **Add `ReplayData` overload**: Pre-load candle data once and share across multiple configs (optimizer use case).

11. **Add `BacktestSummaryForReview`/`RegimeSegmentationSummary`**: Summary models for strategy reviewer.

---

### Task 4.4: Update `24-backtesting-grid-engine-explained.md` {#task-44-update-backtesting-grid-engine}

Fix component descriptions that are stale.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/24-backtesting-grid-engine-explained.md` — update
- **Success**:
  - GridStrategyEngine regime gating documented
  - RiskEngine description corrected (not passthrough)
  - DrawdownBlockedSignalCount tracking mentioned
  - ISignalController mentioned in component glossary

#### Changes Required

1. **Fix `GridStrategyEngine`**: No longer "always true when config valid." `SyntheticRegimeProvider` feeds `MarketContext.LlmContext.DerivedRegime`. When `RiskOff`, `SetupDetected = false`.

2. **Fix `RiskEngine`**: Not a passthrough. `BacktestRiskEngine` enforces heat limits and drawdown-tier gating.

3. **Add `DrawdownBlockedSignalCount`**: Second blocking counter alongside `HeatBlockedSignalCount`.

4. **Add `ISignalController`**: Used in backtest pipeline for `Signal` mode strategies — omit from glossary.

---

### Task 4.5: Update `24-strategy-interpreter-architecture.md` {#task-45-update-strategy-interpreter}

Minor update with cross-references.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/24-strategy-interpreter-architecture.md` — update
- **Success**:
  - Default provider details updated
  - Cross-references added to reviewer and context client systems

#### Changes Required

1. **Update provider**: Default is `"Gemini"` with URL `https://generativelanguage.googleapis.com/v1beta/openai/` and model `"gemini-2.0-flash"`.

2. **Add cross-reference to Strategy Reviewer**: `IStrategyReviewer` + `StrategyReviewer` — separate AI review feature. Has own `IReviewLlmClient`, `ReviewLlmClient`, `LlmReviewOptions`, `StrategyReviewPrompt`, `RequestStrategyReviewCommand`.

3. **Add cross-reference to LLM Context**: `LlmContextClient` + `LlmContextProvider` + `MarketContextPrompt` — market context/regime provider.

---

### Task 4.6: Update `33-risk-management-and-trade-sizing.md` {#task-46-update-risk-management}

Update status of features from "proposed" to "implemented."

- **Complexity**: Medium
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/33-risk-management-and-trade-sizing.md` — update
- **Success**:
  - Portfolio heat marked as IMPLEMENTED
  - Drawdown tiers marked as IMPLEMENTED
  - New IRiskEngine properties documented
  - PassThroughRiskEngine documented
  - Future Recommendations for unimplemented items (Kelly/SQN/partial-close)

#### Changes Required

1. **Portfolio heat**: Mark as IMPLEMENTED, not proposed. `MaxPortfolioHeatPercent` in both `RiskLimitsConfig` and `LiveRiskEngine.CheckPortfolioHeat`.

2. **Drawdown tiers**: Mark as IMPLEMENTED. `LiveRiskEngine._drawdownScalingFactor`, `_drawdownCircuitBreakerTripped`, `DrawdownTiers` config.

3. **Add `LiveRiskEngine` properties**: `DrawdownScalingFactor`, `IsDrawdownCircuitBreakerTripped`, `RecordOrdersPlaced`/`RecordOrdersClosed`, `UpdatePortfolioState(decimal equity)`.

4. **Add `PassThroughRiskEngine`**: No-op for non-live contexts.

5. **Add `PositionSizeResolver.ResolveInitialR`**: Computes dollar R value from `RiskConfig` + equity; returns `null` for non-RiskBased modes.

6. **Add Future Recommendations**:
   - Kelly Criterion advisory metric in backtest results
   - Volatility-scaled risk (ATR as input to PositionSizeResolver)
   - Partial-close at R-level milestones (1R, 2R, 3R tranches)
   - R-Multiple trade history persistence (InitialR, RMultipleResult, MFE, MAE)
   - System Quality Number (SQN) metric

## Phase Success Criteria

- All 6 knowledge files accurately describe implemented behavior
- Aspirational/proposed items clearly labeled or moved to Future Recommendations
- LLM architecture correctly shows three independent clients
- Backtesting async execution model documented
