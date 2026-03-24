# PBI Specification: F1 — Configuration & Connectivity

**Date:** 2026-03-24  
**Author:** PRD Agent  
**Status:** Draft  
**PRD:** [hyperliquid-poc-prd.md](../../prd-approved/hyperliquid-poc-prd.md)  
**Implementation Phase:** 1 (Foundation)  
**Risk Level:** Low

---

## Summary

Configure a Hyperliquid testnet wallet and verify end-to-end connectivity from .NET backend through to the Angular frontend.

### User Story

> As a **developer**, I want to **configure my Hyperliquid testnet wallet and verify connectivity** so that **I can confirm the foundation is working before building on top of it**.

### Business Value

This is the foundation for all subsequent features. Without verified connectivity and key derivation, no other POC work can proceed.

---

## Requirements

### Functional Requirements

- [ ] Private key loaded from `appsettings.json` or environment variable
- [ ] Wallet address derived automatically from private key using Nethereum
- [ ] Health check endpoint (`GET /api/health`) pings Hyperliquid API and returns connectivity status
- [ ] Angular UI displays connection status (connected / disconnected / error)
- [ ] Angular UI displays truncated wallet address (e.g. `0x1a2b...3c4d`)

### Non-Functional Requirements

- [ ] Private key must not be committed to git (`.gitignore` entry for local config overrides)
- [ ] Health check responds within 5 seconds

---

## User Flow

### Happy Path

1. Developer adds private key to `appsettings.Development.json` or sets environment variable
2. Developer runs `dotnet run` to start the backend
3. Developer runs `ng serve` to start the frontend
4. Developer opens browser to Angular app
5. UI shows green "Connected" badge and truncated wallet address
6. Developer can hit `GET /api/health` directly and see JSON status response

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Private key missing from config | Backend logs clear error on startup; health check returns "not configured" |
| Private key malformed | Backend logs error with guidance; health check returns error status |
| Hyperliquid testnet unreachable | Health check returns "disconnected" with error detail; UI shows red status |
| CORS misconfiguration | Angular cannot reach API; browser console shows CORS error (developer must fix config) |

---

## Technical Considerations

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/health` | Pings Hyperliquid testnet API; returns connectivity status and wallet address |

### Key Components

| Component | Action |
|-----------|--------|
| `HyperliquidOptions` | Config model bound from `appsettings.json` — holds private key, testnet endpoint URL |
| `HyperliquidSigner` | Derives wallet address from private key using Nethereum |
| `HyperliquidRestClient` | Makes a lightweight API call to verify testnet reachability |
| `Program.cs` | Registers services, configures CORS for Angular dev server |

### Configuration Shape

```json
{
  "Hyperliquid": {
    "PrivateKey": "<testnet-private-key>",
    "BaseUrl": "https://api.hyperliquid-testnet.xyz"
  }
}
```

---

## Out of Scope

- Key encryption or secure storage (plaintext config is acceptable for testnet)
- Multiple wallet support
- Angular authentication
- Docker or deployment configuration

---

## Open Questions

*None at this time.*

---

## Acceptance Criteria

- [ ] Private key is loaded from config and wallet address is derived correctly
- [ ] `GET /api/health` returns connectivity status (connected/disconnected) and wallet address
- [ ] Angular UI displays connection status badge (green/red)
- [ ] Angular UI displays truncated wallet address
- [ ] Missing or invalid private key produces a clear error, not a crash
- [ ] Private key is excluded from git via `.gitignore`

---

## Related Features

- All subsequent PBIs depend on F1 being complete
