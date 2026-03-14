# Hyperliquid Integration

The bot interacts with Hyperliquid using:

- REST API
- WebSocket streams

Capabilities:

- order placement
- order cancellation
- position monitoring
- market data streaming

---

# Authentication

Hyperliquid uses wallet-based signing.

Each request includes:

action  
nonce  
signature

The private key is stored securely on the server using environment variables.

---

# WebSocket Streams

The worker subscribes to:

- trades
- candles
- orderbook
- user events

User events include:

- fills
- order updates
- position changes

---

# Reconnection

The worker must support:

automatic reconnect  
state recovery

After reconnect:

sync open orders  
sync positions