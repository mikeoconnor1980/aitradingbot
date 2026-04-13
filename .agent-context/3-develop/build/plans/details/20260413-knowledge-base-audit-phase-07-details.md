<!-- markdownlint-disable-file -->

# Task Details: Knowledge Base Audit & Refresh

## Phase 7: Diagrams, README & New Knowledge Files

## Standards and Knowledge References

- `.github/instructions/agent-knowledge.instructions.md` — documentation standards
- New knowledge files must be added to `README.md` index

### Task 7.1: Update `diagrams/high-level-architecture.md` {#task-71-update-high-level-architecture}

Reflect the split API + Worker architecture.

- **Complexity**: Medium
- **Risk Factors**: Diagram must accurately represent two-process architecture
- **Files**:
  - `.agent-context/0-knowledge/diagrams/high-level-architecture.md` — update
- **Success**:
  - Monolith box split into API (control plane) + Worker (execution agent)
  - SignalR path shown between API and Worker/browser
  - Store names updated to match actual code entities
  - Heartbeat/command protocol shown

#### Changes Required

1. **Split architecture**: Replace single "AITradingBot Application" box with two deployable processes:
   - `TradingApp.Api` — Control Plane (cloud-hosted, Azure Container App)
   - `TradingApp.Worker` — Execution Agent (client-side Windows Service)

2. **Add communication paths**:
   - Browser ↔ API: REST + SignalR
   - Worker → API: Heartbeat polling (5s interval)
   - API → Worker: Commands via heartbeat response
   - Worker → Hyperliquid: WebSocket (market data) + REST (orders)
   - (Optional) Worker → API → Browser: Azure SignalR (real-time updates)

3. **Update store names**: `STATE → GridCycle`, `ORD → LiveOrder/LiveFill`, `MDH → Candle/FundingRate`, `DEC → BacktestRun`

4. **Keep accurate elements**: Exchange Connector as separate from main app, AI/LLM as optional (dashed), Test/Simulation correctly mapped.

---

### Task 7.2: Update `diagrams/trading-cycle-sequence.md` {#task-72-update-trading-cycle-sequence}

Fix component names and add control path.

- **Complexity**: Medium
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/diagrams/trading-cycle-sequence.md` — update
- **Success**:
  - Component names match code
  - AgentCheckInService → TradingSession control path shown
  - SignalController mediating role documented

#### Changes Required

1. **Fix participant names**:
   - "Bot Orchestrator" → `TradingSession`
   - "Market Data Service" → `HyperliquidWebSocketClient` + `CandleBuilder`
   - "State & Orders Store" → `TradingAppDbContext` (with `LiveOrder`/`LiveFill`/`GridCycle`)

2. **Add `AgentCheckInService`**: Show how start/stop commands flow from API to Worker via heartbeat protocol.

3. **Add `SignalController`**: Show mediating role between `GridController`/`SignalController` signals and `ExecutionEngine`.

4. **Keep accurate elements**: LLM enrichment path (optional), overall flow direction.

---

### Task 7.3: Create `35-strategy-optimizer.md` {#task-73-create-strategy-optimizer-knowledge}

New knowledge file for the Strategy Optimizer feature.

- **Complexity**: Medium
- **Risk Factors**: None — documenting existing feature
- **Files**:
  - `.agent-context/0-knowledge/35-strategy-optimizer.md` — create new
- **Success**:
  - Optimizer architecture documented (sweep + evolutionary)
  - Key components listed with file paths
  - Walk-forward OOS validation explained
  - Fitness scoring metrics documented
  - OptimizationRun/OptimizationResult entities documented
  - API endpoints documented
  - Frontend page documented

#### Content Outline

```markdown
# Strategy Optimizer

The Strategy Optimizer enables automated parameter search across strategy configurations,
using both grid sweep and evolutionary algorithms with walk-forward out-of-sample validation.

## Architecture

- `SweepRunner` — grid sweep across parameter combinations
- `EvolutionaryRunner` — population-based evolutionary search
- `FitnessScorer` — ranks results by Sharpe/Sortino/Calmar/Kelly metrics
- `StrategyConfigGenerator` — generates candidate configs from parameter ranges
- `OptimizationJobQueue` + `OptimizationCancellationRegistry` — async job management
- `OptimizationProcessorService` — hosted service processing optimization jobs

## Key Components

| Component | Location | Purpose |
|---|---|---|
| `SweepRunner` | `Application/Optimization/` | Grid parameter sweep engine |
| `EvolutionaryRunner` | `Application/Optimization/` | Evolutionary search with selection/crossover/mutation |
| `FitnessScorer` | `Application/Optimization/` | Multi-metric ranking (Sharpe, Sortino, Calmar, Kelly) |
| `StrategyConfigGenerator` | `Application/Optimization/` | Generates candidate configs from parameter ranges |
| `OptimizationJobQueue` | `Application/Optimization/` | Thread-safe job queue |
| `OptimizationCancellationRegistry` | `Application/Optimization/` | Per-job cancellation tokens |
| `OptimizationProcessorService` | `Api/Services/` | Hosted service processing jobs |
| `RunOptimizationCommand` | `Application/Optimization/` | MediatR command to start optimization |
| `CancelOptimizationCommand` | `Application/Optimization/` | MediatR command to cancel |
| `OptimizationsController` | `Api/Controllers/` | REST endpoints |
| `OptimizerPageComponent` | `frontend/.../optimizer/` | Angular UI |

## Entities

| Entity | Purpose |
|---|---|
| `OptimizationRun` | Tracks a parameter sweep/evolutionary run |
| `OptimizationResult` | Individual result with IS/OOS metrics |

## Walk-Forward Validation

The optimizer splits the date range into in-sample (IS) and out-of-sample (OOS) periods.
Candidates are ranked by IS fitness, then the top candidates are validated against OOS data
to detect overfitting.

## Fitness Metrics

Sharpe Ratio, Sortino Ratio, Calmar Ratio, Kelly Criterion, Half Kelly, Win/Loss Ratio,
Profit Factor, Max Drawdown, Total PnL.

## Future Recommendations

- Multi-objective optimization (Pareto frontier)
- Monte Carlo simulation for robustness testing
- Regime-aware optimization (different params per regime)
- Portfolio-level optimization across multiple strategies
```

---

### Task 7.4: Mark `21-business-model-options-legal.md` as placeholder {#task-74-mark-legal-placeholder}

Add clear TODO status to the empty file.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/21-business-model-options-legal.md` — update
- **Success**:
  - File clearly marked as placeholder with TODO items

#### Changes Required

Replace "testing" content with:

```markdown
# Business Model — Legal Considerations

> **STATUS: PLACEHOLDER** — This document requires legal review before launch.

## TODO

- [ ] Regulatory classification of signal provision vs. trade execution
- [ ] Terms of service for subscription model
- [ ] Data privacy (GDPR, CCPA) for user wallet addresses
- [ ] Liability limitations for trading losses
- [ ] Financial services licensing requirements by jurisdiction
- [ ] API abuse / rate limiting policy
```

---

### Task 7.5: Update `README.md` {#task-75-update-readme}

Update the table of contents for all changes.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/README.md` — update
- **Success**:
  - `35-strategy-optimizer.md` entry added
  - Descriptions updated to reflect content changes
  - All existing entries still valid

#### Changes Required

1. **Add to Strategy & Execution section**:

| # | Document | Description |
|---|---|---|
| 35 | [Strategy Optimizer](35-strategy-optimizer.md) | Parameter sweep, evolutionary search, walk-forward OOS validation, fitness metrics |

2. **Add to Risk Management section** (or create section if needed):

| # | Document | Description |
|---|---|---|
| 33 | [Risk Management & Trade Sizing](33-risk-management-and-trade-sizing.md) | Position sizing modes, drawdown tiers, portfolio heat, risk engine |

3. **Review all existing descriptions** for accuracy after updates. Key description changes:
   - 04: Add "GridCycle, LiveOrder, LiveFill" to description
   - 10: Add "including post-build ADRs" to description
   - 20: Change to "Options A/B/C — **Option C chosen**"

4. **Add missing README entries** for files updated by this plan but not currently indexed:

| # | Document | Description |
|---|---|---|
| 24 | [Backtesting Grid Engine Explained](24-backtesting-grid-engine-explained.md) | How GridStrategyEngine, RiskEngine, and GridController interact during backtesting |
| 28 | [Macro Calendar](28-macro-calendar.md) | Economic calendar integration for trade-blocking during high-impact events |
| 34 | [Google SSO Authentication](34-google-sso-authentication.md) | Google OAuth integration via Google Identity Services |

## Phase Success Criteria

- Architecture diagrams reflect the split API + Worker model
- New optimizer knowledge file created and indexed
- Legal placeholder has clear TODO items
- README index is complete and accurate
- All knowledge files have been addressed by the plan
