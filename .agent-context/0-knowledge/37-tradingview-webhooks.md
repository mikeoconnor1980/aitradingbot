# TradingView Webhooks

TradingView webhook support is implemented as a Pro-tier signal passthrough into the existing control-plane and worker execution pipeline. The current feature does not import TradingView strategies into TradePilot. Instead, TradingView alerts POST a small JSON payload to a user-scoped webhook URL, the API validates and maps that payload into an `AgentCommand`, and the connected execution agent places or closes trades locally.

## Architecture Overview

```text
TradingView alert
  -> POST /api/webhooks/tradingview/{token}
     -> WebhookConfig lookup + Pro entitlement check + agent resolution
        -> WebhookCommandMapper
           -> AgentCommandStore queue
              -> Worker heartbeat pulls command
                 -> AgentCheckInService executes order/close locally
```

The feature is intentionally execution-only:

- no Pine strategy import into TradePilot
- no server-side strategy state for webhook trades
- no bypass around the worker; private keys still stay on the execution agent

## Key Components

| Component | Purpose |
|---|---|
| `src/TradePilot.Api/Controllers/WebhookManagementController.cs` | Authenticated CRUD API for user webhook configs |
| `src/TradePilot.Api/Controllers/WebhookController.cs` | Public TradingView ingress endpoint, IP allowlist check, entitlement check, and command enqueue |
| `src/TradePilot.Application/Webhooks/Models/TradingViewWebhookPayload.cs` | JSON contract accepted from TradingView |
| `src/TradePilot.Application/Webhooks/Services/WebhookCommandMapper.cs` | Maps `buy`, `sell`, and `close` payloads into worker commands |
| `src/TradePilot.Application/Webhooks/Services/SymbolMapper.cs` | Normalizes TradingView tickers such as `BTCUSD.P`, `ETHUSDT`, and `BTC-PERP` into platform assets |
| `src/TradePilot.Domain/Entities/WebhookConfig.cs` | User-owned webhook token, default asset, target agent pinning, enabled state, and last-trigger time |
| `frontend/trading-ui/src/app/features/webhooks/webhooks-page.component.ts` | UI for creating webhooks and copying the public URL |
| `src/TradePilot.Worker/Services/AgentCheckInService.cs` | Executes place-order and close-position commands pulled from heartbeat |

## Preconditions

Before a TradingView alert can place a trade, all of the following must be true:

1. The user has a Pro subscription. Webhooks are entitlement-gated on both the management API and the public ingress endpoint.
2. The user has an active wallet address configured in TradePilot.
3. At least one execution agent is connected for that wallet, unless the webhook is pinned to a specific active agent.
4. The webhook config exists and is enabled.
5. TradingView account 2FA is enabled. TradingView only allows webhook alerts when 2-factor authentication is on.

## TradePilot Setup Checklist

Use the built-in webhooks page as the source of truth for the URL users paste into TradingView.

1. Go to `Settings -> Webhooks` in the Angular UI.
2. Create a webhook.
3. Enter a label that identifies the alert source, for example `BTC breakout long`.
4. Choose `Default asset` only when the TradingView alert message will not send a reliable ticker.
5. Choose `Target agent` only when the alert must route to one known machine. Leave it on auto-route when the wallet owner may reconnect with a different active agent.
6. Save the webhook and copy the generated URL.

### Default Asset Guidance

Leave `Default asset` empty when the TradingView message includes `ticker` values like:

- `BTCUSD.P`
- `ETHUSDT`
- `SOL-PERP`
- `BTC/USDT`

`SymbolMapper` strips common quote and perpetual suffixes and maps them back to the platform asset symbol.

Set `Default asset` when:

- the TradingView alert does not send a ticker at all
- the alert uses a non-standard custom label
- multiple TradingView alerts should always route to one asset regardless of chart symbol

## TradingView Alert Setup Checklist

Once the webhook exists in TradePilot:

1. In TradingView, create or edit an alert on the indicator, strategy, or price condition you want to automate.
2. Enable the `Webhook URL` option in the alert dialog.
3. Paste the exact webhook URL copied from TradePilot.
4. In the alert `Message` field, send valid JSON so TradingView posts `application/json` rather than plain text.
5. Save the alert.
6. Trigger a safe test condition and confirm the webhook's `Last triggered` time updates in TradePilot.

TradingView constraints that matter to this integration:

- requests are POST only
- valid JSON is required for `application/json`; plain text will not bind to the API payload model correctly
- only ports `80` and `443` are supported by TradingView
- TradingView cancels the request if the remote server takes longer than about 3 seconds to respond
- webhook source IPs come from TradingView's published IPv4 list, which the API allowlists in production

## Payload Contract

The public endpoint accepts this JSON shape:

| Field | Required | Notes |
|---|---|---|
| `action` | Yes | Supported values: `buy`, `sell`, `close` |
| `ticker` | Conditional | Optional when `Default asset` is configured on the webhook |
| `contracts` | Conditional | Required and positive for `buy` and `sell`; optional for `close` |
| `price` | Conditional | Required when sending a limit order |
| `orderType` | No | `market` or `limit`; defaults to `market` unless `price` is supplied |
| `stopLoss` | No | Optional stop trigger price for `buy`/`sell` |
| `takeProfit` | No | Optional take-profit trigger price for `buy`/`sell` |
| `comment` | No | Currently informational only |

## Example Messages

### Market Buy

```json
{
  "action": "buy",
  "ticker": "BTCUSD.P",
  "contracts": 0.01,
  "orderType": "market",
  "stopLoss": 101250,
  "takeProfit": 105500,
  "comment": "Breakout entry"
}
```

### Limit Sell

```json
{
  "action": "sell",
  "ticker": "ETHUSDT",
  "contracts": 0.5,
  "orderType": "limit",
  "price": 2450,
  "stopLoss": 2485,
  "takeProfit": 2360
}
```

### Full Close

Omit `contracts` to close the full open position on that asset.

```json
{
  "action": "close",
  "ticker": "SOLUSD.P"
}
```

### Partial Close

Provide `contracts` to cap the close size. The worker clamps the requested size to the current open position and sends a reduce-only market order.

```json
{
  "action": "close",
  "ticker": "ETHUSD.P",
  "contracts": 1.25
}
```

## Runtime Behaviour

### Buy / Sell

- mapped to `AgentCommandType.PlaceOrder`
- requires a positive `contracts` value
- uses `market` order type by default
- requires `price` for limit orders
- can include stop-loss and take-profit trigger prices

### Close

- mapped to `AgentCommandType.ClosePosition`
- cancels open orders on that asset first
- queries the live position on the worker
- sends a reduce-only market order in the opposite direction of the open position
- if `contracts` is omitted, closes the full position
- if `contracts` is present, closes up to that size, capped at the live position size

## Failure Modes and Troubleshooting

| Symptom | Likely Cause |
|---|---|
| `403 Forbidden` from webhook POST | User is not on Pro, or the source IP is not allowlisted in production |
| `404 Not Found` | Token is invalid, webhook was deleted, or webhook is disabled |
| `400 Invalid TradingView payload` | Missing `action`, missing `contracts` for buy/sell, unsupported action, or limit order without `price` |
| `503 Agent unavailable` | No connected execution agent matched the webhook's pinned agent or wallet |
| Webhook page loads but alert never triggers | TradingView alert was not saved with Webhook URL enabled, 2FA is off, or message is not valid JSON |

## Creating or Extending TradingView Webhooks

When extending this feature:

1. Update the payload model in `src/TradePilot.Application/Webhooks/Models/TradingViewWebhookPayload.cs`.
2. Extend command mapping in `src/TradePilot.Application/Webhooks/Services/WebhookCommandMapper.cs`.
3. Ensure the worker can execute the new command path in `src/TradePilot.Worker/Services/AgentCheckInService.cs`.
4. Keep entitlement enforcement in both webhook controllers.
5. Update the Angular webhooks page if operator setup changes.
6. Add or update API and application tests covering payload validation and command mapping.

## Related Knowledge Docs

- `29-control-plane-agent-architecture.md`
- `30-worker-execution-pipeline.md`
- `05-feature-specification.md`