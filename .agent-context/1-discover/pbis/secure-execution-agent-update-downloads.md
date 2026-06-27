# PBI Specification: Secure Execution Agent Update Downloads

**Date:** 2026-06-27
**Author:** Copilot / mdoconnor
**Status:** Draft

---

## Summary

Harden the execution-agent update download flow introduced by the installer distribution work. The current implementation protects browser installer downloads with user authentication and subscription checks, but worker update download tokens can still be minted from anonymous endpoints. The token mechanism also depends on ASP.NET Core Data Protection keys that are not persisted across Container App replicas or restarts.

This PBI makes update download token issuance agent-authenticated and makes token validation stable in Azure Container Apps.

### User Story

> As a **platform operator**, I want to **restrict execution-agent update downloads to trusted agents and subscribers** so that **private installer artifacts cannot be discovered or downloaded through anonymous control-plane endpoints**.

### Business Value

- Preserves the subscription-gated distribution model for the execution agent.
- Prevents anonymous callers from obtaining short-lived worker download URLs.
- Improves production reliability when API Container Apps scale beyond one replica or restart.
- Reduces rollout risk before broader subscriber onboarding.

---

## Requirements

### Functional Requirements

- [ ] API validates a configured agent shared secret before returning worker update download URLs from `POST /api/agent/heartbeat`.
- [ ] Production API startup fails or disables worker token issuance when agent authentication is not configured.
- [ ] Local development remains usable with an explicit development-only anonymous agent mode or documented test secret.
- [ ] Anonymous `GET /api/agent/update/latest` does not mint a worker download token.
- [ ] `GET /api/agent/update/latest` either returns metadata only or requires agent authentication before returning a download URL.
- [ ] `GET /api/agent/installer/worker-download` continues to require a valid short-lived token.
- [ ] Browser installer downloads remain authenticated and subscription-gated.
- [ ] API logs denied token-minting attempts without logging bearer secrets or token values.
- [ ] Worker continues sending its configured `Agent:SecretKey` as `Authorization: Bearer {secret}` on control-plane requests.

### Non-Functional Requirements

- [ ] Agent shared secret is treated as a server-side runtime secret and sourced from Key Vault-backed Container Apps secret references in Azure.
- [ ] Worker download token validation works across API replicas and after routine Container App restarts.
- [ ] Token lifetime remains short, currently 10 minutes unless explicitly changed.
- [ ] Token validation failure returns 401 without exposing token internals.
- [ ] No customer private keys, wallet seeds, or signing material are moved into Azure Key Vault.

---

## User Flow

### Happy Path — Authenticated Worker Update

1. Operator configures the API runtime with an agent shared secret through Key Vault-backed Container Apps secret references.
2. Operator configures the installed execution agent with the matching `Agent:SecretKey` value.
3. Worker sends `POST /api/agent/heartbeat` with `Authorization: Bearer {Agent:SecretKey}` and its current version.
4. API validates the agent secret before evaluating update metadata.
5. If a newer manifest-backed release is available, API returns `UpdateDownloadUrl` containing a short-lived worker download token and a non-empty SHA256 hash.
6. Worker downloads the installer through `installer/worker-download`, verifies SHA256, and stages/applies the update when safe.

### Happy Path — Operator Metadata Check

1. Operator calls `GET /api/agent/update/latest` for release verification.
2. API returns latest version, release notes, and SHA256 metadata.
3. API does not return a privileged worker download token unless the request is authenticated as an agent or the endpoint is explicitly changed to require agent authentication.

### Error States

| Scenario | Expected Behavior |
|----------|-------------------|
| Anonymous heartbeat requests update metadata | 401 Unauthorized or heartbeat response without privileged update URL, depending on final compatibility decision |
| Heartbeat sends wrong agent secret | 401 Unauthorized; no command or update metadata returned |
| Production API has no agent secret configured | Startup fails fast or update-token issuance is disabled with a clear health/log signal |
| Anonymous `update/latest` request when release exists | Returns metadata only; no `installer/worker-download` token is minted |
| Worker uses expired token | 401 Unauthorized from `installer/worker-download` |
| Worker token is validated by a different API replica | Token is accepted when unexpired because token keys/signing material are shared across replicas |
| Data Protection key store or token signing secret unavailable | API logs clear configuration error and does not mint update tokens |

---

## Technical Considerations

### Bounded Contexts

**Context:** Agent control plane, installer distribution, Azure runtime configuration.

### Recommended Design

Use API-side agent authentication for token issuance and persist token validation material across replicas.

1. Add API configuration for agent authentication, for example `Agent:SecretKey` or `AgentAuthentication:SharedSecret`.
2. Validate bearer shared secret on anonymous agent endpoints that need worker trust, especially heartbeat update metadata.
3. Keep dashboard/browser endpoints on normal JWT authentication and subscription checks.
4. Remove privileged download URL generation from anonymous `GET /api/agent/update/latest`, or require the same agent secret for download URL generation.
5. Persist ASP.NET Core Data Protection keys to an Azure-backed store using managed identity, or replace the token with a stateless HMAC-signed payload using a Key Vault-backed signing secret.

### Data Protection Options

| Option | Pros | Cons |
|--------|------|------|
| Persist Data Protection keys to Blob Storage | Aligns with current Blob Storage and managed identity direction; supports existing `IDataProtector` token code | Requires Blob container/key path configuration and API identity blob permissions |
| Persist Data Protection keys to Key Vault-backed storage | Centralizes secret/key operations | May add app-level Key Vault complexity beyond current Container Apps secret-reference pattern |
| Replace with HMAC-signed token | Stateless and simple across replicas | Requires careful signing/expiry implementation and secret rotation design |

Recommended initial implementation: persist Data Protection keys to a private Blob Storage location using the API managed identity. Use Key Vault only for the agent shared secret or HMAC signing secret if the design chooses stateless tokens.

### API Endpoints

| Method | Route | Auth | Expected Change |
|--------|-------|------|-----------------|
| POST | `/api/agent/heartbeat` | Agent shared secret | Validate secret before returning worker update URLs |
| GET | `/api/agent/update/latest` | Anonymous or agent shared secret | Do not mint worker download tokens anonymously |
| GET | `/api/agent/installer/worker-download` | Short-lived worker token | Continue token validation; token must validate across replicas |
| GET | `/api/agent/installer/download` | User JWT + active subscription | No change; remains browser/subscriber-gated |

### Configuration

Potential API configuration:

```json
{
  "AgentAuthentication": {
    "SharedSecret": ""
  },
  "DataProtection": {
    "KeyRingBlobUri": ""
  }
}
```

Potential worker configuration remains:

```json
{
  "Agent": {
    "ControlPlaneUrl": "https://api.example.com",
    "SecretKey": ""
  }
}
```

### Azure Infrastructure

- Add a private Data Protection key-ring storage location if using Blob-backed keys.
- Grant the API Container App managed identity the minimum required blob data role for the key-ring location.
- Add a Key Vault-backed secret reference for the agent shared secret.
- Ensure Container App env vars bind to the chosen API configuration names.

### Testing

- API controller tests for anonymous heartbeat/update metadata not returning worker download URLs.
- API controller tests for valid/invalid agent shared secret behavior.
- API controller tests proving browser installer download still requires JWT plus active subscription.
- Token validation tests for valid, expired, wrong-format, and tampered tokens.
- Worker tests proving `Agent:SecretKey` is sent on heartbeat and update requests continue to process authenticated heartbeat metadata.
- Infrastructure/IaC validation for Bicep changes.

---

## Out of Scope

- Full mutual TLS agent authentication.
- Per-agent key enrollment and rotation UI.
- Signed command provenance for all control-plane commands.
- Durable command queue and agent state persistence.
- Public SAS URLs for installer blobs.
- Customer private key, wallet seed, or signing-material storage in Azure.

---

## Open Questions

- [ ] Should production fail fast when `AgentAuthentication:SharedSecret` is missing, or should only update-token issuance be disabled?
- [ ] Should `GET /api/agent/update/latest` remain anonymous metadata-only, or require agent authentication entirely?
- [ ] Should Data Protection keys be persisted to Blob Storage, or should worker download tokens move to stateless HMAC signing?
- [ ] How should the agent shared secret be provisioned into customer-installed execution agents during installer setup and rotation?
- [ ] Should there be a separate short-lived token per agent ID to support future per-agent revocation?

---

## Acceptance Criteria

- [ ] Anonymous callers cannot obtain a valid `installer/worker-download` token from `heartbeat` or `update/latest`.
- [ ] Authenticated workers with the configured agent secret receive update metadata when a newer release exists.
- [ ] Invalid or missing agent secrets are rejected without returning pending commands or update download URLs.
- [ ] Anonymous `GET /api/agent/update/latest` does not expose a privileged download URL.
- [ ] Browser installer downloads still require an authenticated user with an active subscription.
- [ ] Worker download tokens validate across multiple API replicas and after routine restarts.
- [ ] Expired or tampered worker download tokens return 401.
- [ ] API logs enough detail to diagnose denied token issuance and token validation failures without logging secrets.
- [ ] Azure infrastructure provisions any required secret references, managed identity permissions, and key-ring storage.
- [ ] All affected API, worker, and infrastructure tests pass.

---

## Appendix

### References

- [Execution Agent Installer Distribution Plan](../../3-develop/build/plans/20260626-execution-agent-installer-distribution-plan.instructions.md)
- [Control Plane Agent Architecture](../../0-knowledge/29-control-plane-agent-architecture.md)
- [Azure Deployment Infrastructure](../../0-knowledge/21-azure-deployment-infrastructure.md)
- [Azure Secret Operations](../../0-knowledge/39-azure-secret-operations.md)

### Related Features

- Execution Agent Installer Distribution
- Azure Key Vault Secret Management
- Control Plane Agent Authentication
- Worker Auto-Update Flow