# UI Design

The frontend is an Angular 19 standalone SPA in `frontend/trading-ui/`. The implemented UI is organised around an authenticated shell, a compact dashboard for account operations, a larger desktop-oriented strategy authoring surface, and several specialist pages for market data, backtesting, optimisation, agents, and profile management.

## Shell Overview

`AppComponent` provides the outer shell and conditionally renders `SidebarNavComponent`, `MobileNavComponent`, and `HelpPanelComponent`. It also subscribes to SignalR connection state, API health, authenticated user state, and the preferred network so those indicators stay visible outside feature pages.

| Shell Component | Purpose |
|---|---|
| `SidebarNavComponent` | Primary desktop navigation for authenticated routes |
| `MobileNavComponent` | Bottom navigation for mobile-safe routes |
| `HelpPanelComponent` | Contextual help drawer powered by markdown content |
| `HelpMarkdownPipe` | Converts markdown help text to HTML |
| `DurationPipe` | Formats elapsed durations in reusable UI fragments |

## Route Map

Routes are declared in `frontend/trading-ui/src/app/app.routes.ts` and use lazy `loadComponent` entries throughout.

| Route | Component | Guards | Notes |
|---|---|---|---|
| `/login` | `LoginPageComponent` | none | Email/password sign-in plus Google sign-in |
| `/register` | `RegisterPageComponent` | none | Email/password registration plus Google sign-in |
| `/dashboard` | `DashboardComponent` | `authGuard` | Default authenticated landing page |
| `/market-data` | `MarketDataComponent` | `authGuard` | Candles, market info, fills, indicators |
| `/strategies` | `StrategyListPageComponent` | `authGuard`, `subscriptionGuard`, `mobileRedirectGuard` | Strategy inventory |
| `/strategies/new` | `StrategyBuilderPageComponent` | `authGuard`, `subscriptionGuard`, `mobileRedirectGuard` | Full builder; protected by `unsavedChangesGuard` |
| `/strategies/wizard` | `StrategyWizardPageComponent` | `authGuard`, `subscriptionGuard`, `mobileRedirectGuard` | Guided authoring flow |
| `/strategies/:id/edit` | `StrategyBuilderPageComponent` | `authGuard`, `subscriptionGuard`, `mobileRedirectGuard` | Edit mode; revision and AI review enabled |
| `/connection` | `StatusCardComponent` | `authGuard`, `mobileRedirectGuard` | Exchange connectivity status |
| `/order-entry` | `OrderEntryComponent` | `authGuard`, `subscriptionGuard` | Manual order placement |
| `/backtesting` | `BacktestPageComponent` | `authGuard`, `subscriptionGuard`, `mobileRedirectGuard` | Backtest configuration and analysis |
| `/candle-data` | `CandleManagementComponent` | `authGuard`, `mobileRedirectGuard` | Binance candle ingestion/admin operations |
| `/optimizer` | `OptimizerPageComponent` | `authGuard`, `subscriptionGuard`, `mobileRedirectGuard` | Parameter optimisation workflows |
| `/agents` | `AgentsPageComponent` | `authGuard`, `subscriptionGuard`, `mobileRedirectGuard` | Agent fleet control plane |
| `/profile` | `ProfilePageComponent` | `authGuard` | User profile and wallet/network settings |
| `/macro-calendar` | `MacroCalendarPageComponent` | `authGuard`, `mobileRedirectGuard` | Economic calendar and event review |

## Dashboard

The dashboard is intentionally operational rather than chart-centric. The current template does not render a price chart, a signals widget, or the older bot-state/grid-state panel.

Implemented sections:

| Section | Details |
|---|---|
| `AccountSummaryComponent` | Account equity, margin and drawdown information |
| `MarketContextCardComponent` | Polls market-context data on a 60-second cadence and shows regime/sentiment context |
| `PositionsTableComponent` | Close, close-all, set/edit/remove SL/TP actions |
| `OrdersTableComponent` | Cancel, cancel-all, and modify open orders |
| `ActivityFeedComponent` | Third tab beside Positions and Orders; capped to 100 events |
| Mobile FAB | Quick navigation to `/order-entry` |

Known gap: `grid-state/` still exists under the dashboard feature, but it is not rendered by `dashboard.component.html`.

## Strategy Authoring

Strategy authoring lives under `frontend/trading-ui/src/app/features/strategy-builder/` and has two complementary entry points.

### Strategy List

`StrategyListPageComponent` at `/strategies` lists saved strategies with market, timeframe, direction, mode, version, and edit/delete actions.

### Strategy Builder

`StrategyBuilderPageComponent` powers `/strategies/new` and `/strategies/:id/edit`. It uses a large reactive form and composes the following cards:

| Component | Purpose |
|---|---|
| `NlInputCardComponent` | Accepts natural-language strategy text, submits to `StrategyApiService.interpretStrategy`, and emits `StrategyIntentDto` |
| `ConfidenceBadgeComponent` | Displays interpreter confidence level |
| `AssumptionsPanelComponent` | Shows model assumptions and clarification requirements |
| `StrategyTemplateSelectorComponent` | Selects the initial template/mode |
| `StrategyDetailsCardComponent` | Name, market, timeframe, direction |
| `GridConfigCardComponent` | Grid levels, spacing, entry mode, anchor price, breakdown threshold |
| `ExitRulesCardComponent` | Take-profit and stop-loss definitions |
| `RiskManagementCardComponent` | Position sizing, leverage, max open trades, cooldown |
| `TrendFilterCardComponent` | Optional trend filter settings |
| `EntryConditionsCardComponent` | Signal-mode entry-condition container |
| `RsiConditionItemComponent` | RSI condition editor |
| `PriceVsEmaConditionItemComponent` | Price-vs-EMA condition editor |
| `MacdConditionItemComponent` | MACD condition editor |
| `SupportResistanceConditionItemComponent` | Fourth implemented condition type for support/resistance rules |
| `PreviewSummaryCardComponent` | Human-readable summary |
| `AiReviewCardComponent` | Inline AI review summary with truncated markdown preview |
| `ValidationCardComponent` | Client/server validation results |
| `JsonPreviewCardComponent` | Current config JSON |
| `RevisionHistoryPanelComponent` | Revision history, diff selection, restore actions |
| `StrategyBacktestHistoryComponent` | Related backtest history in edit mode |
| `InfoPopoverComponent` | Contextual inline explanations used inside cards |

Services used by the builder:

| Service | Responsibility |
|---|---|
| `StrategyApiService` | CRUD, validation, interpretation, reviews, revision history |
| `StrategyMapperService` | Maps form state to `StrategyConfig` |
| `StrategyValidationService` | Client-side validation rules |
| `ConditionFactoryService` | Builds typed condition `FormGroup` instances |
| `StrategyDraftService` | Wizard-side `sessionStorage` persistence |

#### Natural-Language Input

`NlInputCardComponent` sends free-text input to the strategy API, handles rate-limit and validation failures locally, and emits a structured `StrategyIntentDto` back into the page. The builder then uses `ConfidenceBadgeComponent` and `AssumptionsPanelComponent` to expose the model's certainty and assumptions.

#### AI Review

The edit experience can request a strategy review after save. `AiReviewCardComponent` renders a truncated markdown summary using the `marked` library, and `AiReviewModalComponent` opens the full review text in a dialog.

#### Template and Signal-Mode Inconsistency

`STRATEGY_TEMPLATES` now includes:

- `grid`
- `custom_signal`
- `ema_pullback`
- `macd_cross`
- `rsi_reversal` (`available: false`)
- `blank` (`available: true`)

The builder's signal-template checks still only have concrete defaults for the original signal templates. `blank` and the unavailable `rsi_reversal` are represented in the template list but not given dedicated builder logic, so this remains a knowledge-worthy inconsistency.

### Strategy Wizard

`StrategyWizardPageComponent` at `/strategies/wizard` is a guided `MatStepper` flow intended for desktop users.

| Step Component | Purpose |
|---|---|
| `WizardGoalStepComponent` | Choose the high-level strategy objective |
| `WizardMarketStepComponent` | Market, timeframe, and direction |
| `WizardEntryStepComponent` | Template-aware entry logic |
| `WizardExitStepComponent` | Exit rules |
| `WizardRiskStepComponent` | Sizing and leverage |
| `WizardFilterStepComponent` | Trend filters for signal strategies |
| `WizardReviewStepComponent` | Final review and handoff |

The wizard auto-saves drafts through `StrategyDraftService`, which serialises the current `StrategyConfig` into `sessionStorage` under the `strategy_draft` key. Users can save directly from the wizard or switch to the full builder with the draft prefilled in router state.

## Market Data and Charting Surface

The main price chart is not on the dashboard. It lives on `/market-data` inside `MarketDataComponent`, alongside market info, timeframe selection, fill visibility toggles, and historical candle paging. The charting implementation is described in `09-charting-library.md`.

## Backtesting and Optimisation

The UI includes desktop-focused analysis pages beyond the original design notes:

| Page | Highlights |
|---|---|
| `/backtesting` | Backtest setup, run list, result summaries, equity chart, comparison, cycle narrative, trade log |
| `/optimizer` | Strategy optimisation workflows and result review |

## Agents and Operations UI

`AgentsPageComponent` at `/agents` acts as the execution-agent control plane. It polls every 5 seconds, shows agent status/heartbeat/queue state, and exposes:

- `StartTradingDialogComponent`
- `KillSwitchDialogComponent`
- stop and reinstate actions

This page is desktop-only and subscription-gated.

## Authentication UI

Authentication is fully implemented in the frontend.

| Route | Features |
|---|---|
| `/login` | Email/password form, Google sign-in button, route to dashboard on success |
| `/register` | Email, display name, password/confirm password, password complexity checks, Google sign-up button |

Both pages initialise `GoogleAuthService` in `ngAfterViewInit()` and render the Google Identity Services button into a `ViewChild` host element.

## Other Documented Feature Pages

| Route | Summary |
|---|---|
| `/profile` | Profile, wallet address, preferred network and subscription-related actions |
| `/connection` | Connectivity and signer/exchange status |
| `/order-entry` | Manual order ticket flow |
| `/candle-data` | Candle ingestion control surface |
| `/macro-calendar` | Macro-event calendar page |

## Future Recommendations

- Add grid-level overlays and strategy lifecycle annotations directly on the market-data candlestick chart.
- Deliver a genuinely mobile-optimised dashboard instead of only hiding desktop-only pages behind `mobileRedirectGuard`.
- Add a dedicated real-time P&L widget rather than relying on summary cards and tables.
- Add a strategy comparison view that contrasts live, backtest, and optimisation outcomes.
- Add a trade journal page that ties fills, AI reviews, and cycle narratives together.