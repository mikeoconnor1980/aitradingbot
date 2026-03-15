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

Strategies are stored as JSON configuration.
Each user's strategies are isolated from all other users.

---

# Dashboard

Per-user dashboard displays:

bot state  
PnL  
open orders  
positions  
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