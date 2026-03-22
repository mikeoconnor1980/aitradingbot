# Pre-Launch Audit Checklist

## Overview

This document defines the required checks before launching the trading platform as a paid SaaS product.

The goal is to ensure:

- user funds are protected
- API keys are secure
- strategies behave deterministically
- the system is resilient under failure conditions
- multi-tenant isolation is enforced
- order state is reconciled with the exchange at all times

---

# 1. Security Audit

## API Key Handling

- [ ] API keys are encrypted at rest
- [ ] API keys are never stored in plain text
- [ ] API keys are never logged (including in structured logs)
- [ ] API keys are not exposed to the frontend
- [ ] API keys are stored in a secure secrets manager (e.g. Azure Key Vault)
- [ ] Access to secrets is restricted via managed identity / RBAC

---

## API Key Permissions

- [ ] Users are instructed to disable withdrawal permissions
- [ ] System validates API key permissions where possible
- [ ] Platform blocks or warns on unsafe API key scopes

---

## Authentication & Authorisation

- [ ] Secure authentication implemented (JWT / OAuth via Azure AD B2C or Auth0)
- [ ] Passwords hashed with strong algorithm (if applicable)
- [ ] Role-based access control enforced
- [ ] Admin endpoints are protected
- [ ] Rate limiting applied to authentication endpoints

---

## Multi-Tenant Isolation

- [ ] Users cannot access other users' data
- [ ] Queries are always filtered by tenant/user ID
- [ ] No shared state between users without explicit isolation
- [ ] Background workers operate within tenant scope
- [ ] Logs are partitioned or filtered by tenant
- [ ] WebSocket streams are per-user authenticated (fills, positions, orders)

---

## Data Protection

- [ ] Sensitive data encrypted at rest
- [ ] HTTPS enforced for all endpoints
- [ ] No sensitive data in query strings
- [ ] Data retention policy defined

---

# 2. Trading Safety Audit

## Execution Safety

- [ ] No duplicate order execution paths
- [ ] Idempotency checks implemented for order placement
- [ ] Client order IDs used for all order submissions
- [ ] Orders cannot bypass RiskEngine
- [ ] ExecutionEngine only accepts validated signals
- [ ] Backtest mode cannot call live execution
- [ ] Signal lifecycle tracked end-to-end (Generated → Validated → Approved → Executed)

---

## Order Reconciliation & State Recovery

- [ ] Persisted order journal — every order submission recorded locally before sending to exchange
- [ ] Startup reconciliation — sync open orders and positions from Hyperliquid on worker start
- [ ] Mid-grid crash recovery — if worker crashes with partial fills, state is reconstructed from exchange + journal
- [ ] Stuck-order detection — timeout for orders that remain open beyond expected duration
- [ ] Exchange rejection handling — signals marked "Executed" locally but rejected by exchange are detected and corrected
- [ ] Partial-fill handling — grid levels partially filled are tracked correctly (not assumed fully filled or unfilled)
- [ ] Orphan position detection — positions that exist on exchange but not in local state are flagged

---

## Kill Switch & Circuit Breaker

- [ ] Emergency flatten endpoint — cancel all open orders + close all positions immediately
- [ ] Per-user kill switch — stop trading for a single user without affecting others
- [ ] Global kill switch — stop all trading across all users
- [ ] Automatic circuit breaker — triggers on:
  - [ ] Rapid consecutive losses (e.g. 3+ losing grid cycles in a row)
  - [ ] Unexpected position size (position larger than any signal would create)
  - [ ] Repeated exchange errors (e.g. 5+ rejections in 10 minutes)
  - [ ] Daily loss limit breach
- [ ] Circuit breaker logs reason and notifies admin
- [ ] Kill switch accessible via API endpoint and admin UI

---

## Risk Controls

- [ ] Max position size enforced
- [ ] Max exposure limits enforced
- [ ] Strategy-level limits enforced
- [ ] Daily loss / drawdown limits enforced
- [ ] Cooldown period enforced after daily loss limit hit
- [ ] All rejected signals logged with rejection reason

---

## Strategy Consistency

- [ ] Strategy runs only on closed candles
- [ ] CandleClock prevents duplicate triggers
- [ ] StrategyScheduler executes once per candle
- [ ] State transitions are deterministic
- [ ] GridState lifecycle fully defined (Inactive → Planning → Deploying → Active → PartiallyFilled → FullyFilled → Closing → Closed)
- [ ] Invalid state transitions are rejected and logged

---

## Hedge & Grid Safety

- [ ] Hedge cannot overexpose account
- [ ] Grid cannot expand infinitely
- [ ] Take profit logic validated
- [ ] Partial fills handled correctly
- [ ] State recovery after restart verified
- [ ] No duplicate grids per user/symbol (PositionManager enforced)

---

## Execution Checkpoints

- [ ] StrategyExecutionCheckpoint persisted per user, per symbol, per timeframe
- [ ] Checkpoint prevents re-execution of already-processed candles after restart
- [ ] Checkpoint verified: restart mid-session does not duplicate signals or orders

---

# 3. Hyperliquid Exchange Integration

## API Rate Limiting

- [ ] Order submission queue with throttling to stay within Hyperliquid rate limits
- [ ] Rate limit breach handling — backoff, queue overflow alert
- [ ] Rate limiting tested under multi-user concurrent load

---

## WebSocket Connectivity

- [ ] Automatic reconnection on disconnect
- [ ] Full state sync after reconnect (open orders, positions, recent fills)
- [ ] Per-user stream restoration after disconnect
- [ ] No orders lost or duplicated during reconnection window
- [ ] Reconnection tested by simulating network drops

---

## Geographic Latency

- [ ] VPS proximity to Hyperliquid infrastructure verified
- [ ] Order placement round-trip latency measured and baselined
- [ ] Latency acceptable for candle-close execution model (seconds, not milliseconds)

---

# 4. Backtesting Integrity

- [ ] Backtest uses same StrategyEngine as live trading
- [ ] Backtest uses same RiskEngine
- [ ] Backtest uses same GridController
- [ ] Backtest uses simulated execution only
- [ ] No live API calls during backtesting
- [ ] Historical data integrity verified
- [ ] Fees modelled (maker/taker) and verified against real Hyperliquid fee tier
- [ ] Funding rate impact modelled for positions held >8 hours
- [ ] Slippage assumptions documented and configurable
- [ ] Breakeven win-rate calculated for current strategy parameters (0.8% TP vs fees/funding)

---

# 5. Infrastructure & Cloud Audit

## Azure / Hosting

- [ ] All services run in private networks where possible
- [ ] Public endpoints secured
- [ ] Firewall rules configured
- [ ] Unused ports closed

---

## Secrets & Identity

- [ ] Azure Key Vault used for secrets (production)
- [ ] Managed identities used instead of hardcoded credentials
- [ ] Secrets rotated periodically (future improvement)
- [ ] No secrets in environment variables, config files, or source control

---

## Monitoring & Logging

- [ ] Structured logging enabled with correlation IDs per signal/order/fill chain
- [ ] Error tracking enabled
- [ ] Trade execution logs recorded (every signal, order, fill, state transition)
- [ ] Logs do not contain API keys, private keys, or user credentials
- [ ] Alert channels configured:
  - [ ] Exchange connectivity loss
  - [ ] Daily P&L limit breached
  - [ ] Circuit breaker triggered
  - [ ] Worker crash / restart
  - [ ] Strategy mode change (Normal → Defensive → RiskOff)
- [ ] Alerts delivered via at least one channel (Telegram / email / Discord)
- [ ] Admin dashboard or log viewer for system health

---

## Database

- [ ] SQLite WAL mode enabled (POC)
- [ ] Concurrent read/write tested under expected subscriber load
- [ ] Azure SQL migration path validated with EF Core provider swap
- [ ] Database backups configured

---

## Resilience

- [ ] Retry logic implemented for API failures (with exponential backoff)
- [ ] Circuit breaker for repeated exchange failures
- [ ] Graceful degradation on exchange downtime (pause, don't crash)
- [ ] System restart does not cause duplicate trades (checkpoint + reconciliation)
- [ ] Worker process monitored with automatic restart on crash

---

# 6. Operational Readiness

## Uptime

- [ ] System can run continuously (24/7)
- [ ] Background workers monitored
- [ ] Health checks implemented (API + Worker)

---

## Paper Trading Burn-In

- [ ] Paper trading mode implemented (live market data, simulated execution)
- [ ] Minimum burn-in period defined (e.g. 7-14 days)
- [ ] Success criteria defined (no crashes, no duplicate orders, no state corruption)
- [ ] Paper trading results reviewed before enabling live capital

---

## Subscription Billing Safety

- [ ] Trading pauses immediately on subscription expiry
- [ ] Open positions handled gracefully on subscription lapse (flatten or hold — policy defined)
- [ ] No trading resumes until subscription is reactivated
- [ ] Billing state transitions tested (active → expired → reactivated)

---

## Incident Handling

- [ ] Error logs accessible to admin
- [ ] Basic incident response plan defined
- [ ] Ability to pause trading globally (kill switch)
- [ ] Ability to pause trading per user
- [ ] Post-incident review process defined

---

## Deployment

- [ ] CI/CD pipeline configured
- [ ] Environment separation (dev / staging / prod)
- [ ] Configs not hardcoded
- [ ] Rollback procedure documented and tested

---

# 7. Legal & Compliance

- [ ] Terms of Service created
- [ ] Risk disclaimer included (trading involves risk of loss)
- [ ] Privacy policy compliant with UK GDPR
- [ ] Clear statement: platform is software, not financial advice
- [ ] No guarantees of profit in any marketing material
- [ ] User agreement covers API key custody and liability

---

# 8. User Trust & Transparency

## Signal Audit Trail

- [ ] Every signal persisted with full lifecycle (Generated → Validated → Approved → Executed)
- [ ] Rejected signals logged with rejection reason and which risk gate blocked them
- [ ] Signal history queryable per user
- [ ] Users can view why trades occurred (Strategy Decision Log)

---

## Dashboard Visibility

- [ ] Basic performance metrics visible (P&L, win rate, trade count)
- [ ] UI shows strategy state (e.g. Active, Idle, Defensive, Cooldown)
- [ ] Open positions and open orders displayed
- [ ] Risk engine status visible (exposure %, daily P&L %, leverage)

---

# 9. External Review (Recommended)

- [ ] Independent code review completed
- [ ] Security-focused review of API key handling and multi-tenant isolation
- [ ] Penetration test (light or full) performed
- [ ] Order reconciliation logic reviewed by second pair of eyes
- [ ] Issues documented and resolved

---

# 10. Pre-Launch Final Checks

## Live Integration Tests (Small Amounts)

- [ ] Place and fill a real grid order on Hyperliquid (minimum size)
- [ ] Verify take-profit execution on real exchange
- [ ] Verify hedge opening and closing on real exchange
- [ ] Verify order cancellation works correctly

---

## Failure Simulation

- [ ] Simulate API failures — verify retry and circuit breaker behaviour
- [ ] Simulate network interruptions — verify WebSocket reconnection and state sync
- [ ] Simulate exchange downtime — verify graceful degradation
- [ ] Kill worker process mid-grid — verify state recovery on restart
- [ ] Kill worker process mid-order — verify no duplicate orders and reconciliation works

---

## Verification

- [ ] Verify no duplicate execution after restart (checkpoint system)
- [ ] Verify order reconciliation matches local state to exchange state
- [ ] Verify circuit breaker triggers correctly on rapid losses
- [ ] Verify kill switch flattens all positions and cancels all orders
- [ ] Verify logs are correct, complete, and contain no sensitive data
- [ ] Verify multi-tenant isolation under concurrent load (user A cannot see user B data)

---

## Paper Trading Sign-Off

- [ ] Paper trading burn-in completed for minimum defined period
- [ ] No crashes or state corruption during burn-in
- [ ] No duplicate orders during burn-in
- [ ] Performance metrics reviewed and strategy edge validated
- [ ] Sign-off documented before enabling live capital

---

# Summary

The platform should only launch as a paid service once:

- user data is secure
- trading execution is safe and deterministic
- order state is reconciled with the exchange at all times
- kill switch and circuit breaker are tested and operational
- infrastructure is stable with monitoring and alerts
- paper trading burn-in completed successfully
- legal protections are in place
- system behaviour is transparent and auditable

This checklist ensures the system is not only functional, but trustworthy and production-ready.