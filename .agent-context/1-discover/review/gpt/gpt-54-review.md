# Project Viability Review

## Overall View

This project is viable as a research and prototyping effort.

The high-level architecture is directionally good:

- deterministic candle-close execution
- clear separation between strategy, risk, position, and execution
- explicit backtesting intent
- constrained use of AI as context rather than trade generation

As a live trading system for real capital, it is not ready yet.

The main issue is not the strategy idea or technology choice. The issue is that the current plan underweights the operational and validation layers that determine whether a trading bot survives real exchange conditions.

## What Looks Strong

### 1. Deterministic scheduling

Running strategies on confirmed candle closes is the right default for a system that wants reproducible behaviour between live trading and backtesting.

### 2. Good separation of concerns

The split between StrategyEngine, RiskEngine, PositionManager, and ExecutionEngine is a strong design choice. That structure gives you a realistic path to testing, extension, and safer enforcement of trading rules.

### 3. Sensible handling of AI

The AI/LLM layer is positioned as a context modifier rather than an order generator. That is the right boundary.

### 4. Strategy configuration model

Config-driven strategies are a good fit for backtesting, version comparison, and later parameter sweeps.

## Main Risks

### 1. The roadmap is missing the hard parts

The current development plan focuses on setup, database, strategy plugin, worker, API, and UI. It does not explicitly prioritize:

- reconciliation
- paper trading
- rollout safety
- exchange state recovery
- alerting
- kill switches
- validation discipline

Those are the systems that matter most before live trading.

### 2. Backtesting is likely to be too optimistic in v1

The proposed backtest approach uses candle-based fill assumptions such as filling a limit order whenever candle low crosses the order price.

That is acceptable for very early research, but it is too optimistic for a leveraged perp grid strategy where the outcome is heavily affected by:

- spread
- queue position
- partial fills
- funding
- slippage
- path dependence inside the candle

If you are not careful, the backtester will validate a strategy that fails immediately in live conditions.

### 3. Exchange-state correctness is underspecified

The design mentions reconnect and state recovery, but the docs do not yet define the mechanisms that normally prevent real-money failures:

- idempotent order submission
- client order IDs
- persisted order journal
- startup reconciliation
- stuck-order detection
- partial-fill handling
- emergency flatten path

Without these, restarts and network issues become trading risk.

### 4. Infrastructure is prototype-grade, not production-grade

A VPS with Docker Compose and SQLite is fine for research.

It is not enough on its own for a resilient live system holding signing keys and real exposure. The current secret-handling description is also too light for anything beyond early experimentation.

## What I Would Change

### 1. Reorder the roadmap

I would change the build order to:

1. market data ingestion and storage
2. deterministic backtester
3. paper trading mode
4. reconciliation and safety controls
5. observability and alerting
6. live execution
7. API and UI expansion

I would not prioritize a strategy-builder UI before the system can safely validate and simulate itself.

### 2. Cut v1 scope harder

For v1, I would keep:

- one exchange
- one symbol
- one strategy
- one account
- no AI-driven runtime behaviour changes

Keep the LLM output visible in the dashboard if useful, but do not let it influence live trading until the deterministic core is proven.

### 3. Make reconciliation a first-class subsystem

Treat exchange truth and local truth as separate states that must be continuously reconciled.

This should include:

- persisted order/event journal
- replayable state reconstruction
- deterministic startup recovery
- manual kill switch
- emergency flatten workflow

### 4. Make the backtester harsher

Add conservative assumptions early:

- fees
- funding
- spread/slippage bands
- partial-fill logic where practical
- walk-forward evaluation
- paper-trade burn-in before any live capital

### 5. Tighten config safety

The strategy config model is good, but the schema should evolve from descriptive JSON into a strongly validated contract with hard operational bounds.

For example:

- spacing array must match level count
- size distribution must sum to 1
- max exposure must be bounded by exchange/account rules
- hedge settings must be internally consistent

## Bottom Line

This is a credible architecture for a trading research platform.

It is not yet a credible live trading system.

The biggest improvement is not changing the strategy idea. It is shifting effort away from UI and CRUD features and toward validation quality, reconciliation, and operational safety.

If that change is made early, the project has a much better chance of becoming something robust rather than just well-structured on paper.
