# 3Commas Competitive Review

## What is 3Commas?

3Commas is a cloud-hosted crypto trading automation platform founded in 2017, registered in the British Virgin Islands. It connects to 14+ centralised exchanges (Binance, Bybit, OKX, Kraken, Coinbase, KuCoin, etc.) via API keys and provides a suite of pre-built bot types, backtesting, and a manual trading terminal.

They claim millions of registered traders and 134,000+ active community members.

---

## 3Commas Product Offering

| Capability | Detail |
|---|---|
| **DCA Bot** | Dollar-cost-average entries with safety orders. Multi-pair bots can monitor 100+ pairs. Most configurable bot in the platform. |
| **Grid Bot** | Classic grid trading — buy low / sell high within a range. AI Grid Bot variant auto-adjusts grid parameters. |
| **Signal Bot** | Accepts TradingView webhooks / Pine Script alerts to trigger entries, supports long & short in the same bot. |
| **Smart Trade** | Manual order enhancement — concurrent TP/SL, trailing TP, trailing SL, multi-target exits. |
| **Terminal** | Unified manual trading interface across connected exchanges. |
| **Backtesting** | 1-minute candle resolution, up to full history on Expert plan; limited to 4 months for Grid bots. |
| **Portfolio Tracking** | Asset tracking, portfolio balancing, performance reports. |
| **TradingView Integration** | Charts, signals, Pine Script execution inside the platform. |
| **Mobile App** | iOS & Android with near-desktop feature parity. |
| **Asset Management** | White-label portal, bulk bot deployment, automated client reporting. |
| **AI Assistant** | New feature across all plans — likely LLM-based help for strategy setup. |

### Pricing (Annual)

| Plan | Price | Highlights |
|---|---|---|
| Starter | $15/mo | Spot only, 1 exchange, 5 DCA bots, 100 backtests (1yr), read-only API |
| Pro | $40/mo | Spot & futures, 3 exchanges, 20 DCA bots, 500 backtests (2yr) |
| Expert | $110/mo | 15 exchanges, 1K bots each type, 5K backtests (full history), read/write API |
| Asset Manager | $223+/mo | Multi-client, white-label, batch deployments |

---

## Is 3Commas the Market Leader?

**Yes — 3Commas is one of the top 2-3 retail crypto bot platforms globally**, alongside Pionex and Bitsgap.

### Why they lead

- **First-mover advantage** — operating since 2017, significant brand recognition.
- **Breadth of exchange support** — 14+ exchanges from a single account.
- **Bot variety** — DCA, Grid, Signal, Smart Trade covers the most common retail strategies.
- **Community & ecosystem** — 134K active members, influencer partnerships, TradingView marketplace.
- **Mobile parity** — full-featured iOS/Android app.
- **Trust signals** — Trustpilot 4.3, Capterra 4.8, G2 4.7.

### Where they have weaknesses

- **Cloud-hosted / custodial risk** — users hand API keys to a third party. 3Commas suffered a high-profile API key leak in late 2022.
- **Generic strategy engine** — bots are pre-defined templates (DCA, Grid, Signal). Users configure parameters but cannot compose custom strategy logic beyond what the templates allow.
- **No true multi-timeframe strategy composition** — Grid Bot is single-range, DCA Bot is single-pair entry logic. There is no native way to combine a 4H trend filter → 1H bias → 15m entry trigger as a single coherent strategy.
- **AI is surface-level** — "AI Grid Bot" auto-tunes grid parameters, "AI Assistant" is a chatbot. Neither provides real-time LLM-driven market context or sentiment-aware position sizing.
- **Backtesting limitations** — Grid bot backtests limited to 4 months even on Expert plan. No shared execution engine between backtest and live trading is advertised.
- **Subscription ceiling** — power users hit plan limits and pay $110+/mo. No self-hosted option to eliminate recurring costs.
- **No Hyperliquid support** — exchange list is focused on CEXes. On-chain / DEX perpetuals (Hyperliquid, dYdX, GMX) are not supported.

---

## Comparison: 3Commas vs This Project

| Dimension | 3Commas | AI Grid Trading System |
|---|---|---|
| **Hosting model** | Cloud SaaS (third-party holds API keys) | Cloud SaaS (subscribers connect their own Hyperliquid keys, platform trades on their behalf). Same trust model as 3Commas. |
| **Strategy model** | Pre-built bot templates with parameter knobs | Plugin-based strategy engine with composable pipeline (ITradingStrategy interface) |
| **Multi-timeframe** | Not natively supported in single bot | Core architecture: 4H trend → 1H bias → 15m entry in one strategy |
| **Grid trading** | Simple range grid, AI auto-tune | Full grid lifecycle state machine with hedge protection and take-profit management |
| **AI integration** | AI Grid Bot (auto-params), AI chatbot assistant | LLM-driven market context — sentiment, macro regime, event risk feed into strategy as modifiers (not direct trade signals) |
| **Backtesting** | 1-min candle, capped by plan tier | Replay engine sharing the same StrategyEngine, GridController, RiskEngine as live — true parity by design |
| **Risk engine** | Basic per-bot TP/SL/trailing | Dedicated RiskEngine layer in the pipeline applied consistently across all strategies |
| **Exchange support** | 14+ CEXes | Hyperliquid (DEX perps) — no CEX middleman, on-chain settlement |
| **Execution model** | Tick/price-based triggers | Deterministic candle-close execution — eliminates partial-candle noise, ensures backtest ≡ live |
| **Hedge protection** | Not built-in | Native short hedge on confirmed breakdown within grid strategy |
| **Cost** | $15–$110+/mo recurring | Subscription-based — pricing TBD. Competes directly on value: deeper strategy logic, AI context, and execution quality for a comparable or lower price point. |
| **Mobile app** | Full-featured iOS/Android | Dashboard UI (web), no native mobile yet |
| **Community / ecosystem** | 134K members, influencer network, Pine Script marketplace | Solo/small-team project, no marketplace |

---

## Key Differentiators We Can Exploit

### 1. LLM-Augmented Strategy Context (Biggest Moat)
3Commas' "AI" is parameter auto-tuning and a chatbot. This project integrates LLM output as a first-class market context signal — sentiment, macro regime, event risk — that modifies strategy behaviour (grid sizing, position sizing, entry gating). No retail bot platform does this natively today. This is a genuine differentiator.

### 2. Deterministic Candle-Close Execution
Most retail bots (3Commas included) execute on real-time price ticks. This creates divergence between backtests and live performance. Candle-close-only execution guarantees that what you backtest is what you trade — a strong trust argument for serious traders.

### 3. True Multi-Timeframe Strategy Composition
3Commas bots operate on a single timeframe per bot. This project's pipeline natively chains 4H → 1H → 15m into one coherent strategy with trend filtering, bias confirmation, and pullback entry. This is closer to how professional discretionary traders actually think.

### 4. Hyperliquid / DEX-Native
3Commas does not support Hyperliquid or on-chain perpetual DEXes. As DeFi perps grow in volume, supporting Hyperliquid natively is an early-mover advantage in an underserved segment.

### 5. Backtest-Live Parity by Architecture
The backtesting engine reuses the same StrategyEngine, GridController, and RiskEngine as live trading, swapping only the data source and execution engine. 3Commas does not advertise this level of code-sharing. This matters for traders who want reproducible results.

### 6. Grid Lifecycle State Machine + Hedge Protection
3Commas grid bots are range-bound buy/sell. This project tracks grid state through a formal lifecycle (Idle → GridPending → GridActive → ...) and adds automated hedge opening on confirmed breakdowns — features pro traders want but retail tools don't offer.

---

## What 3Commas Does Better (Gaps to Acknowledge)

| Gap | Impact |
|---|---|
| **Multi-exchange support** | 3Commas supports 14+ exchanges out of the box. This project is Hyperliquid-only initially. |
| **Mobile app** | 3Commas has full iOS/Android apps. This project has a web dashboard only. |
| **Community & social proof** | 134K members, Trustpilot reviews, influencer network. This project has none yet. |
| **Ease of onboarding** | 3Commas is sign-up-and-go with 14+ exchanges. This project requires a Hyperliquid wallet and key connection. |
| **Bot templates & marketplace** | Copy-bot, preset sharing, Pine Script marketplace lowers barrier for beginners. |
| **Portfolio management** | Built-in portfolio tracking and balancing. Not in scope for this project yet. |

---

## Positioning Summary

3Commas is the Swiss Army knife for retail crypto traders who want breadth, convenience, and a low barrier to entry. It optimises for the mass market.

This project is a scalpel — purpose-built for serious traders who want:
- deeper strategy logic (multi-timeframe, hedge-aware grids)
- AI-driven market awareness (not just auto-tuned parameters)
- execution they can trust (deterministic, backtest = live)
- Hyperliquid-native DEX trading that 3Commas doesn't support

The differentiator is **depth over breadth** — fewer exchanges, fewer bot types, but dramatically more sophisticated strategy composition, AI integration, and execution guarantees than any retail platform offers today.

### One-Liner Positioning
> "The AI-augmented trading engine for Hyperliquid traders who've outgrown 3Commas."