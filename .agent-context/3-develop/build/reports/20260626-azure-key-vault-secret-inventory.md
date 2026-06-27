# Runtime Secret Inventory

## Scope

This inventory was created during Phase 0 of the Azure Key Vault secret management plan.

Constraints applied:

- No Azure deployment performed.
- No external provider secret rotation performed from this repo.
- No GitHub environment values were modified from this repo.
- Customer private keys and wallet signing material remain local to the ExecutionAgent and are out of scope for Key Vault.

## Runtime Secrets

| Secret / Setting | Current Source | Current Consumers | Proposed Key Vault Secret Name | Rotation Owner | Managed Identity End State |
|---|---|---|---|---|---|
| `Jwt__SecretKey` | GitHub environment secret `JWT_SECRET_KEY` passed to `infrastructure/main.bicep` and injected into Container App secret `jwt-secret-key` | `TradePilot.Api` | `jwt--secret-key` | Platform operations | No, remains a secret even after Key Vault adoption |
| `Llm__ApiKey` | Tracked `src/TradePilot.Api/appsettings.json` before Phase 0; GitHub environment secret `LLM_API_KEY` in infra deployment flow | `TradePilot.Api` strategy interpretation | `llm--api-key` | Platform operations / AI provider owner | No, remains a provider secret |
| `LlmReview__ApiKey` | Indirectly shares `LLM_API_KEY` through Container App env wiring; tracked `src/TradePilot.Api/appsettings.json` before Phase 0 | `TradePilot.Api` strategy review | `llm-review--api-key` | Platform operations / AI provider owner | No, remains a provider secret |
| `LlmContext__ApiKey` | Indirectly shares `LLM_API_KEY` through Container App env wiring; tracked `src/TradePilot.Api/appsettings.json` before Phase 0 | `TradePilot.Api` market context snapshots | `llm-context--api-key` | Platform operations / AI provider owner | No, remains a provider secret |
| `Telegram__BotToken` | GitHub environment secret `TELEGRAM_BOT_TOKEN` set directly on Container App in `.github/workflows/deploy.yml` | `TradePilot.Api` Telegram integration | `telegram--bot-token` | Platform operations / Telegram bot owner | No, remains a provider secret |
| `ConnectionStrings__DefaultConnection` | Constructed in `infrastructure/main.bicep` from SQL admin login/password and injected into Container App secret `sql-connection-string` | `TradePilot.Api` | `connectionstrings--defaultconnection` | Platform operations / database owner | Yes, candidate to eliminate after Azure SQL managed identity migration |
| `Azure__SignalR__ConnectionString` | `infrastructure/modules/signalr.bicep` output from `listKeys()` and injected into Container App secret `signalr-connection-string` | `TradePilot.Api` | `azure--signalr--connectionstring` | Platform operations | Potentially, if SignalR identity-based access becomes viable for this host path |
| `Installer__BlobConnectionString` | `infrastructure/modules/storage-account.bicep` output from storage account keys and injected into Container App secret `installer-blob-connection` | `TradePilot.Api` installer download path | `installer--blob-connectionstring` | Platform operations | Yes, candidate to eliminate after Blob RBAC / managed identity migration |
| `sqlAdminPassword` | GitHub environment secret `SQL_ADMIN_PASSWORD` passed as secure Bicep parameter | Azure SQL provisioning only | Not a runtime app secret; keep out of steady-state Key Vault inventory | Platform operations / database owner | Yes, can be reduced after passwordless SQL provisioning |
| `registryPassword` | GitHub environment secret `GHCR_PAT` passed to Bicep and used by Container Apps registry config | Container Apps image pull bootstrap | Not an application runtime secret; treat as deployment/bootstrap secret | Platform operations | Possibly, if image pull moves to ACR plus managed identity |

## GitHub Ownership Classification

### Runtime secrets to migrate out of GitHub

These are currently runtime inputs and should move to Azure Key Vault as the source of truth:

- `JWT_SECRET_KEY`
- `LLM_API_KEY`
- `TELEGRAM_BOT_TOKEN`
- Any future split values for `LlmReview__ApiKey` and `LlmContext__ApiKey` if they stop sharing one provider key

These are currently derived shared-secret runtime values and should move behind Key Vault references first, then be removed over time via identity-based access:

- SQL connection string derived from `SQL_ADMIN_LOGIN` + `SQL_ADMIN_PASSWORD`
- SignalR connection string from service keys
- Installer Blob Storage connection string from storage account keys

### Deployment-only GitHub variables/secrets to retain outside Key Vault

These are deployment/bootstrap settings rather than application runtime secrets:

- GitHub environment variables: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `SQL_ADMIN_LOGIN`, `SWA_URL`, `API_FQDN`, `INSTALLER_STORAGE_ACCOUNT`, `TELEGRAM_BOT_USERNAME`
- GitHub environment secrets: `GHCR_PAT`, `SWA_DEPLOYMENT_TOKEN`, `SNYK_TOKEN`

## Operator Follow-Up

1. Revoke and rotate the Gemini API key that was previously committed in `src/TradePilot.Api/appsettings.json`. Treat the old value as compromised.
2. Confirm whether GitHub environments `dev` and `prod` both carry `JWT_SECRET_KEY`, `LLM_API_KEY`, `TELEGRAM_BOT_TOKEN`, `SQL_ADMIN_PASSWORD`, `GHCR_PAT`, and `SWA_DEPLOYMENT_TOKEN`, and record the owning team/account for each.
3. Decide whether `Llm`, `LlmReview`, and `LlmContext` should continue sharing one provider key or receive separate provider credentials before Key Vault seeding.
4. Confirm that no customer private keys, wallet seeds, or signing material are stored in GitHub secrets, Bicep parameters, or API appsettings.

## Evidence Reviewed

- `src/TradePilot.Api/appsettings.json`
- `.github/workflows/deploy-infra.yml`
- `.github/workflows/deploy.yml`
- `infrastructure/main.bicep`
- `infrastructure/modules/container-app.bicep`
- `infrastructure/modules/storage-account.bicep`
- `infrastructure/modules/signalr.bicep`