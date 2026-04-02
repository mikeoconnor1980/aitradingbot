# Architecture Review & Known Risks

Assessment of the current system architecture, documenting design strengths and known operational risks that should be addressed as the system matures.

---

## Architecture Strengths

| Strength | Detail |
|----------|--------|
| Deterministic execution | Strategies execute only on confirmed candle closes via `CandleClock → StrategyScheduler`. Eliminates partial-candle bugs and ensures backtest/live parity. |
| Backtest/live code reuse | `StrategyEngine`, `GridController`, and `RiskEngine` are shared between live trading and backtesting. `CandleReplayEngine` feeds historical candles through the same `CandleClock → StrategyScheduler` path. |
| Signal contracts as boundary | Strategies emit intent (`DeployGrid`, `TakeProfit`, `OpenHedge`) rather than raw orders. `RiskEngine` is a mandatory gate — strategies cannot bypass risk checks. |
| Clean DDD layering | Domain → Application → Infrastructure/Persistence follows standard boundaries. Application owns repository contracts; Infrastructure and Persistence implement them. No dependency inversions leaking. |
| Multi-tenant by design | All trading entities scoped by `UserId`. `Candle` data is shared (correct — market data is not tenant-specific). |
| Phased deployment | Same application architecture works on VPS (SQLite + Docker Compose) and Azure (Azure SQL + Container Apps). Infrastructure changes, application logic doesn't. |

---

## Known Risks & Mitigations

### 1. SQLite Concurrent Write Contention

**Risk:** SQLite supports concurrent readers but only one writer at a time. API host (candle ingestion, user requests) and Worker host (order writes, state updates) share the same database file. Under load, `SQLITE_BUSY` errors will occur.

**Severity:** Medium (POC) → High (multi-user)

**Mitigations:**
- Enable WAL (Write-Ahead Logging) mode if not already active
- Accept as a POC limitation; Phase 2 migration to Azure SQL resolves this
- If multi-user POC is needed before Phase 2, consider splitting to separate SQLite files (market data vs trading data)

---

### 2. MarketDataStreamService in API Host

**Risk:** `MarketDataStreamService` (WebSocket → SignalR relay) runs as a `BackgroundService` inside `TradingApp.Api`. API host restarts (deploys, crashes, recycling) will drop the WebSocket stream during active trading.

**Severity:** Medium

**Mitigations:**
- Already acknowledged in knowledge docs as a planned migration to `TradingApp.Worker`
- Production deployment should use a Redis backplane for cross-process SignalR
- Priority: move before any real-money trading

---

### 3. LLM Latency on Critical Path

**Risk:** `LlmContextProvider` sits in the synchronous pipeline: `MarketContextBuilder → LlmContextProvider → StrategyEngine`. A slow or unavailable LLM call blocks strategy evaluation for the current candle close.

**Severity:** Medium

**Mitigations:**
- Cache LLM context with a TTL (e.g., refresh every N minutes, not per candle)
- Pre-fetch context asynchronously between candle closes
- Fall back to a neutral/default `LlmContext` if the call times out
- Consider making LLM context a best-effort enrichment rather than a blocking dependency

---

### 4. No Event Sourcing / Decision Audit Trail

**Risk:** When a signal is emitted, the full `MarketContext` (indicators, LLM context, grid state) that led to that decision is not persisted. Debugging why a trade was taken requires reconstructing context from candle data after the fact.

**Severity:** Medium (debugging difficulty increases with subscriber count)

**Mitigations:**
- Persist `MarketContext` snapshots alongside emitted signals
- At minimum, log a structured JSON snapshot of the decision context per signal
- Full event sourcing is overkill for POC; snapshot-per-signal is the pragmatic middle ground

---

### 5. Per-User Key Security in Phase 1

**Risk:** Subscriber Hyperliquid private keys are encrypted at rest in SQLite on the same VPS where the encryption key resides. A VPS compromise exposes both the encrypted data and the decryption key.

**Severity:** High (financial keys)

**Mitigations:**
- Phase 2's Azure Key Vault approach properly separates key storage
- For Phase 1: consider storing the encryption master key in a separate location (e.g., environment variable loaded from a restricted file, not in the database or application config)
- Limit Phase 1 to personal use / trusted testers who understand the risk
- Document the threat model clearly for any early subscribers

---

### 6. Worker Fan-Out Concurrency Model

**Risk:** The Worker loads all active strategies for all subscribers on each candle close. The concurrency model (sequential vs parallel, bounded vs unbounded) is not defined. With N subscribers on overlapping timeframes, execution order and timing guarantees matter.

**Severity:** Low (POC, single user) → High (production, N subscribers)

**Mitigations:**
- Define explicit concurrency: parallel with bounded semaphore (e.g., `SemaphoreSlim` with configurable max)
- Ensure per-subscriber isolation (one subscriber's error doesn't block others)
- Add per-subscriber execution timing metrics for observability
- Document expected subscriber capacity per Worker instance

---

## Priority Order

For progression from POC to production:

1. **WAL mode for SQLite** — quick win, reduces write contention immediately
2. **Move MarketDataStreamService to Worker** — required before real-money trading
3. **Decision audit snapshots** — persist `MarketContext` per signal for debuggability
4. **LLM caching/async refresh** — decouple LLM latency from candle evaluation
5. **Worker fan-out concurrency model** — define before onboarding multiple subscribers
6. **Key security hardening** — master key separation in Phase 1; Key Vault in Phase 2
