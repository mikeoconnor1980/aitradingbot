# Feature Specification

This document describes the current product surface as implemented in the codebase. It distinguishes between features that are live in the API, worker, and Angular UI versus features that were originally planned but are not yet shipped.

## Current Product Surface

| Feature Area | Status | What Exists Today |
|---|---|---|
| Authentication | Implemented | Email/password registration and login, JWT access and refresh tokens, `GET /api/auth/me`, and Google SSO via `POST /api/auth/google` |
| Subscription access | Implemented, limited | `POST /api/subscriptions/free` creates a 30-day free subscription; guards in the Angular app use subscription status to gate trading features |
| Wallet connection | Implemented | Users store wallet addresses in the platform profile while private keys remain on the execution agent |
| Strategy builder | Implemented | JSON-backed strategy authoring, validation, save/load, revision history, diff/restore, and strategy review retrieval |
| Strategy wizard | Implemented | A guided 7-step creation flow at `/strategies/wizard` with educational prompts and a review step |
| NLP strategy interpreter | Implemented | `POST /api/strategies/interpret` turns natural-language prompts into `StrategyConfig` output |
| AI strategy review | Implemented | Revision-scoped AI review endpoints plus builder UI to request and display a persisted Markdown review |
| Backtesting | Implemented | Historical replay, metrics, audit log, charting, trade log, and result exploration in the UI |
| Strategy optimizer | Implemented | Sweep and evolutionary optimization, persisted runs/results, progress tracking, and optimizer UI |
| Live trading control plane | Implemented | Dashboard, market data, position/order views, strategy activation through the agent, and risk-aware execution controls |
| Agents page | Implemented | Agent listing, start/stop, pending command visibility, kill switch, reinstate, and update-state reporting |
| Macro calendar | Implemented | Event browser, active block visibility, sync endpoint, and live entry blocking through `MacroEventRiskCheck` |
| Help and tutorial system | Implemented | Global help panel, curated topics, and `POST /api/help/chat` assistant-style guidance |

## User Onboarding

Current onboarding is:

1. Register or sign in with email/password or Google.
2. Activate the free subscription tier.
3. Configure a wallet address and any preferred network settings.
4. Create a strategy through the builder, the wizard, or the NLP interpreter.
5. Backtest or optimize the strategy.
6. Start or stop execution through a connected agent.

There is no paid-plan purchase, upgrade, downgrade, or billing-history flow in the current application.

## Subscription Model

The shipped subscription model is intentionally narrow.

| Capability | Current State |
|---|---|
| Free access | Implemented via `POST /api/subscriptions/free` |
| Duration | 30 days |
| Paid plans | NOT IMPLEMENTED |
| Stripe integration | NOT IMPLEMENTED |
| Upgrade or downgrade flows | NOT IMPLEMENTED |
| Billing history | NOT IMPLEMENTED |
| Self-service cancellation | NOT IMPLEMENTED |

The knowledge base should treat the app as free-tier-only until a real billing system exists.

## Strategy Authoring and Review

Strategy authoring is broader than the original plan now assumed.

Implemented capabilities include:

- direct builder editing for strategy JSON-backed configuration
- immutable `StrategyRevision` history with diff and restore support
- natural-language interpretation into strategy configuration
- AI review of saved revisions
- strategy wizard guidance for new users

This means the strategy surface is no longer just CRUD. It is a mixed authoring workflow spanning manual editing, guided creation, AI generation, and AI review.

## Operations and Runtime Controls

Operational features available to end users today include:

- dashboard views for market context, positions, orders, and subscription status
- agent command routing for start, stop, order, cancellation, leverage, and trigger-order actions
- kill-switch management from the Agents page
- macro calendar visibility with active event blocking surfaced in the UI
- help content and chat guidance inside the control plane

## NOT IMPLEMENTED

The following features should not be described as shipped:

- admin dashboard for platform operators
- per-user admin revenue metrics
- admin error-monitoring UI
- admin-visible global tenant operations panel
- Stripe or any other payment integration
- paid plan selection or billing lifecycle management

There are agent operational controls, but there is no separate administrator product surface yet.

## Future Recommendations

- Add paid tiers and Stripe-backed billing only after entitlement and expiry policy are fully defined.
- Add a real admin console for incident response, tenant diagnostics, and support operations.
- Add explicit subscription lifecycle actions such as renewal, downgrade, cancellation, and billing history.
- Add feature-flagged operator views for fleet health, update rollout status, and kill-switch audits.