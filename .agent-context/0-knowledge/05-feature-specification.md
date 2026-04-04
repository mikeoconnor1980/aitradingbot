# Feature Specification

Core features include:

User Registration & Authentication  
Subscription Management  
Exchange Key Connection  
Trading Engine  
Risk Engine  
Strategy Management  
Dashboard UI  
Admin Dashboard

---

# User Onboarding

Users can:

Register an account  
Subscribe to a plan  
Connect Hyperliquid wallet key  
Verify connection (read-only check)  
Activate trading

---

# Subscription Management

Managed via Stripe or similar provider.

Users can:

Choose a plan  
Upgrade or downgrade  
Cancel subscription  
View billing history

Trading is paused if subscription expires.

---

# Strategy Management

Users can:

Create strategy  
Configure parameters  
Save strategy configuration  
Activate strategy
View revision history
Compare revisions
Restore previous versions

Strategies are stored as JSON configuration.
Each user's strategies are isolated from all other users.

## Strategy Revision History (F3)

Every save creates an immutable `StrategyRevision` record:

- Automatic change summary (compares current vs. previous JSON)
- Source tracking (UI, API, import, or restore)
- Full JSON snapshot for deterministic restore
- Revision number (1, 2, 3...) per strategy

Users can browse revision history, view diffs between any two revisions, and restore a previous version. Restoring creates a new revision with source = `Restore` and a generated label. See [Domain Model — StrategyRevision](04-domain-model.md) for entity details.

## AI Strategy Review (F4)

Users can request an AI analysis of any saved strategy revision:

- Click "AI Review" button in the strategy builder to analyze the currently loaded revision
- Server generates a Markdown review covering entry logic quality, exit completeness, risk management, weaknesses, market regime fit, complexity, and execution realism
- Review persists per revision; re-requesting overwrites the prior review for that revision
- Rate limit: 1 request per minute per IP
- Persisted review loads automatically when opening a strategy with saved review

Review UI:

- Collapsible summary card in the strategy builder showing first portion of markdown
- Full-review modal with complete formatted markdown and review metadata (model name, timestamp)
- Cooldown timer in UI enforces server-side rate limit display
- Apply Suggestions button placeholder (not yet implemented; disabled)

See [LLM Context & Sentiment Architecture](17-llm-context-sentiment-architecture.md) for endpoint details and review analysis scope.
PnL  
open orders (with cancel, cancel-all, and modify actions)  
positions (with close action; SL/TP display, set dialog, inline edit, and removal)  
signals  
subscription status

---

# Admin Dashboard

Platform admin view:

total active subscribers  
system health  
per-user bot status  
revenue metrics  
error monitoring