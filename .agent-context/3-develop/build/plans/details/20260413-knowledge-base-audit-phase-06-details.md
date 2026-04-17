<!-- markdownlint-disable-file -->

# Task Details: Knowledge Base Audit & Refresh

## Phase 6: Operations, Business & Control Plane (05, 08, 20, 21-azure, 22, 26, 28, 29, 30, 31, 34-google-sso)

## Standards and Knowledge References

- `.github/instructions/agent-knowledge.instructions.md` — documentation standards
- Security: Remove any plaintext credentials from documentation

### Task 6.1: Update `05-feature-specification.md` {#task-61-update-feature-specification}

Align feature spec with implemented vs planned features.

- **Complexity**: Medium
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/05-feature-specification.md` — update
- **Success**:
  - Implemented features correctly listed (optimizer, interpreter, wizard, help, Google SSO)
  - Admin/billing clearly marked as NOT IMPLEMENTED
  - Subscription status updated (free-only, no Stripe)

#### Changes Required

1. **Add implemented features**:
   - Strategy Optimizer (parameter sweep, evolutionary, walk-forward)
   - NLP Strategy Interpreter (text → StrategyConfig via LLM)
   - Strategy Wizard (7-step guided creation)
   - Strategy AI Review
   - Help/Tutorial system
   - Google SSO authentication
   - Agents page (start/stop trading, kill-switch)
   - Macro Calendar

2. **Mark NOT IMPLEMENTED**:
   - Admin dashboard (no admin controllers/views)
   - Per-user bot status / revenue metrics / error monitoring for admin
   - Full subscription management (plan choice, upgrade/downgrade, billing history, cancel)
   - Stripe or any payment integration

3. **Update subscription**: Only `POST /api/subscriptions/free` exists — creates 30-day free subscription. No paid plans.

4. **Add Future Recommendations**: Admin panel, Stripe integration, paid tiers

---

### Task 6.2: Update `08-development-plan.md` {#task-62-update-development-plan}

Mark completed phases and note additional deliverables.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/08-development-plan.md` — update
- **Success**:
  - Each phase marked with completion status
  - Phase 6 marked partial (kill switch yes, auto circuit breaker no)
  - Beyond-plan deliverables noted

#### Changes Required

1. **Phase 1 — Solution Foundation**: ✅ Complete
2. **Phase 2 — Historical Data**: ✅ Complete
3. **Phase 3 — Deterministic Backtester**: ✅ Complete (including audit log, R-multiples, Kelly, SQN)
4. **Phase 4 — Paper Trading**: ⚠️ Partial — architecture exists but no explicit paper-trading mode toggle
5. **Phase 5 — Exchange Integration**: ✅ Complete
6. **Phase 6 — Safety Controls**: ⚠️ Partial — kill switch ✅, `LiveRiskEngine` ✅; missing: auto circuit breaker, emergency flatten, stuck-order detection
7. **Phase 7 — Controlled Live Rollout**: ✅ Complete — Worker is shippable Windows Service with InnoSetup installer
8. **Phase 8 — Minimal API + UI**: ✅ Complete and significantly exceeded scope

**Beyond-plan deliverables**: Strategy Optimizer, NLP Strategy Interpreter, Help/Tutorial, Update Checker, Agents page, Strategy Wizard, AI Strategy Review, Macro Calendar

---

### Task 6.3: Update `20-business-model-options.md` {#task-63-update-business-model-options}

Record the chosen business model decision.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/20-business-model-options.md` — update
- **Success**:
  - Option C clearly marked as CHOSEN
  - Implementation evidence cited

#### Changes Required

1. **Add decision section** at the top or in a clear "Decision" heading:

> **Decision: Option C (Split Architecture) — CHOSEN**
>
> Evidence:
> - Worker is `TradePilot.ExecutionAgent` (Windows Service, `SelfContained=true`, `RuntimeIdentifier=win-x64`)
> - API control plane comment: "The control plane does not hold private keys. Wallet addresses are stored in the database; private keys live only on the execution agent (Worker)."
> - Worker `appsettings.json` has `Agent.ControlPlaneUrl` pointing at the API
> - `AgentController` + `AgentCommandStore` implement the heartbeat/command protocol
> - InnoSetup installer in `deploy/worker/`

---

### Task 6.4: Update `21-azure-deployment-infrastructure.md` {#task-64-update-azure-deployment}

Fix .NET version and add Worker pipeline.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/21-azure-deployment-infrastructure.md` — update
- **Success**:
  - .NET 10 correctly referenced (not .NET 8)
  - Worker installer pipeline mentioned
  - GITHUB_TOKEN vs GHCR_PAT distinction noted

#### Changes Required

1. **Fix .NET version**: .NET 10 (build uses `10.0.x`), not .NET 8.
2. **Add Worker pipeline**: `deploy/worker/build-installer.ps1` builds InnoSetup installer for the execution agent.
3. **Add GITHUB_TOKEN note**: Pipeline push uses `GITHUB_TOKEN`; Container App registry access uses `GHCR_PAT`.

---

### Task 6.5: Update `22-prelaunch-checklist.md` {#task-65-update-prelaunch-checklist}

Show completion status for each checklist item.

- **Complexity**: Medium
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/22-prelaunch-checklist.md` — update
- **Success**:
  - Completed items marked with ✅
  - Remaining gaps clearly identified

#### Changes Required

Mark each item with its status. **Completed** items include: API keys never stored as private keys, JWT auth, role-based access, rate limiting, tenant isolation, candle-close execution, order idempotency, RiskEngine enforcement, persisted order journal, startup reconciliation, kill switch, backtest code reuse, no live API in backtest, WebSocket reconnection, Bicep infrastructure.

**NOT YET IMPLEMENTED**: Emergency flatten endpoint, automatic circuit breaker (consecutive losses, daily loss, repeated rejections), withdrawal permission validation, admin UI for kill switch, stuck-order detection/timeout, `StrategyExecutionCheckpoint` as DB entity.

---

### Task 6.6: Update `26-architecture-review.md` {#task-66-update-architecture-review}

Update risk statuses with current mitigation state.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/26-architecture-review.md` — update
- **Success**:
  - Risk 5 (key security) marked as resolved by Option C
  - Risk 2 (MarketDataStreamService) marked as partially resolved
  - Risk 3 (LLM latency) updated with SyntheticRegimeProvider fallback
  - Risk 6 (fan-out) updated with single-tenant agent model context

#### Changes Required

| Risk # | Update |
|---|---|
| 1 (SQLite contention) | Still applies |
| 2 (MarketDataStreamService in API) | Partially resolved — exists in both API and Worker now |
| 3 (LLM latency) | `SyntheticRegimeProvider` exists as always-active fallback |
| 4 (No event sourcing) | Still valid for live; backtest has detailed audit log |
| 5 (Per-user key security) | **Resolved by Option C** — keys never in API |
| 6 (Worker fan-out) | Single-tenant agent model — one user per Worker deployment |

Add mention of: `AgentCommandStore` control mechanism, Windows Service delivery model, execution agent architecture.

---

### Task 6.7: Update `28-macro-calendar.md` {#task-67-update-macro-calendar}

Add risk check integration note.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/28-macro-calendar.md` — update
- **Success**:
  - `MacroEventRiskCheck` integration into live trading documented

#### Changes Required

1. Add note that `MacroEventRiskCheck` in `TradePilot.Persistence.Services` integrates into the live trading `LiveRiskEngine` pipeline, blocking order placement during high-importance event windows.

---

### Task 6.8: Update `29-control-plane-agent-architecture.md` {#task-68-update-control-plane-architecture}

Add auto-update fields and UpdateCheckerService.

- **Complexity**: Medium
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/29-control-plane-agent-architecture.md` — update
- **Success**:
  - HeartbeatResponse update fields added
  - AgentHeartbeat version/update fields added
  - UpdateCheckerService documented
  - Session creation description corrected

#### Changes Required

1. **Add `HeartbeatResponse` fields**: `UpdateAvailable` (bool), `LatestVersion` (string?), `UpdateDownloadUrl` (string?), `UpdateSha256Hash` (string?) — auto-update system.

2. **Add `AgentHeartbeat` fields**: `AgentVersion` (string), `TimestampUtc` (DateTimeOffset), `UpdateState` (`UpdateState` enum), `UpdateDeferredReason` (string?).

3. **Add `UpdateCheckerService`**: Full auto-update `BackgroundService` that receives update notifications from heartbeat, checks if safe to apply (no active trading session), and runs InnoSetup installer silently.

4. **Fix session creation**: Uses DI constructor injection, not manual factory. `GridState` is optional constructor parameter (defaults to `new GridState()`).

5. **Add conditional service note**: `MarketDataStreamService` and `UserEventStreamService` only start when `Azure:SignalR:ConnectionString` is configured.

---

### Task 6.9: Update `30-worker-execution-pipeline.md` {#task-69-update-worker-execution-pipeline}

Add missing services and components.

- **Complexity**: Medium
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/30-worker-execution-pipeline.md` — update
- **Success**:
  - BackgroundService table corrected (conditional + UpdateCheckerService)
  - ITriggerOrderManager documented
  - DrawdownTiers documented
  - Session creation description corrected

#### Changes Required

1. **Fix BackgroundService table**: `MarketDataStreamService` and `UserEventStreamService` are conditional (Azure SignalR required). Add `UpdateCheckerService` (always runs). In non-Azure: only 3 services (`HealthMonitorService`, `AgentCheckInService`, `UpdateCheckerService`).

2. **Add `ITriggerOrderManager`**: On `StopAsync`, if `GridState.ProtectionOrders.HasAny`, cancels all exchange-native protection orders before `CancelAllOrdersAsync`.

3. **Add `DrawdownTiers`**: `IOptions<RiskLimitsConfig>` passed to `TradingSession`, forwarded to `StrategyScheduler` for adaptive drawdown gating.

4. **Fix session creation**: DI constructor injection, not manual wiring. `GridState` as optional constructor parameter.

5. **Add `UserEventStreamService` distinction**: Worker has its own `UserEventStreamService` that broadcasts to Angular dashboard via Azure SignalR — distinct from `HyperliquidUserEventClient` in `TradingSession`.

---

### Task 6.10: Update `31-atr-calculation.md` {#task-610-update-atr-calculation}

Minimal update — highest fidelity file.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/31-atr-calculation.md` — review and leave mostly as-is
- **Success**:
  - Verified accurate — minimal changes if any

#### Changes Required

This file has the highest fidelity of all knowledge files. Review for any minor corrections. The ATR default multipliers (`AtrTrailing` = 3m, `AtrInitial` = 2m) are already documented correctly. Only add a Future Recommendations section if there are relevant items (e.g., configurable ATR period).

---

### Task 6.11: Fix `34-google-sso-authentication.md` {#task-611-fix-google-sso-authentication}

**SECURITY**: Remove plaintext credentials and add minor updates.

- **Complexity**: Low
- **Risk Factors**: **HIGH** — live Client Secret in plaintext in documentation
- **Files**:
  - `.agent-context/0-knowledge/34-google-sso-authentication.md` — update
- **Success**:
  - Client Secret REMOVED from document
  - GoogleAuthOptions config binding documented
  - Picture field noted

#### Changes Required

1. **🔴 SECURITY FIX**: Remove the Google OAuth Client Secret (`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`) from the document. Replace with `<stored in environment/secrets>`. The Client ID can optionally remain (it's public) but the secret MUST be removed.

2. **Add `GoogleAuthOptions` config binding**: `builder.Services.AddOptions<GoogleAuthOptions>()` binding from `Google:ClientId` section in appsettings.

3. **Add `Picture` field**: `GoogleUserInfo` record has 4th field `Picture` (profile photo URL).

4. **Add `GetByExternalProviderAsync`**: Note this is a new `IUserRepository` method.

## Phase Success Criteria

- No plaintext credentials remain in any knowledge file
- All checklist items have clear completion status
- Business model decision is recorded
- Development plan phases have accurate completion status
- Control plane and worker docs reflect auto-update system
- Architecture review risks have current mitigation status
