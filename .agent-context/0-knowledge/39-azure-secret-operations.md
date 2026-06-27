# Azure Secret Operations

This document captures the Azure-side operational checks and runbooks for the Key Vault-backed control-plane secrets. It is intentionally limited to cloud runtime secrets such as JWT signing keys, LLM API keys, Telegram bot tokens, SignalR connection strings, and temporary SQL credentials. Customer private keys and wallet signing material remain local to TradePilot.ExecutionAgent and are never rotated through these procedures.

## Overview

The current Azure runtime secret flow is:

GitHub environment bootstrap secret
-> Key Vault secret seed
-> Container Apps Key Vault secret reference
-> API environment-backed configuration

The operational responsibilities are split as follows:

| Area | Owner | Notes |
|---|---|---|
| Key Vault secret values | Platform operator | Seeded during bootstrap, then treated as source of truth |
| Container App managed identity access | Infrastructure operator | Must retain `Key Vault Secrets User` access on the vault |
| GitHub environment bootstrap values | Platform operator | Only needed for initial seed or later manual reseed |
| Subscriber private keys | Subscriber / execution-agent operator | Must stay local to the execution agent |

## Key Components

| Component | Purpose |
|---|---|
| `.github/workflows/deploy-infra.yml` | Runs Bicep build, `what-if`, bootstrap deployment, Key Vault seeding, and final Container App redeployment with Key Vault references |
| `.github/workflows/deploy.yml` | Runs build/test CI and checked-tree secret scanning |
| `infrastructure/main.bicep` | Provisions Key Vault, identity, RBAC, and API configuration wiring |
| `infrastructure/modules/container-app.bicep` | Defines Key Vault-backed Container Apps secret references |
| `src/TradePilot.Api/Program.cs` | Fails fast outside development when `Jwt:SecretKey` is missing |

## Initial Deployment Bootstrap

Do not manually create the normal runtime secrets in Key Vault before the first deployment. The infrastructure workflow owns the first seed so the vault, Container App identity, RBAC assignments, secret values, and Container App secret references are created in one repeatable path.

Configure these values on each GitHub environment that can run infrastructure deployment, such as `dev` and `prod`.

### GitHub Environment Secrets

| Secret | Purpose |
|---|---|
| `SQL_ADMIN_PASSWORD` | Azure SQL bootstrap password and temporary API SQL connection-string seed |
| `JWT_SECRET_KEY` | Initial API JWT signing key, seeded to `jwt--secret-key` |
| `LLM_API_KEY` | Initial provider key, currently seeded to `llm--api-key`, `llm-review--api-key`, and `llm-context--api-key` |
| `TELEGRAM_BOT_TOKEN` | Initial Telegram token, seeded to `telegram--bot-token` |
| `GHCR_PAT` | Deployment/bootstrap credential for pulling the API image from GitHub Container Registry |

### GitHub Environment Variables

| Variable | Purpose |
|---|---|
| `AZURE_CLIENT_ID` | OIDC application/client ID used by `azure/login` |
| `AZURE_TENANT_ID` | Azure tenant ID used by `azure/login` |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID used by `azure/login` |
| `SQL_ADMIN_LOGIN` | Azure SQL administrator login name |
| `SWA_URL` | Static Web App origin used for API and SignalR CORS |
| `AZURE_DEPLOYMENT_PRINCIPAL_OBJECT_ID` | Object ID of the GitHub deployment principal; Bicep grants this principal Key Vault Secrets Officer on the vault for seeding |

Optional SQL passwordless planning variables:

| Variable | Purpose |
|---|---|
| `SQL_ENTRA_ADMIN_OBJECT_ID` | Optional Entra administrator object ID for future Azure SQL passwordless setup |
| `SQL_ENTRA_ADMIN_LOGIN` | Optional Entra administrator display/login name for future Azure SQL passwordless setup |

### Bootstrap Flow

1. Add or update the GitHub environment secrets and variables above.
2. Run `.github/workflows/deploy-infra.yml` for the target environment.
3. The workflow performs a bootstrap Bicep deployment with `useKeyVaultSecretReferences=false` so the first Container App revision can start before Key Vault secrets exist.
4. The workflow seeds Key Vault with `jwt--secret-key`, `llm--api-key`, `llm-review--api-key`, `llm-context--api-key`, `telegram--bot-token`, `connectionstrings--defaultconnection`, and `azure--signalr--connectionstring`.
5. The workflow redeploys the Container App with `useKeyVaultSecretReferences=true` and a new runtime configuration version so the active revision uses Key Vault-backed secret references.
6. Run the post-deployment runtime validation checklist below.

After the first successful deployment, treat Key Vault as the runtime source of truth. Keep GitHub bootstrap values only for later reseeding or disaster recovery, and prefer direct Key Vault secret rotation plus Container App restart for routine runtime secret changes.

Manual Key Vault secret creation is only appropriate for emergency repair, rotation, or one-off operational correction after the vault already exists. Do not place customer execution-agent private keys, wallet seeds, or signing material in Key Vault.

## Post-Deployment Runtime Validation

Run this checklist after `deploy-infra.yml` completes successfully for `dev` or `prod`. Do not change or invent secret values during validation.

### Preconditions

- The infrastructure workflow completed without Bicep build or `what-if` errors.
- The final `tradepilot-{environment}-api` Container App redeployment with Key Vault references completed.
- You have the environment FQDN and Azure access needed to inspect Container App revisions and logs.

### Validation Checklist

1. Confirm the Container App revision is healthy.
   - `az containerapp revision list --name tradepilot-<environment>-api --resource-group rg-tradepilot-<environment> --query "[].{name:name,active:properties.active,health:properties.healthState}" --output table`
   - Expected: the latest revision is active and reports a healthy/running state.

2. Confirm platform liveness through `/healthz`.
   - `curl --fail https://<api-fqdn>/healthz`
   - Expected: HTTP 200.

3. Confirm API version metadata through `/api/version`.
   - `curl --fail https://<api-fqdn>/api/version`
   - Expected: HTTP 200 with the current environment name plus non-empty deployment metadata when `deploy.yml` has updated the app image.

4. Confirm JWT issuance still works.
   - Use a non-production test account in `dev`, or a designated smoke-test account in higher environments.
   - `curl --fail --request POST https://<api-fqdn>/api/auth/login --header "Content-Type: application/json" --data '{"email":"<smoke-test-email>","password":"<smoke-test-password>"}'`
   - Expected: HTTP 200 with a non-empty `token` and `refreshToken`.
   - Operator follow-up: manage smoke-test credentials outside the repository. Do not hardcode them in workflows or source.

5. Confirm an authenticated endpoint accepts the issued JWT.
   - Call `GET /api/auth/me` or another low-risk authenticated read endpoint with `Authorization: Bearer <token>`.
   - Expected: HTTP 200.

6. Confirm SignalR startup still works.
   - Inspect API logs for Azure SignalR startup success and absence of Key Vault secret-resolution errors.
   - `az containerapp logs show --name tradepilot-<environment>-api --resource-group rg-tradepilot-<environment> --follow false --tail 200`
   - Optionally connect a client to `https://<api-fqdn>/hubs/marketdata` using the deployed UI or a purpose-built smoke client.
   - Expected: no startup exception related to `Azure:SignalR:ConnectionString`, and hub connections succeed.

7. Confirm LLM-backed configuration still resolves.
   - Inspect API logs for missing configuration or authentication failures from `Llm`, `LlmReview`, or `LlmContext` clients.
   - In `dev`, execute a low-risk LLM-backed workflow such as strategy review or interpretation using a smoke-test request.
   - Expected: no startup-time missing-key exceptions and no immediate 401/403 provider failures caused by missing secrets.

8. If validation fails, inspect the Key Vault reference chain in this order.
   - Confirm the secret exists in Key Vault with the expected name.
   - Confirm the Container App identity still has vault read access.
   - Confirm the Container App secret reference still points at the correct secret URI and `identityref:system`.
   - Restart the Container App revision again only after the underlying issue is fixed.

## Rotation Runbooks

These runbooks are for operator-managed rotation only. Do not perform real secret rotation from CI, and do not rotate customer execution-agent signing material here.

### JWT Signing Key

Use when:
- the JWT signing key is suspected exposed
- scheduled platform secret rotation requires new signing material

Procedure:
1. Generate a new high-entropy signing key outside the repository and store it in the operator secret manager.
2. Set the new value in Key Vault under `jwt--secret-key`.
3. Restart the API Container App revision.
4. Validate `/healthz`, `/api/version`, and `POST /api/auth/login`.
5. Confirm new tokens issue successfully.
6. Operator follow-up: understand that existing JWTs may become invalid immediately after rotation unless a multi-key validation strategy is introduced later.

### LLM API Keys

Applies to:
- `llm--api-key`
- `llm-review--api-key`
- `llm-context--api-key`

Procedure:
1. Rotate the provider-side API key in the provider console.
2. Update the corresponding Key Vault secret values.
3. Restart the API Container App revision.
4. Run the runtime validation checklist, focusing on LLM-backed flows.
5. Operator follow-up: if the three app roles move to separate credentials later, rotate each secret independently and validate each workflow separately.

### Telegram Bot Token

Procedure:
1. Revoke and recreate the token through BotFather or the approved Telegram operator workflow.
2. Update `telegram--bot-token` in Key Vault.
3. Restart the API Container App revision.
4. Inspect logs for `TelegramBotPollingService` startup behavior.
5. Validate the notification/link flow using a non-production test chat in `dev`.

### SQL Password, If Still Present

This applies only while the API still depends on `connectionstrings--defaultconnection`.

Procedure:
1. Rotate the Azure SQL login password using approved database operations.
2. Update the Key Vault secret value for `connectionstrings--defaultconnection`.
3. Restart the API Container App revision.
4. Validate `/healthz` and an API request that touches the database.
5. Operator follow-up: complete the planned managed-identity SQL migration so this secret can be retired.

### SignalR Connection String

Procedure:
1. Regenerate the Azure SignalR primary or secondary key.
2. Update `azure--signalr--connectionstring` in Key Vault.
3. Restart the API Container App revision.
4. Validate hub startup and browser connectivity against `/hubs/marketdata`.
5. Operator follow-up: revisit identity-based access if Azure SignalR support becomes sufficient for this host model.

## Secret Naming Reference

| Secret Name | Runtime Consumer | Rotation Notes |
|---|---|---|
| `jwt--secret-key` | API JWT bearer configuration | Immediate effect after restart |
| `llm--api-key` | Strategy interpretation | Provider-managed key |
| `llm-review--api-key` | Review workflow | Provider-managed key |
| `llm-context--api-key` | Market context workflow | Provider-managed key |
| `telegram--bot-token` | Telegram polling and notifications | Rotate in Telegram first |
| `connectionstrings--defaultconnection` | API database connectivity | Temporary until SQL passwordless is complete |
| `azure--signalr--connectionstring` | Azure SignalR integration | Rotate through SignalR keys |

## Operator Guardrails

- Never commit rotated secret values to source control.
- Never print secret values in workflow logs or shell history.
- Never move subscriber private keys, seed phrases, or wallet signing material into Key Vault.
- Prefer Key Vault secret updates plus Container App restart over app-level configuration edits.
- Treat GitHub environment secrets as bootstrap inputs only; keep the steady-state runtime source of truth in Key Vault.

## Creating/Extending Azure Secret Operations

When a new cloud runtime secret is introduced:

1. Add the secret to the runtime inventory and choose a Key Vault name using the existing lowercase double-dash convention.
2. Wire the secret into `infrastructure/main.bicep` and `infrastructure/modules/container-app.bicep` through a Key Vault-backed Container Apps secret reference.
3. Update `.github/workflows/deploy-infra.yml` only if bootstrap seeding still requires a GitHub-provided initial value.
4. Add validation and rotation instructions to this document before rollout.
5. If the secret is browser-visible or execution-agent-local, do not place it in Key Vault; document the reason explicitly.