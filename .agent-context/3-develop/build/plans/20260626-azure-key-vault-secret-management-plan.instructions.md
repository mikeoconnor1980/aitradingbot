---
applyTo: ".agent-context/3-develop/build/changes/20260626-azure-key-vault-secret-management-changes.md"
currentAgent: "None"
agentStartedAt: "2026-06-26T20:13:54Z"
status: "implemented"
lastUpdated: "2026-06-26T22:28:24Z"
---

<!-- markdownlint-disable-file -->

# Task Checklist: Azure Key Vault Secret Management

## Overview

Move TradePilot's Azure-hosted runtime secrets out of direct Bicep parameters, GitHub workflow parameter values, and Container App inline secrets, and into Azure Key Vault with managed identity access from the API Container App.

This plan deliberately keeps the Option C security boundary intact: customer private keys and wallet signing material stay on the local `TradePilot.ExecutionAgent`. Key Vault is for cloud-side platform secrets such as JWT signing keys, LLM API keys, Telegram bot tokens, temporary SQL connection strings, and any remaining service connection strings that have not yet been replaced by managed identity.

## PBI Details

The current Azure deployment path provisions Azure SQL, Azure SignalR, Azure Container Apps, Azure Static Web Apps, and Blob Storage through Bicep. It does not provision Key Vault. Sensitive values are currently supplied through GitHub secrets into `infrastructure/main.bicep`, passed to `infrastructure/modules/container-app.bicep`, and injected as Container App secrets.

That works functionally, but it keeps long-lived runtime secrets in GitHub and redeploys them through CI. The stronger production posture is: GitHub Actions uses OIDC to deploy infrastructure; Azure Key Vault owns runtime secrets; the API uses managed identity to read only the secrets it needs; and shared keys are removed over time where Azure RBAC can replace them.

### Current State and Gaps

- `infrastructure/main.bicep` has `@secure()` parameters for SQL admin password, JWT secret, LLM API key, registry password, and storage connection string flow.
- `infrastructure/modules/container-app.bicep` stores SQL, SignalR, JWT, LLM, GHCR, and installer Blob connection strings as Container App secrets with inline values.
- `infrastructure/modules/storage-account.bicep` outputs an account-key connection string using `listKeys()`.
- `infrastructure/modules/signalr.bicep` outputs a primary connection string and currently allows broad CORS.
- `src/TradePilot.Api/Program.cs` reads normal configuration only; there is no Key Vault provider or managed identity bootstrap.
- `.github/workflows/deploy-infra.yml` already uses Azure OIDC login, which is the right foundation.
- `src/TradePilot.Api/appsettings.json` contains LLM API key material in tracked config. Treat any committed key as compromised: revoke it, rotate it, and remove it from source before rollout.

### Decision

Use Key Vault for server-side runtime secrets, not as a universal configuration store.

Do not put browser-visible Angular configuration in Key Vault. SPA values such as API URLs, hub URLs, Google client IDs, and public feature flags are public once shipped to a browser.

Prefer managed identity and Azure RBAC over storing connection strings where possible. Key Vault is the intermediate safe home for secrets that still exist while SQL, SignalR, and Storage move toward identity-based access.

### Acceptance Criteria

- A Key Vault is provisioned by Bicep for each environment with soft delete and purge protection enabled.
- The API Container App has a managed identity with read-only access to required Key Vault secrets.
- Runtime secrets are no longer passed to the Container App as raw Bicep parameters except for unavoidable bootstrap/deployment-only credentials.
- GitHub Actions uses OIDC for Azure login and no longer passes app runtime secrets through `azure/arm-deploy` parameters after the bootstrap phase.
- Any committed LLM/API keys are removed from tracked config and rotated outside the repo.
- The API fails fast in production if required secrets such as the JWT signing key are missing.
- Public frontend configuration remains public and documented as such.
- Existing local development remains possible using user secrets, environment variables, or local appsettings without requiring Key Vault access.

## Objectives

- Establish Azure Key Vault as the source of truth for cloud runtime secrets.
- Reduce GitHub's secret footprint to deployment identity and short-lived bootstrap needs.
- Enable secret rotation without rebuilding or redeploying the full application image.
- Prepare the infrastructure for later passwordless SQL, Storage, and SignalR access.
- Document the security boundary so customer trading private keys are never moved into cloud secret storage.

### Discovery References

- `.agent-context/0-knowledge/00-project-overview.md`
- `.agent-context/0-knowledge/03-infrastructure-architecture.md`
- `.agent-context/0-knowledge/10-architecture-decisions.md`
- `.agent-context/0-knowledge/20-business-model-options.md`
- `infrastructure/main.bicep`
- `infrastructure/modules/container-app.bicep`
- `infrastructure/modules/storage-account.bicep`
- `.github/workflows/deploy-infra.yml`
- `.github/workflows/deploy.yml`
- `src/TradePilot.Api/Program.cs`

### Project Patterns

- Bicep files live under `infrastructure/` and `infrastructure/modules/`.
- Azure deployments are managed through GitHub Actions with OIDC via `azure/login@v2`.
- The API is the cloud control plane and must not receive customer private keys.
- The Angular app should only receive public runtime/build configuration.

### [x] Phase 0: Secret Containment and Inventory

**Complexity**: Low | **Risk**: High

- [x] Task 0.1: Revoke and rotate any API keys currently committed in `src/TradePilot.Api/appsettings.json`
  - Files: `src/TradePilot.Api/appsettings.json`, GitHub environment secrets, provider consoles
  - What: Remove live LLM/API key values from tracked config, rotate the provider-side keys, and replace local examples with empty placeholders.
  - Why: A committed key should be considered exposed regardless of later Key Vault adoption.

- [x] Task 0.2: Create a runtime secret inventory
  - Files: `.agent-context/3-develop/build/changes/20260626-azure-key-vault-secret-management-changes.md`
  - What: List each current secret, current source, target Key Vault name, rotation owner, and whether it can eventually be eliminated by managed identity.
  - Expected candidates: `Jwt--SecretKey`, `Llm--ApiKey`, `LlmReview--ApiKey`, `LlmContext--ApiKey`, `Telegram--BotToken`, `ConnectionStrings--DefaultConnection`, `Azure--SignalR--ConnectionString`, and temporary installer storage credentials if still needed.

- [x] Task 0.3: Confirm GitHub environment variable and secret ownership
  - Files: `.github/workflows/deploy-infra.yml`, `.github/workflows/deploy.yml`
  - What: Identify which GitHub secrets are runtime secrets versus deployment-only secrets. Mark runtime secrets for migration into Key Vault.

### [x] Phase 1: Provision Key Vault and Managed Identity in Bicep

**Complexity**: Medium | **Risk**: Medium

- [x] Task 1.1: Add a Key Vault Bicep module
  - Files: `infrastructure/modules/key-vault.bicep`
  - What: Provision `tradepilot-{environment}-kv` with RBAC authorization, soft delete, purge protection, TLS-only access, tags consistent with other modules, and no public secret values in outputs.
  - Notes: Use the latest available stable Key Vault API version. Do not disable purge protection.

- [x] Task 1.2: Add managed identity to the API Container App
  - Files: `infrastructure/modules/container-app.bicep`
  - What: Enable a managed identity for the API Container App and output its principal ID.
  - Decision point: System-assigned identity is simplest; user-assigned identity is cleaner if future storage, SignalR, or cross-resource role assignments need a stable identity before the Container App exists.

- [x] Task 1.3: Grant least-privilege Key Vault access
  - Files: `infrastructure/main.bicep`, `infrastructure/modules/key-vault.bicep`
  - What: Grant the API identity `Key Vault Secrets User` on the vault. Grant the GitHub deployment principal only the bootstrap role required to set secrets, ideally scoped to the vault.

- [x] Task 1.4: Update infra outputs
  - Files: `infrastructure/main.bicep`
  - What: Output vault name and URI as non-secret values for workflow and operations use.

### [x] Phase 2: Seed and Reference Key Vault Secrets

**Complexity**: Medium | **Risk**: Medium

- [x] Task 2.1: Add a Key Vault secret seeding step to the infra workflow
  - Files: `.github/workflows/deploy-infra.yml`
  - What: After Bicep deployment, call `az keyvault secret set` for runtime secrets during bootstrap. Use GitHub environment secrets only as initial seed inputs, then treat Azure Key Vault as the runtime source of truth.
  - Validation: Workflow logs must not print secret values.

- [x] Task 2.2: Replace inline Container App secrets with Key Vault references
  - Files: `infrastructure/modules/container-app.bicep`, `infrastructure/main.bicep`
  - What: Use Container Apps Key Vault-backed secret references for `ConnectionStrings__DefaultConnection`, `Azure__SignalR__ConnectionString`, `Jwt__SecretKey`, LLM API keys, Telegram token, and installer storage secret where still needed.
  - Why: This keeps API code largely unchanged because environment variable names can remain stable.

- [x] Task 2.3: Remove runtime secret parameters from Bicep where possible
  - Files: `infrastructure/main.bicep`, `infrastructure/main.bicepparam`, `.github/workflows/deploy-infra.yml`
  - What: Stop passing `sqlAdminPassword`, `jwtSecretKey`, and `llmApiKey` as normal deployment parameters once the vault seeding flow exists. Keep only parameters that are truly deployment-time concerns.

- [x] Task 2.4: Keep local development fallback intact
  - Files: `src/TradePilot.Api/appsettings.Development.json`, `src/TradePilot.Api/Program.cs`
  - What: Confirm local dev can still use user secrets or environment variables without Azure access.

### [x] Phase 3: Application Hardening

**Complexity**: Low | **Risk**: Medium

- [x] Task 3.1: Fail fast for missing production secrets
  - Files: `src/TradePilot.Api/Program.cs`, `src/TradePilot.Application/Abstractions/Auth/JwtOptions.cs` if applicable
  - What: Ensure Production does not silently generate a fallback JWT signing key if Key Vault or env configuration is missing.

- [x] Task 3.2: Consider app-level Key Vault provider only if Container App references are insufficient
  - Files: `src/TradePilot.Api/TradePilot.Api.csproj`, `src/TradePilot.Api/Program.cs`
  - What: If dynamic secret refresh or direct Key Vault configuration binding is needed, add `Azure.Extensions.AspNetCore.Configuration.Secrets` and `Azure.Identity`, then call `AddAzureKeyVault` behind a `KeyVault__VaultUri` configuration gate.
  - Default: Prefer Container Apps secret references first to minimize application code changes.

- [x] Task 3.3: Add a secret-management ADR
  - Files: `.agent-context/0-knowledge/10-architecture-decisions.md` or a new knowledge file
  - What: Record what belongs in Key Vault, what remains public, what stays local on the execution agent, and what will move to managed identity later.

### [x] Phase 4: Reduce or Eliminate Shared Secrets

**Complexity**: High | **Risk**: Medium

- [x] Task 4.1: Move Blob Storage access from account keys to managed identity
  - Files: `infrastructure/modules/storage-account.bicep`, `src/TradePilot.Infrastructure/Storage/BlobInstallerStore.cs`, `src/TradePilot.Api/Program.cs`
  - What: Assign `Storage Blob Data Reader` to the API identity and `Storage Blob Data Contributor` to the GitHub deployment principal. Disable shared key access only after the code no longer needs connection strings.

- [x] Task 4.2: Plan SQL passwordless access
  - Files: `infrastructure/modules/sql-server.bicep`, `src/TradePilot.Api/appsettings.Production.json`, deployment docs
  - What: Add Entra admin and plan migration from SQL admin password connection strings to managed identity database authentication. This may require Azure SQL user provisioning for the Container App identity.

- [x] Task 4.3: Review SignalR identity support and production tier
  - Files: `infrastructure/modules/signalr.bicep`, `src/TradePilot.Api/Program.cs`, `src/TradePilot.Infrastructure/AzureSignalRPublisher.cs`
  - What: Keep connection string in Key Vault initially. Later, evaluate identity-based SignalR access and tighten CORS from wildcard to the actual Static Web App origin.

- [x] Task 4.4: Reduce deployment-only secrets
  - Files: `.github/workflows/deploy.yml`
  - What: Keep GHCR/SWA deployment credentials only where required. Consider moving from GHCR to ACR with managed identity image pull if GHCR PAT management becomes a recurring risk.

### [x] Phase 5: Validation and Operational Runbooks

**Complexity**: Medium | **Risk**: Low

- [x] Task 5.1: Add infrastructure validation to the workflow
  - Files: `.github/workflows/deploy-infra.yml`
  - What: Run Bicep build and an Azure deployment `what-if` before applying changes.

- [x] Task 5.2: Validate runtime secret resolution
  - Files: deployment notes / change record
  - What: After deployment, restart the Container App revision and confirm API startup, `/healthz`, `/api/version`, JWT issuance, SignalR startup, and LLM configuration behavior.

- [x] Task 5.3: Add a rotation runbook
  - Files: `.agent-context/0-knowledge/` or `docs/operations/`
  - What: Document how to rotate JWT signing keys, LLM keys, Telegram token, SQL password if still present, and installer storage credentials if still present.

- [x] Task 5.4: Add secret scanning validation
  - Files: `.github/workflows/deploy.yml` or separate security workflow
  - What: Add or enable secret scanning checks so future live keys are not committed.

## Scoping Summary

| Phase | Complexity | Risk |
|-------|------------|------|
| Phase 0: Secret Containment and Inventory | Low | High |
| Phase 1: Key Vault and Identity Bicep | Medium | Medium |
| Phase 2: Secret Seeding and References | Medium | Medium |
| Phase 3: Application Hardening | Low | Medium |
| Phase 4: Shared Secret Reduction | High | Medium |
| Phase 5: Validation and Runbooks | Medium | Low |
| **Total** | **Medium-High** | **Medium** |

### Scoping Notes

- Phase 0 is urgent because committed secrets must be rotated regardless of infrastructure changes.
- Native Container Apps Key Vault references are the smallest first implementation because the API already reads environment-backed configuration.
- SQL passwordless, Storage RBAC, and SignalR identity should be separate hardening steps after the initial Key Vault migration is stable.
- GHCR image pull still likely needs a registry secret unless the project moves to ACR.
- This plan does not move execution-agent private keys to Azure.

## Dependencies

- Azure subscription and resource group access for `dev` and `prod`.
- GitHub Actions OIDC federated credential already configured for `azure/login@v2`.
- The deployment principal needs appropriate management-plane roles and Key Vault secret bootstrap permissions.
- Bicep CLI / Azure CLI availability in GitHub Actions.

## Success Criteria

- Bicep deploys a Key Vault and managed identity without diagnostics.
- Runtime secrets resolve from Key Vault-backed Container App secret references.
- `deploy-infra.yml` no longer passes runtime app secrets into ARM deployment parameters after bootstrap.
- Production API startup fails if required secrets are missing.
- No live API keys remain in tracked appsettings files.
- Local development remains unaffected.
- The knowledge base records the new secret-management decision.

## Agent Log

| Agent | Status | Started | Completed |
|-------|--------|---------|-----------|
| Implementation Planner | planned | 2026-06-26T19:46:49Z | 2026-06-26T19:46:49Z |
| Plan Implementer | implemented | 2026-06-26T20:13:54Z | 2026-06-26T22:28:24Z |
