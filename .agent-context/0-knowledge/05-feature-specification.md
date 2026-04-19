# Feature Specification

This document describes the current product surface as implemented in the codebase. It distinguishes between features that are live in the API, worker, and Angular UI versus features that were originally planned but are not yet shipped.

## Current Product Surface

| Feature Area | Status | What Exists Today |
|---|---|---|
| Authentication | Implemented | Email/password registration and login, JWT access and refresh tokens, `GET /api/auth/me`, and Google SSO via `POST /api/auth/google` |
| Subscription access | Implemented | Beginner and Pro tiers, 1-year testing trial, profile-based subscribe/cancel flows, tier-aware route guards, and server-side entitlement enforcement |
| Wallet connection | Implemented | Users store wallet addresses in the platform profile while private keys remain on the execution agent |
| Strategy builder | Implemented | JSON-backed strategy authoring, validation, save/load, revision history, diff/restore, and strategy review retrieval |
| Strategy wizard | Implemented | A guided 7-step creation flow at `/strategies/wizard` with educational prompts and a review step |
| NLP strategy interpreter | Implemented | `POST /api/strategies/interpret` turns natural-language prompts into `StrategyConfig` output |
| AI strategy review | Implemented | Revision-scoped AI review endpoints plus builder UI to request and display a persisted Markdown review |
| Backtesting | Implemented | Historical replay, metrics, audit log, charting, trade log, and result exploration in the UI |
| Strategy optimizer | Implemented | Sweep and evolutionary optimization, persisted runs/results, progress tracking, and optimizer UI |
| Live trading control plane | Implemented | Dashboard, market data, position/order views, strategy activation through the agent, and risk-aware execution controls |
| Agents page | Implemented | Agent listing, start/stop, pending command visibility, kill switch, reinstate, and update-state reporting |
| TradingView webhooks | Implemented | Pro-tier webhook management UI, public TradingView ingress endpoint, buy/sell/close mapping, and worker execution through connected agents |
| Macro calendar | Implemented | Event browser, active block visibility, sync endpoint, and live entry blocking through `MacroEventRiskCheck` |
| Help and tutorial system | Implemented | Global help panel, curated topics, and `POST /api/help/chat` assistant-style guidance |

## User Onboarding

Current onboarding is:

1. Register or sign in with email/password or Google.
2. Choose a Beginner or Pro subscription tier from Profile.
3. Configure a wallet address and any preferred network settings.
4. Create a strategy through the builder, the wizard, or the NLP interpreter.
5. Backtest or optimize the strategy.
6. Start or stop execution through a connected agent.

There is no paid-plan purchase, Stripe checkout, or billing-history flow in the current application. Tier selection is a product entitlement flow only.

## Subscription Model

The shipped subscription model now has real feature entitlements, but billing is still deferred.

| Capability | Current State |
|---|---|
| Beginner tier | Implemented |
| Pro tier | Implemented |
| Trial duration | 365 days for both tiers |
| Subscribe flow | Implemented via `POST /api/subscriptions/subscribe` |
| Legacy free alias | `POST /api/subscriptions/free` maps to Beginner |
| Self-service cancellation | Implemented via `POST /api/subscriptions/cancel` and Profile UI |
| Paid billing | NOT IMPLEMENTED |
| Stripe integration | NOT IMPLEMENTED |
| Billing history | NOT IMPLEMENTED |
| In-place commercial upgrade flow | NOT IMPLEMENTED |

### Tier Entitlements

| Capability | Beginner | Pro |
|---|---|---|
| Strategy library | 2 admin-configurable Beginner-visible templates only | Full library |
| Manual and strategy-trading assets | BTC, ETH only | All supported assets |
| Max leverage | 5x | Exchange/asset maximum |
| AI review | Not available | Available |
| Macro calendar | Not available | Available |
| Optimizer | Not available | Available |
| TradingView webhooks | Not available | Available |

Tier restrictions are enforced in both the Angular app and the API. Order entry, strategy validation, template cloning, and feature routes should all be treated as entitlement-aware.

## Strategy Authoring and Review

Strategy authoring is broader than the original plan now assumed.

Implemented capabilities include:

- direct builder editing for strategy JSON-backed configuration
- immutable `StrategyRevision` history with diff and restore support
- natural-language interpretation into strategy configuration
- AI review of saved revisions for Pro-tier users
- strategy wizard guidance for new users
- tier-aware template visibility so Beginner users only see the subset explicitly marked for Beginner access

This means the strategy surface is no longer just CRUD. It is a mixed authoring workflow spanning manual editing, guided creation, AI generation, and AI review.

## Operations and Runtime Controls

Operational features available to end users today include:

- dashboard views for market context, positions, orders, and subscription status
- agent command routing for start, stop, order, cancellation, leverage, and trigger-order actions
- kill-switch management from the Agents page
- macro calendar visibility with active event blocking surfaced in the UI for Pro-tier users
- help content and chat guidance inside the control plane

Manual order entry is now also entitlement-aware:

- the asset list is filtered by the current subscription tier
- leverage updates are clamped and validated against the tier max leverage
- the API rejects disallowed assets or leverage even if a stale client attempts them

## NOT IMPLEMENTED

The following features should not be described as shipped:

- admin dashboard for platform operators
- per-user admin revenue metrics
- admin error-monitoring UI
- admin-visible global tenant operations panel
- Stripe or any other payment integration
- payment-backed plan selection or billing lifecycle management

There are agent operational controls, but there is no separate administrator product surface yet.

## Future Recommendations

- Add commercial billing only after the entitlement model is stable and upgrade/downgrade policy is explicitly defined.
- Add a real admin console for incident response, tenant diagnostics, and support operations.
- Add explicit subscription lifecycle actions such as renewal, downgrade handling, and billing history.
- Add feature-flagged operator views for fleet health, update rollout status, and kill-switch audits.