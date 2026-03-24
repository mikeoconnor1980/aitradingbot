# PBI Specification: F1 — Configuration & Connectivity

**PBI ID:** Draft
**Status:** Draft
**Iteration:** Backlog
**Created:** 2026-03-24
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)
**Implementation Phase:** 1 (Foundation)
**Risk Level:** Low
**Depends On:** None

---

## Summary

Configure a Hyperliquid testnet wallet and verify end-to-end connectivity from .NET backend through to the Angular frontend. The health endpoint focuses exclusively on Hyperliquid testnet reachability — no other infrastructure checks are in scope.

### User Story

> As a **developer**, I want to **configure my Hyperliquid testnet wallet and verify connectivity** so that **I can confirm the foundation is working before building on top of it**.

### Business Value

This is the foundation for all subsequent features. Without verified connectivity and key derivation, no other POC work can proceed.

---

## Problem Statement

Without verified connectivity and a working key derivation pipeline, no other POC feature can proceed. This PBI establishes the foundation by configuring the testnet wallet, deriving the wallet address, and proving that the backend can reach the Hyperliquid testnet API.

---

## Requirements

### Functional Requirements

- [ ] Private key loaded from configuration using standard .NET config hierarchy (`appsettings.json` < `appsettings.Development.json` < environment variables)
- [ ] Application fails fast on startup if private key is missing or malformed, with a clear error message
- [ ] Wallet address derived automatically from private key using Nethereum
- [ ] Health check endpoint (`GET /api/health`) calls Hyperliquid public `/info` endpoint (meta/exchange info) to verify testnet reachability
- [ ] Health check returns structured JSON: `status`, `walletAddress`, `network`, `timestamp`, and optional `error`
- [ ] Angular UI displays a status card component showing: connection status (green/red), truncated wallet address (e.g. `0x1a2b...3c4d`), network name (testnet), and a manual refresh button
- [ ] Angular UI auto-polls the health endpoint every 10 seconds in the background
- [ ] Manual refresh button triggers an immediate health check outside the polling cycle

### Non-Functional Requirements

- [ ] Private key must not be committed to git (`.gitignore` entry for `appsettings.Development.json` and any local config overrides)
- [ ] Health check responds within 5 seconds
- [ ] CORS configured to allow Angular dev server (`http://localhost:4200`)

---

## User Flow

### Happy Path

1. Developer adds private key to `appsettings.Development.json` or sets `Hyperliquid__PrivateKey` environment variable
2. Developer runs `dotnet run` to start the backend
3. Developer runs `ng serve` to start the frontend
4. Developer opens browser to Angular app
5. UI shows a status card with green "Connected" indicator, truncated wallet address, and "Testnet" network label
6. Status card auto-refreshes every 10 seconds; developer can also click refresh manually
7. Developer can hit `GET /api/health` directly and see structured JSON response

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Private key missing from config | Application throws on startup with clear error message indicating the missing configuration key |
| Private key malformed | Application throws on startup with error message including guidance on expected format |
| Hyperliquid testnet unreachable | Health check returns `"status": "disconnected"` with error detail; UI status card shows red indicator |
| CORS misconfiguration | Angular cannot reach API; browser console shows CORS error (developer must fix config) |

---

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/health` | Calls Hyperliquid public `/info` endpoint; returns connectivity status, wallet address, network, and timestamp |

#### Response Shape

```json
{
  "status": "connected",
  "walletAddress": "0x1a2b...3c4d",
  "network": "testnet",
  "timestamp": "2026-03-24T12:00:00Z",
  "error": null
}
```

Error example:

```json
{
  "status": "disconnected",
  "walletAddress": "0x1a2b...3c4d",
  "network": "testnet",
  "timestamp": "2026-03-24T12:00:05Z",
  "error": "Hyperliquid testnet API did not respond within 5 seconds"
}
```

### Key Components

| Component | Action |
|-----------|--------|
| `HyperliquidOptions` | Config model bound from `Hyperliquid` section — holds private key and testnet base URL |
| `HyperliquidSigner` | Derives wallet address from private key using Nethereum |
| `HyperliquidRestClient` | Calls Hyperliquid public `/info` endpoint to verify testnet reachability |
| `Program.cs` | Validates config on startup (fail fast), registers services, configures CORS for Angular dev server |

### Configuration Shape

```json
{
  "Hyperliquid": {
    "PrivateKey": "<testnet-private-key>",
    "BaseUrl": "https://api.hyperliquid-testnet.xyz"
  }
}
```

Environment variable equivalent: `Hyperliquid__PrivateKey`, `Hyperliquid__BaseUrl`

---

## Out of Scope

- Key encryption or secure storage (plaintext config is acceptable for testnet POC)
- Multiple wallet support
- Angular authentication
- Docker or deployment configuration
- Database health checks or other infrastructure verification
- WebSocket connectivity checks

---

## Open Questions

*None at this time.*

---

## Acceptance Criteria

- [ ] **Given** a valid private key in config, **When** the application starts, **Then** the wallet address is derived correctly and logged on startup
- [ ] **Given** no private key in config, **When** the application starts, **Then** it throws a clear startup error indicating the missing configuration
- [ ] **Given** a malformed private key in config, **When** the application starts, **Then** it throws a clear startup error with format guidance
- [ ] **Given** a running backend with valid config, **When** `GET /api/health` is called, **Then** it returns structured JSON with `status`, `walletAddress`, `network`, `timestamp`, and `error` fields
- [ ] **Given** the Hyperliquid testnet is reachable, **When** `GET /api/health` is called, **Then** `status` is `"connected"` and `error` is `null`
- [ ] **Given** the Hyperliquid testnet is unreachable, **When** `GET /api/health` is called, **Then** `status` is `"disconnected"` and `error` contains a descriptive message
- [ ] **Given** the Angular UI is loaded, **When** the page renders, **Then** a status card displays the connection status (green/red), truncated wallet address, and network name
- [ ] **Given** the Angular UI is loaded, **When** 10 seconds elapse, **Then** the health endpoint is polled automatically and the status card updates
- [ ] **Given** the Angular UI is loaded, **When** the user clicks the refresh button, **Then** the health endpoint is called immediately and the status card updates
- [ ] **Given** the project repository, **When** checking `.gitignore`, **Then** `appsettings.Development.json` is excluded from version control

### Release Notes Information

- **Heading**: Hyperliquid Testnet Configuration & Connectivity
- **Release note type**: Feature
- **Release Note Summary**: Configure a Hyperliquid testnet wallet, derive the wallet address, and verify connectivity via a health check endpoint with an Angular status card.
- **Release Notes Audience**: Product
- **Breaking Change**: No

---

## Related Features

- **F2** — Account dashboard depends on the connectivity established here
- **F3** — Market data REST depends on the `HyperliquidRestClient` created here
- **F4** — WebSocket streaming depends on the base URL and connectivity proven here
