# Hyperliquid Integration

The platform interacts with Hyperliquid using:

- REST API
- WebSocket streams

Capabilities:

- order placement (per-user)
- order cancellation (per-user)
- position monitoring (per-user)
- market data streaming (shared)

---

# Authentication

Hyperliquid uses wallet-based signing.

Each request includes:

action  
nonce  
signature

Since the platform is multi-tenant, each subscriber provides their own wallet private key.

Private keys are encrypted at rest and stored per-user in the database.
In the Azure phase, keys are stored in Azure Key Vault.

The platform signs trading actions on behalf of each subscriber using their key.

---

# Multi-Tenant Connection Model

Market data streams (trades, candles, orderbook) are shared across all users.
These do not require per-user authentication.

User-specific streams (fills, order updates, position changes) require
per-user WebSocket subscriptions or polling.

The worker must manage connections for all active subscribers.

---

# WebSocket Streams

Shared streams:

- trades
- candles
- orderbook

Per-user streams:

- fills
- order updates
- position changes

---

# Reconnection

The worker must support:

automatic reconnect  
state recovery

After reconnect:

sync open orders (per-user)  
sync positions (per-user)

---

# Rate Limiting

With multiple subscribers, the platform must respect Hyperliquid API rate limits.

Order submissions should be queued and throttled to stay within limits.
Market data streams are shared and do not multiply with user count.