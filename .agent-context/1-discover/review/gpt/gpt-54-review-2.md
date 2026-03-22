# Project Review 2

**Reviewer:** GPT-5.4  
**Date:** 2026-03-22  
**Status:** Documentation + wireframes review  
**Previous review:** [gpt-54-summary.md](./gpt-54-summary.md)

---

## Overall Verdict

This project is materially stronger than it was in the earlier review.

The biggest change is that the hard operational parts are no longer absent from the design. They are now named, scoped, and treated as launch criteria. The architecture is more coherent, the product thinking is more mature, and the wireframes show a system that is being designed as an actual product rather than just a trading engine.

This is now a credible architecture for building a serious trading platform.

It is still not yet a credible live trading system.

The remaining weakness is alignment, not awareness. The knowledge base, roadmap, and wireframes do not fully point in the same direction yet.

---

## What Improved Since The Earlier Review

### 1. Operational safety is now first-class in the documentation

The earlier review said the plan underweighted the layers that matter most in production. That is no longer true at the documentation level.

The pre-launch checklist now explicitly requires:

- idempotent order placement
- client order IDs
- persisted order journal
- startup reconciliation
- partial-fill handling
- per-user and global kill switches
- circuit breakers
- paper trading burn-in

This is a major improvement. Those controls are now treated as launch gates rather than nice-to-haves.

### 2. Runtime boundaries are much clearer

The system architecture around CandleClock, StrategyScheduler, Signal Contracts, GridController, RiskEngine, PositionManager, and ExecutionEngine is substantially cleaner than before.

In particular:

- scheduling is properly tied to confirmed candle closes
- live and backtest flows share the same core pipeline
- signal intent is separated from execution
- grid lifecycle management is no longer buried inside strategy code

That gives the project a much better chance of behaving deterministically across live and replayed environments.

### 3. The project now shows real product thinking, not just backend thinking

The wireframes are materially more developed than the earlier documentation set.

You now have visible product support for:

- onboarding
- strategy configuration
- backtesting
- signal visibility
- order and position history
- admin monitoring
- error handling surfaces

That matters because trust, transparency, and operability are core product requirements for a trading system.

### 4. Option C is a meaningful strategic improvement

The split architecture option is the most interesting strategic development in the current design.

It uses the signal-contract boundary well:

- cloud decides
- user-side agent executes
- keys stay with the user

That is a strong answer to the trust and liability problems of fully hosted key custody, while preserving centralized strategy control.

---

## Main Findings

### 1. High: the project still has a deployment-model conflict

The business-model analysis is now richer, but the system still has not committed to a default delivery model.

That is not a minor product question. It changes core assumptions about:

- key custody
- trust model
- legal exposure
- onboarding flow
- operational monitoring
- reconciliation design

The knowledge files explicitly keep Option A, Option B, and Option C open.

But the current product flow and wireframes are already strongly optimized for Option B, the platform-hosted model:

- users are asked to enter wallet private keys directly into the product
- onboarding assumes immediate exchange connection and strategy activation
- the exchange screen explains encrypted-at-rest platform storage

If Option C is the real strategic preference, the current onboarding and exchange UX are pointed at the wrong operating model.

If Option B is the real preference, then the project should stop treating the hosting model as undecided and instead push harder on the legal, trust, and security implications.

Right now the architecture and the product UX are partially misaligned.

### 2. High: the development plan is now behind the architecture

The biggest documentation inconsistency is the roadmap.

The current development plan still reads like a prototype sequence:

1. project setup
2. database
3. strategy plugin
4. config
5. worker
6. API
7. Angular UI

That plan no longer matches the maturity of the rest of the knowledge base.

Your newer documents now treat the following as mandatory before launch:

- execution idempotency
- reconciliation and recovery
- kill switch and circuit breaker
- paper trading burn-in
- monitoring and alerting

So the architectural thinking has advanced, but the delivery plan has not caught up.

This matters because teams build what the roadmap prioritizes. If the old plan remains the real execution sequence, there is a real risk of shipping surface area before safety.

### 3. Medium: the product still pushes users toward live trading too early

The onboarding and wireframes are cleaner now, but they still guide the user from registration to key entry to activation to deployment in a fairly direct line.

That conflicts with the newer operational standard, which now clearly expects paper trading and burn-in before real capital.

The backtest screen is useful and much more mature than before, but the product flow still appears to be:

Backtest if you want, then activate live.

For this kind of system, the safer product posture is:

1. Backtest
2. Paper trade
3. Promote to live

That transition should be visible in the product model, not just in launch checklists.

### 4. Medium: backtesting is more honest in the docs, but not yet harsh enough in the product story

The backtesting architecture is clearly improved. It now acknowledges:

- candle-based fills are simplified
- fees and slippage must be modelled
- unrealistic assumptions will overstate performance

That is a substantial step forward from the earlier review.

But the backtest experience is still presented mainly through attractive summary metrics such as:

- net profit
- total return
- profit factor
- Sharpe-like metrics

without an equally prominent emphasis on:

- realism assumptions
- funding impacts
- fill-quality caveats
- unsupported market conditions

For a grid strategy on leveraged perps, those caveats are not side notes. They are central to whether the system survives.

### 5. Medium: operations visibility improved a lot, but intervention tooling is still less explicit than monitoring

This is an area of real progress.

The admin and history views now make operational problems visible:

- bot errors
- degraded providers
- halted runs
- reconnect events
- user-level bot status

That is a meaningful improvement over the earlier review baseline.

But the reviewed screens still emphasize observation more than control.

The project now says kill switches and flatten controls are required. Those controls should be treated as visible product features, not just backend capabilities or checklist items.

---

## What Looks Strong

### 1. Deterministic execution remains the right core design choice

Running on confirmed candle closes is still the correct default for this class of system.

It keeps live behaviour aligned with replay behaviour and reduces a huge class of timing bugs.

### 2. The separation of concerns is now meaningfully better than before

The architecture is not just modular in a generic sense. The responsibilities are clearer:

- Strategy decides intent
- GridController manages lifecycle
- RiskEngine approves or blocks
- PositionManager keeps state coherent
- ExecutionEngine submits orders

That is a stronger design than putting too much logic inside a monolithic strategy plugin.

### 3. Signal contracts are a strong architectural boundary

The signal model is one of the best parts of the current design.

It gives you:

- auditability
- replayability
- a clean path for split execution
- better reasoning about approval vs execution

This is one of the places where the architecture is now clearly more mature than in the earlier review.

### 4. The GridController abstraction is a good move

This is a real improvement.

Grid strategies become brittle when entry logic, fill state, hedge state, and take-profit logic are all spread across the strategy itself.

Pulling lifecycle ownership into a GridController is the right move for:

- restart recovery
- debugging
- state safety
- clearer transition rules

### 5. The wireframes are broad, coherent, and product-aware

The UI work is not just decorative. It helps pressure-test the model.

The fact that you now have screens for:

- strategy history
- backtesting
- signals
- admin error logs
- exchange connection

shows the project is thinking about trust and operations, not only trading logic.

---

## Remaining Risks

### 1. Strategy edge is still not proven

This remains the deepest unresolved risk.

Better architecture does not compensate for an unprofitable strategy.

The docs are stronger, but there is still no evidence in the reviewed material that the strategy has been validated under realistic live-like assumptions.

### 2. Some critical controls are requirements, not yet designed subsystems

The project now explicitly requires:

- reconciliation
- circuit breakers
- kill switches
- paper trading

That is good.

But in several cases they still read more like gates on a checklist than like fully described subsystems with data flow, ownership, and operational procedures.

The project has improved from omission to acknowledgment. The next step is design completeness.

### 3. Scope pressure is rising

The project is now stronger, but also much broader.

You are simultaneously designing:

- a trading engine
- a multi-tenant SaaS
- a strategy research platform
- advanced debugging tooling
- AI explanation features
- multiple deployment/business models

That is a lot of surface area.

The risk is no longer underthinking. It is trying to industrialize too many layers before the core loop is proven.

---

## What I Would Change Next

### 1. Decide the default operating model

This is the highest-leverage product decision still open.

Pick the default between:

- Option B as the standard product
- Option C as the standard product
- or an explicitly staged hybrid model

Then refactor onboarding, exchange connection, legal framing, and operational design around that choice.

### 2. Replace the current development plan

The current roadmap should be rewritten to reflect the actual maturity of the architecture.

Recommended build order:

1. historical data ingestion and storage
2. deterministic backtester with harsh assumptions
3. paper trading mode
4. reconciliation and recovery controls
5. kill switch, circuit breaker, and alerting
6. live execution
7. minimal API and essential UI
8. broader product UI and advanced features

### 3. Make the product flow explicitly Backtest → Paper → Live

This should show up in:

- onboarding
- strategy activation UX
- dashboard state
- admin oversight

If live trading is a promotion step instead of the default next click, the product will better reflect the operational philosophy already present in the docs.

### 4. Give intervention tooling the same visibility as monitoring

Add explicit product support for:

- emergency flatten
- per-user pause
- global kill switch
- reconciliation status
- degraded-mode handling

These are core trust features for operators and admins.

### 5. Keep v1 narrower than the architecture suggests

For v1, I would still constrain aggressively:

- one exchange
- one symbol
- one strategy
- one user cohort
- minimal AI impact on live behaviour
- no advanced replay debugger UI yet

The architecture can stay broad. The first implementation should not.

---

## Bottom Line

This project has moved on a lot.

The earlier review argued that the architecture was promising but underweighted the operational and validation layers that matter in production. That criticism is no longer fair in the same way. Those layers are now present in the design and treated seriously.

The project is now much closer to a coherent product architecture.

The main thing holding it back is not missing awareness of the hard parts. It is that the roadmap, deployment model, and user flow have not fully caught up with the stronger architecture you now have.

If you resolve those alignment problems early, this has a credible path from well-structured concept to robust trading product.