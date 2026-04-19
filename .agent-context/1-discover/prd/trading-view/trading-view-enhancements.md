# PRD: TradingView Webhook Integration — Future Considerations

**Status:** Draft
**Priority:** High (extends PRD-07 MVP webhook integration)
**Date:** 2026-04-19
**Depends on:** PRD-07 MVP (webhook passthrough endpoint, management API, frontend UI)
**Depended on by:** None currently

---

## 1. Risk Engine Integration

### Problem
MVP webhook orders bypass `IRiskEngine.ValidateAsync()` — they're treated as manual orders
(same as `PlaceOrder` from the dashboard). This means drawdown circuit breakers, portfolio
heat limits, and per-symbol position caps don't apply to TV-sourced trades.

### Recommendation
Add an optional `EnforceRiskEngine` flag on `WebhookConfig` (default: true). When enabled,
webhook commands are wrapped in a risk validation step before enqueuing to `AgentCommandStore`:

- `IRiskEngine.ValidateAsync()` checks: portfolio heat, daily loss limit, max open positions, drawdown tier
- Rejected signals return 200 to TV (to prevent retry loops) but log rejection reason in `WebhookLog`
- User can disable per-webhook if they want raw passthrough (e.g., for testnet experimentation)

### Key Files
- `src/TradePilot.Application/Trading/Services/LiveRiskEngine.cs` — existing risk validation
- `src/TradePilot.Application/Webhooks/Services/WebhookCommandMapper.cs` — add risk gate before enqueue
- `src/TradePilot.Domain/Entities/WebhookConfig.cs` — add `EnforceRiskEngine` property

---

## 2. Smart Position Close

### Problem
TV `close` action currently maps to `CancelAllOrders`, which cancels open limit orders but
does NOT close an existing position. A Pine Script `strategy.close()` expects the position
to be flattened.

### Recommendation
Implement a `ClosePosition` flow:

1. Query current position size for the asset via Hyperliquid REST (`/info` endpoint)
2. If position exists, submit an opposing market order for the full position size
3. Also cancel any open limit orders for the asset (existing `CancelAllOrders`)
4. If no position exists, only cancel open orders (current behavior)

This requires a new `AgentCommandType.ClosePosition` with payload `{ Asset, ReduceOnly: true }`,
or reuse `PlaceOrder` with a `ReduceOnly` flag added to `OrderCommandPayload`.

### Key Changes
- `AgentCommandType` — add `ClosePosition` or add `ReduceOnly` to `OrderCommandPayload`
- `AgentCheckInService` — handle new command type in Worker
- `IExecutionEngine` — needs position query capability (already exists via `IHyperliquidClient`)
- `WebhookCommandMapper` — map `close` → `ClosePosition` instead of `CancelAllOrders`

---

## 3. Strategy Trigger Mode

### Problem
MVP treats TradingView as the complete strategy brain (buy/sell/close passthrough).
A more powerful integration would let TV alerts trigger TradePilot's own strategy logic —
e.g., a TV alert says "setup detected" and TradePilot deploys its grid, manages exits,
applies risk rules, and handles the full lifecycle.

### Recommendation
Add a new `StrategyMode.WebhookTrigger` that:

1. Replaces `CandleClock` as the execution trigger (webhook arrival instead of candle close)
2. Links a `WebhookConfig` to a `Strategy` via `WebhookConfig.LinkedStrategyId`
3. On webhook receipt: loads the linked strategy's `StrategyConfig`, runs
   `IStrategyEngine.EvaluateAsync()` with current market context, emits signals through
   the normal pipeline (GridController/SignalController → RiskEngine → PositionManager)
4. TV payload can optionally override parameters (e.g., direction, anchor price)

This is the "pull in strategies" capability — TV detects the setup, TradePilot manages execution.

### Architecture Impact
- New `StrategyMode.WebhookTrigger` enum value
- `WebhookConfig` gains `LinkedStrategyId` (nullable FK to Strategy)
- `StrategyScheduler` needs a `HandleWebhookTriggerAsync()` path parallel to `HandleCandleClosedAsync()`
- Frontend: webhook config dialog adds optional "Link to Strategy" selector
- Backtesting: would need a webhook event replay source (lower priority)

### Dependencies
- Requires MVP webhook infrastructure (Phases 1–6)
- Requires the strategy to be backtestable with event-driven triggers (PRD-04 extension)