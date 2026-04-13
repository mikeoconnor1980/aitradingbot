<!-- markdownlint-disable-file -->

# Task Details: Knowledge Base Audit & Refresh

## Phase 5: Frontend & UI (07, 09, 11)

## Standards and Knowledge References

- `.github/instructions/agent-knowledge.instructions.md` — documentation standards
- `.agent-context/0-knowledge/11-angular-instructions.md` — Angular coding standards

### Task 5.1: Update `07-ui-design.md` {#task-51-update-ui-design}

Fix dashboard description and add all new UI features.

- **Complexity**: Medium
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/07-ui-design.md` — update
- **Success**:
  - Dashboard description corrected (no chart/signals/bot-state on dashboard)
  - Strategy Wizard page documented
  - AI Review feature documented
  - NL Input feature documented
  - SupportResistance condition documented
  - Auth pages documented
  - All new routes documented
  - Agents page documented

#### Changes Required

1. **Fix Dashboard description**:
   - Remove: chart (it's on `/market-data`), signals widget, bot state display (component exists but is not rendered in dashboard template)
   - Keep: Positions tab, Orders tab, Activity Feed (100-event cap)
   - Add: `MarketContextCardComponent` (polls LLM context every 60s, shows regime/sentiment)

2. **Add Strategy Wizard**: `StrategyWizardPageComponent` at `/strategies/wizard` — `MatStepper`-based 7-step wizard: `WizardGoalStep`, `WizardMarketStep`, `WizardEntryStep`, `WizardExitStep`, `WizardRiskStep`, `WizardFilterStep`, `WizardReviewStep`. Uses `StrategyDraftService` for `sessionStorage` persistence.

3. **Add NL Input**: `NlInputCardComponent` — submits text to `StrategyApiService.interpretIntent()`, emits `StrategyIntentDto`. Paired with `AssumptionsPanelComponent` and `ConfidenceBadgeComponent`.

4. **Add AI Review**: `AiReviewCardComponent` + `AiReviewModalComponent` — displays LLM strategy review with markdown rendering via `marked` library.

5. **Add `SupportResistanceConditionItemComponent`**: 4th entry condition type in the builder.

6. **Fix signal template IDs**: `STRATEGY_TEMPLATES` also contains `rsi_reversal` (available: false) and `blank` (available: true). Builder-page check only handles 3 originals — document the inconsistency.

7. **Add authentication routes**: `/login` (`LoginPageComponent`), `/register` (`RegisterPageComponent`) with Google Sign-In support.

8. **Add all new feature routes**:

| Route | Component | Description |
|---|---|---|
| `/agents` | `AgentsPageComponent` | Start/stop trading, kill-switch dialog, agent status |
| `/candle-data` | `CandleManagementComponent` | Binance data ingestion management |
| `/macro-calendar` | `MacroCalendarComponent` | Economic event calendar |
| `/optimizer` | `OptimizerPageComponent` | Strategy parameter optimization |
| `/profile` | `ProfilePageComponent` | User profile, wallet address |
| `/order-entry` | `OrderEntryComponent` | Manual order placement |
| `/backtesting` | `BacktestPageComponent` | Backtest configuration and results |
| `/connection` | `ConnectionPageComponent` | Exchange connectivity setup |

9. **Add guards**: `authGuard`, `subscriptionGuard` (on strategy/backtest/optimizer/agents/order-entry), `mobileRedirectGuard` (desktop-only routes).

10. **Add other undocumented components**: `InfoPopoverComponent`, `StrategyBacktestHistoryComponent`, `HelpPanelComponent`, `SidebarNavComponent`, `MobileNavComponent`.

11. **Add Future Recommendations**:
    - Grid level overlays on price chart
    - Mobile-responsive dashboard
    - Real-time P&L tracking widget
    - Strategy performance comparison view
    - Trade journal view

---

### Task 5.2: Rewrite `09-charting-library.md` {#task-52-rewrite-charting-library}

Replace line chart description with actual candlestick implementation.

- **Complexity**: Medium
- **Risk Factors**: None — fundamentally wrong documentation
- **Files**:
  - `.agent-context/0-knowledge/09-charting-library.md` — rewrite
- **Success**:
  - Line chart description replaced with candlestick chart
  - 3-pane architecture documented (main, RSI, MACD)
  - 6 indicator toggles documented
  - Fill markers documented
  - loadMoreCandles pagination documented
  - Grid overlay section removed (not implemented)
  - EquityChartComponent accurately described

#### Changes Required

**PriceChartComponent — complete rewrite:**

1. **Main chart pane**: `CandlestickSeries` (not `LineSeries`). Supports timeframes 1m–1d via `@Input() selectedTimeframe`.

2. **Three independent chart panes**: Main candlestick (`_chart`), RSI sub-chart (`_rsiChart`), MACD sub-chart (`_macdChart`).

3. **Indicator toggle system**: 6 toggles: `emaFast`, `emaSlow`, `emaTrend`, `bollinger`, `rsi`, `macd`. User-controllable.

4. **9 additional series**: Fast EMA, Slow EMA, Trend EMA, Bollinger upper/middle/lower, MACD line, signal, histogram (`HistogramSeries`).

5. **`@Input() fills: FillEvent[]`** and `@Input() showTradeMarkers`: Fill events plotted as consolidated marker groups.

6. **`@Output() loadMoreCandles: EventEmitter<number>`**: Emits oldest timestamp when user scrolls left past loaded data. Called from `MarketDataComponent.onLoadMoreCandles()`.

7. **`@Input() selectedAsset`** and `@Input() selectedTimeframe`**: Chart reloads on change.

8. **`timeWindowLabel` getter**: Displays loaded time window (e.g., "48H" / "7D").

9. **`ChartIndicatorValues` model**: Indicator values (EMA, Bollinger, RSI, MACD) arrive from API per candle.

10. **`PRICE_CHART_THEME` and `EQUITY_CHART_THEME`**: Centralised colour theming constants.

11. **No rolling window**: All loaded candles kept; no pruning logic.

12. **Remove grid overlay section**: Grid levels, entry line, hedge line, TP overlay — none implemented.

**EquityChartComponent updates:**

13. **Add `@Input() cycleSummaries: GridCycleSummary[]`**: Not in current doc.

14. **Add Future Recommendations**:
    - Grid level price overlays on candlestick chart
    - Entry/exit markers integrated with grid cycles
    - Volume profile / VWAP overlay
    - Multi-chart layout

---

### Task 5.3: Update `11-angular-instructions.md` {#task-53-update-angular-instructions}

Fix color palette and add missing infrastructure documentation.

- **Complexity**: Medium
- **Risk Factors**: None
- **Files**:
  - `.agent-context/0-knowledge/11-angular-instructions.md` — update
- **Success**:
  - Primary color corrected (cyan `#79cfc3`, not green)
  - Full CSS token table (24 tokens)
  - Guards, interceptors, services documented
  - Core subfolder structure documented
  - CommonModule usage inconsistency noted

#### Changes Required

1. **Fix primary palette**: `$cyan-palette` (cyan/teal, `#79cfc3`), not green. Tertiary is `$orange-palette`.

2. **Expand CSS token table**: Add all 24 tokens from `styles.scss`:
   - Existing 7 + new: `--colour-warning`, `--colour-warning-elevated`, `--colour-border-light`, `--colour-surface-alt`, `--colour-surface-soft`, `--colour-surface-strong`, `--colour-accent`, `--colour-accent-strong`, `--colour-accent-soft`, `--colour-accent-text`, `--colour-info`, `--colour-info-soft`, `--colour-profit-soft`, `--colour-loss-soft`, `--colour-warning-soft`, `--colour-text-primary`, `--colour-on-profit`, `--colour-on-loss`

3. **Add guards**: `authGuard` (JWT-based), `subscriptionGuard` (subscription check), `mobileRedirectGuard` (desktop enforcement).

4. **Add interceptors**: `authInterceptor` (attaches Bearer token), `errorInterceptor` (global error handling with `SKIP_ERROR_NOTIFICATION` HttpContext token).

5. **Add services**: `GoogleAuthService`, `NotificationService` (snackbar, 4 severity classes), `ResponsiveDialogService` (mobile-aware dialogs), `LayoutService` (`isMobile` signal), `StrategyDraftService` (sessionStorage persistence).

6. **Update core subfolder structure**: Add `guards/`, `interceptors/`, `components/` (HelpPanelComponent, SidebarNavComponent, MobileNavComponent), `pipes/` (DurationPipe, HelpMarkdownPipe), `utils/`.

7. **Add `app.config.ts` full providers**: `provideAnimationsAsync`, `NativeDateAdapter` with `en-GB`, `MAT_DATE_FORMATS`, both interceptors.

8. **Note `CommonModule` inconsistency**: Several components still import it (market-context-card, nl-input-card, assumptions-panel, grid-state, market-data) despite the "never import CommonModule" rule.

9. **Note JWT in localStorage**: `auth_token`, `auth_refresh_token`, `auth_user` stored in `localStorage` — intentional design despite security guideline.

10. **Add third-party libraries**: `marked` (`^17.0.5`) for markdown rendering.

11. **Add deployment config**: `staticwebapp.config.json` for Azure Static Web Apps routing, `proxy.conf.json` for local dev API proxy.

## Phase Success Criteria

- Charting documentation accurately describes the 3-pane candlestick chart
- All UI routes and feature pages are documented
- Angular instructions reflect actual color palette and full token set
- Guards, interceptors, and core services documented
