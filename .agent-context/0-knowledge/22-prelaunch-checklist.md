# Pre-Launch Audit Checklist

## Overview

This document defines the required checks before launching the trading platform as a paid SaaS product.

The goal is to ensure:

- user funds are protected
- API keys are secure
- strategies behave deterministically
- the system is resilient under failure conditions
- multi-tenant isolation is enforced

---

# 1. Security Audit

## API Key Handling

- [ ] API keys are encrypted at rest
- [ ] API keys are never stored in plain text
- [ ] API keys are never logged
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

- [ ] Secure authentication implemented (JWT / OAuth)
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
- [ ] Orders cannot bypass RiskEngine
- [ ] ExecutionEngine only accepts validated signals
- [ ] Backtest mode cannot call live execution

---

## Risk Controls

- [ ] Max position size enforced
- [ ] Max exposure limits enforced
- [ ] Strategy-level limits enforced
- [ ] Daily loss / drawdown limits (optional but recommended)
- [ ] Kill switch implemented (global stop trading)

---

## Strategy Consistency

- [ ] Strategy runs only on closed candles
- [ ] CandleClock prevents duplicate triggers
- [ ] StrategyScheduler executes once per candle
- [ ] State transitions are deterministic
- [ ] GridState lifecycle fully defined

---

## Hedge & Grid Safety

- [ ] Hedge cannot overexpose account
- [ ] Grid cannot expand infinitely
- [ ] Take profit logic validated
- [ ] Partial fills handled correctly
- [ ] State recovery after restart verified

---

# 3. Backtesting Integrity

- [ ] Backtest uses same StrategyEngine as live trading
- [ ] Backtest uses same RiskEngine
- [ ] Backtest uses simulated execution only
- [ ] No live API calls during backtesting
- [ ] Historical data integrity verified

---

# 4. Infrastructure & Cloud Audit

## Azure / Hosting

- [ ] All services run in private networks where possible
- [ ] Public endpoints secured
- [ ] Firewall rules configured
- [ ] Unused ports closed

---

## Secrets & Identity

- [ ] Azure Key Vault used for secrets
- [ ] Managed identities used instead of hardcoded credentials
- [ ] Secrets rotated periodically (future improvement)

---

## Monitoring & Logging

- [ ] Application logging enabled
- [ ] Error tracking enabled
- [ ] Trade execution logs recorded
- [ ] Alerts configured for failures
- [ ] Logs do not contain sensitive data

---

## Resilience

- [ ] Retry logic implemented for API failures
- [ ] Circuit breaker for repeated failures
- [ ] Graceful degradation on exchange downtime
- [ ] System restart does not cause duplicate trades

---

# 5. Operational Readiness

## Uptime

- [ ] System can run continuously (24/7)
- [ ] Background workers monitored
- [ ] Health checks implemented

---

## Incident Handling

- [ ] Error logs accessible
- [ ] Basic incident response plan defined
- [ ] Ability to pause trading globally

---

## Deployment

- [ ] CI/CD pipeline configured
- [ ] Environment separation (dev / staging / prod)
- [ ] Configs not hardcoded

---

# 6. Legal & Compliance

- [ ] Terms of Service created
- [ ] Risk disclaimer included
- [ ] Privacy policy compliant with UK GDPR
- [ ] Clear statement: platform is software, not financial advice

---

# 7. User Trust & Transparency

- [ ] Strategy Decision Log implemented
- [ ] Users can view why trades occurred
- [ ] Basic performance metrics visible
- [ ] UI shows strategy state (e.g. GridActive, Idle)

---

# 8. External Review (Recommended)

- [ ] Independent code review completed
- [ ] Security-focused review completed
- [ ] Penetration test (light or full) performed
- [ ] Issues documented and resolved

---

# 9. Pre-Launch Final Checks

- [ ] Test with real exchange (small amounts)
- [ ] Simulate API failures
- [ ] Simulate network interruptions
- [ ] Restart system mid-trade
- [ ] Verify no duplicate execution
- [ ] Verify logs are correct and complete

---

# Summary

The platform should only launch as a paid service once:

- user data is secure  
- trading execution is safe and deterministic  
- infrastructure is stable  
- legal protections are in place  
- system behaviour is transparent  

This checklist ensures the system is not only functional, but trustworthy and production-ready.