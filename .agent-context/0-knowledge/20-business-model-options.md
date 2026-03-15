# Business Model Options

This document scopes three possible subscription models for delivering the platform to paying users.
No decision has been made. All options are being evaluated.

---

# Option A — Self-Hosted (User Deploys the Bot)

Subscribers receive a deployable package (Docker image or similar).
They run the bot on their own VPS or infrastructure.
They provide their own Hyperliquid wallet keys locally — keys never leave their machine.

## How It Works

Subscriber signs up and pays subscription.
Platform provides a Docker image or deployment package.
Subscriber deploys to their own VPS.
Subscriber configures Hyperliquid wallet key locally.
Bot connects to Hyperliquid and trades autonomously.
Platform provides strategy updates and LLM context via API.

## Advantages

### Key custody stays with the user
The strongest trust argument. Users never share private keys with a third party.
Eliminates the risk category that damaged 3Commas in 2022.

### Reduced platform liability
If a user's keys are compromised, it is their infrastructure problem, not yours.
No regulatory grey area around holding customer trading credentials.

### Lower infrastructure cost per user
Users pay for their own compute. Platform only hosts the subscription portal,
strategy distribution, and LLM context API.

### Attracts high-value users
Serious traders and institutions prefer self-custody.
Willingness to run a VPS signals a higher-value, more committed customer.

### Simpler compliance
Not holding third-party trading keys removes a significant regulatory surface area.

## Disadvantages

### High barrier to entry
Users must be capable of deploying Docker containers on a VPS.
This excludes the majority of retail crypto traders.

### Support burden
Every user has a different VPS provider, OS, network configuration.
Debugging user-specific deployment issues is expensive.

### Version fragmentation
Users may run outdated versions of the bot.
Rolling out updates requires users to manually pull and redeploy.
Cannot guarantee all users are running the latest strategy logic or security patches.

### Limited visibility
Platform has no direct visibility into whether bots are running,
what errors users encounter, or how strategies are performing across the subscriber base.

### Piracy / key sharing risk
Docker images can be shared or reverse-engineered.
Subscription enforcement relies on licence key validation that can be circumvented.

### LLM context delivery complexity
If the platform provides LLM sentiment/context as a service,
self-hosted bots must call back to the platform API.
This adds latency, requires authentication, and creates a dependency.

---

# Option B — Platform-Hosted (3Commas Model)

The platform runs the entire trading system in the cloud.
Subscribers connect their Hyperliquid wallet keys via the UI.
The platform holds user keys and executes trades on their behalf.

## How It Works

Subscriber signs up and pays subscription.
Subscriber connects Hyperliquid wallet key via the dashboard.
Platform encrypts and stores the key (Key Vault in Azure phase).
Platform runs strategy evaluation and order execution using the subscriber's keys.
Subscriber monitors performance via the dashboard.

## Advantages

### Low barrier to entry
Sign up, connect key, activate — no VPS, no Docker, no deployment.
Accessible to the broadest possible market.

### Full control over user experience
Platform controls the entire runtime — versioning, updates, monitoring.
Every user is always on the latest version.

### Centralised monitoring and support
Platform sees all bots, all errors, all performance metrics.
Support can diagnose issues directly without relying on user-side debugging.

### Shared market data infrastructure
Market data streams, indicator calculations, and LLM context are shared.
Only strategy evaluation and execution are per-user.
Efficient use of compute and API connections.

### Simpler onboarding funnel
No deployment documentation, no VPS guides, no version management.
Reduces churn from users who fail at setup.

### Revenue predictability
Direct control over the service means churn is driven by value perception,
not by deployment difficulty or version drift.

## Disadvantages

### You hold user keys — liability and trust
The platform stores subscriber trading keys.
A breach exposes all users simultaneously (the 3Commas 2022 scenario).
This is the single biggest risk of this model.

### Regulatory exposure
Holding trading credentials and executing trades on behalf of others
may trigger financial services regulations depending on jurisdiction.
Legal review is required.

### Infrastructure cost scales with users
Every active subscriber adds compute, database, and API load.
Costs scale linearly (or worse) with subscriber count.

### Key security is your problem
Encryption at rest, access controls, audit logging, breach response —
all of this must be built, maintained, and proven trustworthy.

### Single point of failure
Platform downtime = all subscribers stop trading simultaneously.
A bug in a shared component (strategy engine, risk engine) affects everyone.

### Rate limiting at scale
Hyperliquid API rate limits apply across all subscribers.
At scale, order queuing and throttling become critical.

---

# Option C — Split Architecture (Cloud Brain + User Execution Agent)

The platform runs all strategy logic centrally in the cloud.
Subscribers run a lightweight Docker container (the "execution agent") on their own VPS.
The agent holds the private key, receives approved signals from the cloud, signs orders,
and submits them to Hyperliquid.

Keys never leave the subscriber's machine.

## Architecture

Cloud (run by platform):

Market data WebSocket (shared)
Indicator calculation
Strategy evaluation (per-user)
LLM context and sentiment
Risk engine
Signal generation
Dashboard / API
Subscription management

User's execution agent (Docker on user's VPS):

Holds subscriber's Hyperliquid private key
Receives approved trading signals from cloud
Signs and submits orders to Hyperliquid
Subscribes to user-specific WebSocket events (fills, position updates)
Reports execution acknowledgements back to cloud

## Signal Flow

Cloud generates approved signal (e.g. DeployGrid)
↓
Signal sent to user's agent via authenticated channel (WebSocket or webhook)
↓
Agent receives signal
↓
Agent signs order using local private key
↓
Agent submits order to Hyperliquid
↓
Agent reports execution result back to cloud
↓
Cloud updates dashboard, state, and audit log

## What the Existing Architecture Already Provides

The Signal Contracts (DeployGrid, CancelGrid, TakeProfit, OpenHedge, etc.)
already define a clean boundary between "decision" and "execution."

The pipeline splits naturally:

Cloud: MarketData → Indicators → Strategy → Signals → RiskEngine → approved signals
Agent: approved signals → ExecutionEngine → Hyperliquid

This is the same pipeline, cut at the point where signals have been approved by the risk engine.

## WebSocket Ownership

Market data streams (candles, orderbook, trades):
Cloud. Shared across all users. One connection.

User event streams (fills, order updates, position changes):
User's agent. The agent authenticates with the user's key.
This gives the agent real-time local feedback on execution.
The agent relays relevant events back to the cloud for dashboard display.

## Advantages

### Key custody stays with the user
Same trust benefit as Option A. Keys never touch the platform.
Eliminates the 3Commas breach risk entirely.

### Strategy logic is centralised and always current
Unlike Option A, the user does not run the strategy engine.
Every subscriber gets the latest strategy logic, LLM context, and risk rules
without needing to update their Docker image.

### Piracy is effectively eliminated
The execution agent is useless without an active cloud subscription.
It cannot generate signals — it can only execute them.
Sharing the agent Docker image gives no value.

### Low regulatory exposure
Platform never holds trading keys.
The platform generates signals; the user's infrastructure executes trades.
This is more analogous to a signal service than a trading service.

### Distributed rate limiting
Each user's agent submits orders independently to Hyperliquid.
API rate limits do not concentrate on a single platform IP.

### Lightweight agent = low user burden
The agent does not run strategy logic, indicators, or market data processing.
It is a thin process: receive signal, sign, submit, report back.
Target footprint: single container, minimal RAM.

### Cloud compute is shared efficiently
Market data, indicators, and LLM context are computed once and shared.
Per-user cost is only the strategy evaluation and signal dispatch — lightweight.

## Disadvantages

### Still requires user to run a VPS
Lower barrier than Option A (no full bot deployment, just a thin agent),
but still requires Docker and a running machine.
Not as frictionless as Option B.

### Signal delivery latency
Cloud → agent adds network latency between signal generation and order execution.
For candle-close strategies (15m minimum), this is negligible.
Would be problematic for tick-level strategies (not in scope).

### Agent availability
If the user's agent goes offline, signals cannot be executed.
The cloud must handle this gracefully:
- detect agent offline
- queue or discard signals based on expiry
- alert the user
- pause strategy if agent is unreachable for too long

### Relay security
The channel between cloud and agent must be authenticated and encrypted.
Options: mutual TLS, HMAC-signed webhooks, authenticated WebSocket.
Replay attacks and signal tampering must be prevented.

### Two deployment surfaces
Cloud infrastructure + agent Docker image.
More moving parts than Option B, though less than Option A.

### Execution reconciliation
Cloud sends a signal but doesn't directly observe the Hyperliquid response.
The agent must report back execution results.
The cloud must reconcile expected state vs reported state
and handle cases where the agent reports success but the order actually failed (or vice versa).

---

# Comparison Summary

| Dimension | Option A (Self-Hosted) | Option B (Platform-Hosted) | Option C (Split Architecture) |
|---|---|---|---|
| Key custody | User holds keys | Platform holds keys | User holds keys |
| Barrier to entry | High (full VPS + Docker) | Low (sign up + connect) | Medium (lightweight Docker only) |
| Target market | Technical / high-value traders | Broad retail market | Moderate technical comfort |
| Platform liability | Low | High | Low |
| Infrastructure cost | Low (user pays compute) | High (scales with users) | Medium (cloud brain shared, user pays execution) |
| Version control | Fragmented (full bot) | Centralised | Mostly centralised (strategy logic in cloud) |
| Support burden | High (diverse environments) | Lower (single environment) | Medium (agent is thin, less to go wrong) |
| Monitoring | Blind to user issues | Full visibility | Good (cloud sees signals + acks, user runs execution) |
| Regulatory risk | Lower | Higher | Lower (no key custody) |
| Revenue ceiling | Limited by technical market size | Larger addressable market | Medium-large (lower barrier than A) |
| Security posture | User's responsibility | Platform's responsibility | Split (cloud secures strategy, user secures keys) |
| Piracy risk | Docker image sharing (full bot) | None | Low (agent is useless without cloud subscription) |
| Rate limiting | User's problem | Platform's problem | Distributed (each user hits Hyperliquid independently) |

---

# Hybrid Possibility

Option B and Option C could coexist:

Platform-hosted (Option B) as the default for users who want zero setup.  
Split architecture (Option C) as a premium tier for users who demand key custody.

Both share the same cloud brain. The difference is only whether execution
happens in the cloud or in the user's agent.

---

# Competitive Precedent for Option C

No crypto bot platform currently offers the Option C split architecture as a managed product.
However, the pattern is well-established in traditional finance and partially exists in crypto.

## Traditional Finance (Proven Precedent)

MetaTrader Signals (MT4/MT5):
Strategy provider runs logic remotely. Signals relay to the subscriber's locally-running
MT4/MT5 terminal, which holds broker credentials and executes locally.
This is architecturally almost identical to Option C.
Running successfully for over a decade with millions of users.

cTrader Copy:
Same pattern — cloud strategy, local execution via user's broker connection.

NinjaTrader + signal services:
Third-party signal providers push to NinjaTrader running locally
with user's broker credentials.

Collective2:
Strategy marketplace where signals are generated centrally and subscribers
auto-execute via their own brokerage accounts.

## Crypto Bots (No Direct Equivalent)

3Commas, Bitsgap, Cryptohopper:
Full cloud (Option B). User hands keys to platform. No local agent.

Freqtrade, Hummingbot:
Full self-hosted (Option A). User runs everything locally. No cloud brain.

OctoBot:
Offers both cloud and self-hosted, but each is a complete instance.
Not a split architecture.

TradingView + webhook bots:
TradingView generates alerts in the cloud, users run a local bot to receive
webhooks and execute. Conceptually similar to Option C but not a managed product —
it is duct-taped together by the user.

Cornix:
Receives signals from Telegram groups and executes via exchange keys.
But Cornix holds the keys (Option B model).

## Why Nobody Does It in Crypto Yet

The crypto bot market is younger and gravitates toward the simpler extremes
(full cloud or full self-hosted).

CEX API key models are simpler than wallet-based signing — cloud execution
is straightforward, so there was less pressure to split.

Hyperliquid's wallet-based signing (where the private key IS the trading authority)
makes the split more natural than CEX API keys.

## Strategic Opportunity

Being first to offer this model for crypto — especially for Hyperliquid where
the private key concern is more acute than CEX API keys — is a genuine differentiator.

MetaTrader's signal model provides proven precedent that the architecture works at scale.
The positioning becomes: "we brought the MetaTrader signal model to DeFi perps."

---

# Open Questions

- What is the regulatory position on holding subscriber trading keys in the target jurisdiction?
- What is the addressable market size for each option?
- What is the acceptable infrastructure cost per subscriber for Option B?
- Is the hybrid model (B + C) worth the engineering overhead at this stage?
- What insurance or indemnification is needed if the platform holds keys (Option B)?
- How lightweight can the Option C execution agent be? (target: single container, < 128MB RAM)
- What is the latency budget for cloud → agent signal delivery?
- How does the agent authenticate with the cloud? (API key, JWT, mutual TLS)
- What happens when the user's agent goes offline? (queue signals? pause strategy? alert user?)
