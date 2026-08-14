# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

TradePilot serves technically confident traders who need to understand account health, market conditions, strategy behaviour and safe next actions quickly, often while markets are moving.

## Product Purpose

TradePilot is an AI-native trading intelligence, strategy, research and execution platform. It owns the deterministic market, account, strategy, risk and execution capabilities that its Angular UI, APIs and AI interfaces consume.

The Overview succeeds when a user can answer four questions without interpreting a wall of equally weighted widgets:

1. What is my account worth and how is it performing?
2. Is anything unsafe or stale?
3. What is happening in the market and how does it affect me?
4. What requires attention or a decision?

## Positioning

TradePilot produces facts; AI interprets facts; adapters transport capabilities; the exchange executes authorised operations. AI is an evidence-led interface over TradePilot, not the source of trading truth.

## Operating Context

The web application is an operational trading workstation. Users monitor equity, margin, drawdown, portfolio heat, positions, orders, activity and market context; research and test strategies; and perform explicitly confirmed execution tasks.

Hyperliquid is the primary exchange today, while the product remains exchange-agnostic where practical. Account and execution state may update frequently and can become partial or stale, so freshness and degraded states are first-class information.

## Capabilities and Constraints

- Angular, REST, AI and MCP interfaces must reuse application capabilities rather than reproduce business logic.
- Indicators, PnL, risk, strategy rules, sizing and exchange state are deterministic application concerns.
- AI provides reasoning, explanation, summarisation and orchestration over structured evidence.
- Existing trading calculations, risk thresholds, order semantics, entitlements and confirmation safeguards are safety boundaries.
- Initial Analyst and explainability experiences are read-only. Privileged operations require explicit permission, audit and confirmation boundaries.
- The browser must not infer strategy decisions or invent metrics that current contracts do not provide.

## Brand Commitments

- Name: TradePilot.
- Voice: concise, calm, precise and operational. Prefer direct labels and recoverable error language over hype.
- Identity: restrained dark teal with semantic use of colour.
- Anti-references: neon crypto-casino styling, decorative glow, gratuitous glassmorphism, untraceable AI claims and motion that competes with live data.

## Evidence on Hand

- Current product implementation and Angular UI in this repository.
- Product vision, architecture, explainability and codebase-map documents supplied for this milestone.
- Existing account, risk, position, order, activity and market-context contracts.
- No approved customer claims, performance claims or additional strategy metrics may be fabricated.

## Product Principles

1. Deterministic evidence precedes interpretation.
2. Safety and execution authority are explicit.
3. Account health and required attention lead the interface.
4. Existing capabilities are reused across every interface.
5. Operational clarity outranks decoration.

## Accessibility & Inclusion

Critical monitoring and execution controls must be keyboard operable, visibly focused and screen-reader named. State is communicated with language or iconography as well as colour. Functional text targets at least 12px, reduced-motion preferences are respected, and mobile layouts avoid page-level horizontal overflow.
