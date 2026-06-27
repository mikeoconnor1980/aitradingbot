<!-- markdownlint-disable-file -->
# Release Changes: Azure Key Vault Secret Management

**Related Plan**: 20260626-azure-key-vault-secret-management-plan.instructions.md
**Implementation Date**: 2026-06-26

## Summary

Completed implementation for Azure Key Vault secret management across all six phases, with operator follow-up captured for any external secret rotation, Azure-side configuration, or deployment-only actions that cannot be completed safely from the repo. The finished change set removes committed runtime key material from tracked config, adds Azure Key Vault and managed identity support in Bicep, moves Container App runtime secret resolution to Key Vault references, hardens API startup around missing JWT secrets, shifts installer blob access to managed identity, adds workflow preflight validation and secret scanning, and documents the operational rotation and post-deployment verification steps.

## Changes

### Added

<!-- Phase 0: Secret Containment and Inventory -->
- .agent-context/3-develop/build/reports/20260626-azure-key-vault-secret-inventory.md: Added the runtime secret inventory, GitHub workflow secret classification, and operator follow-up notes for provider rotation and ownership confirmation.

<!-- Phase 1: Provision Key Vault and Managed Identity in Bicep -->
- infrastructure/modules/key-vault.bicep: Added a reusable Key Vault module with RBAC authorization, purge protection, soft-delete retention, TLS-only access, tags, and non-secret outputs.

<!-- Phase 3: Application Hardening -->
- tests/TradePilot.Api.Tests/Infrastructure/SecretConfigurationStartupTests.cs: Added focused startup coverage proving non-development API startup fails when Jwt:SecretKey is missing.

<!-- Phase 4: Reduce or Eliminate Shared Secrets -->
- tests/TradePilot.Infrastructure.Tests/Storage/BlobInstallerStoreTests.cs: Added focused tests covering the BlobServiceUri-based managed-identity configuration path for installer downloads.

<!-- Phase 5: Validation and Operational Runbooks -->
- .agent-context/0-knowledge/39-azure-secret-operations.md: Added the Azure post-deployment secret-resolution checklist and operator rotation runbooks.
- .gitleaks.toml: Added repository secret-scanning configuration with a narrow allowlist for known non-sensitive test keys.

### Modified

<!-- Phase 0: Secret Containment and Inventory -->
- src/TradePilot.Api/appsettings.json: Removed committed Gemini API key values from tracked LLM configuration and replaced them with empty placeholders.

<!-- Phase 1: Provision Key Vault and Managed Identity in Bicep -->
- infrastructure/main.bicep: Added Key Vault provisioning, vault-scoped RBAC assignments for the API and optional deployment principal, and non-secret vault outputs.
- infrastructure/modules/container-app.bicep: Enabled a system-assigned managed identity for the API Container App and exposed its principal ID.

<!-- Phase 2: Seed and Reference Key Vault Secrets -->
- .github/workflows/deploy-infra.yml: Removed runtime app secrets from ARM parameters, added Key Vault secret seeding after infrastructure deployment, and restarted the Container App revision to pick up updated secret references.
- .github/workflows/deploy.yml: Switched Telegram runtime secret updates to a Key Vault-backed Container Apps secret reference instead of inline secret injection.
- infrastructure/main.bicep: Replaced direct runtime secret flow into the Container App module with Key Vault URI-based wiring.
- infrastructure/main.bicepparam: Removed JWT and LLM runtime secret parameter bindings from the checked-in Bicep parameter file.
- infrastructure/modules/container-app.bicep: Replaced inline runtime secret values with Key Vault-backed secret references while preserving stable environment variable names for the API host.
- src/TradePilot.Api/Program.cs: Made the hosted environment-variable secret fallback explicit without adding direct Key Vault provider code.
- src/TradePilot.Api/appsettings.Development.json: Removed tracked development secret values while preserving the local configuration shape for user secrets and environment variables.

<!-- Phase 3: Application Hardening -->
- src/TradePilot.Api/Program.cs: Restricted the generated JWT fallback key to development only and made non-development startup fail fast with a clear configuration error when Jwt:SecretKey is missing.
- .agent-context/0-knowledge/10-architecture-decisions.md: Added a secret-management ADR covering Key Vault scope, public browser-visible configuration, execution-agent custody boundaries, and the decision to defer direct app-level Key Vault loading.

<!-- Phase 4: Reduce or Eliminate Shared Secrets -->
- src/TradePilot.Application/Agent/Models/InstallerOptions.cs: Replaced installer blob connection-string configuration with a Blob service URI setting for managed-identity access.
- src/TradePilot.Infrastructure/Storage/BlobInstallerStore.cs: Switched installer blob access from connection strings to BlobServiceClient plus DefaultAzureCredential.
- src/TradePilot.Api/Program.cs: Updated installer store wiring to use BlobServiceUri-based configuration.
- src/TradePilot.Api/appsettings.json: Changed installer configuration to BlobServiceUri instead of a blob connection string.
- src/TradePilot.Api/appsettings.Production.json: Added installer BlobServiceUri placeholders for production configuration.
- infrastructure/modules/storage-account.bicep: Added a non-secret blob service URI output and removed the storage connection-string output.
- infrastructure/modules/container-app.bicep: Removed installer blob connection-string secret injection and passed BlobServiceUri as non-secret configuration.
- infrastructure/modules/sql-server.bicep: Added optional Entra admin provisioning inputs to support the planned SQL passwordless migration.
- infrastructure/modules/signalr.bicep: Tightened SignalR CORS defaults and moved the default SKU from Free_F1 to Standard_S1.
- infrastructure/main.bicep: Added storage RBAC assignments, passed BlobServiceUri into the Container App, and surfaced SQL Entra-admin planning outputs.
- .github/workflows/deploy-infra.yml: Removed installer blob secret seeding, added optional SQL Entra admin and deployment principal wiring, and documented operator follow-up for SQL passwordless completion and storage shared-key disablement.
- .github/workflows/deploy.yml: Added explicit follow-up guidance for the remaining GHCR and Static Web Apps deployment secrets.

<!-- Phase 5: Validation and Operational Runbooks -->
- .github/workflows/deploy-infra.yml: Added Bicep build and what-if preflight validation plus explicit post-deploy runtime secret verification guidance.
- .github/workflows/deploy.yml: Added repository secret scanning with gitleaks using a checked-in configuration.
- .agent-context/0-knowledge/README.md: Indexed the new Azure secret operations knowledge document.
- tests/TradePilot.Api.Tests/Infrastructure/BaseControllerTests.cs: Added minimal LlmContext test-host settings so startup validation remains compatible with focused API tests.
- tests/TradePilot.Api.Tests/Controllers/HealthControllerTests.cs: Added minimal LlmContext test-host settings for health-surface startup coverage.
- tests/TradePilot.Api.Tests/Hubs/MarketDataHubTests.cs: Added minimal LlmContext test-host settings for hub startup coverage.

### Removed

## Test Results

<!-- Phase 0: Secret Containment and Inventory -->
- Focused secret check: PASSED — committed Gemini key search in src/TradePilot.Api/appsettings.json returned 0 matches.
- File diagnostics: PASSED — 2/2 passed for src/TradePilot.Api/appsettings.json and .agent-context/3-develop/build/reports/20260626-azure-key-vault-secret-inventory.md.
- Architecture Tests: PASSED — not applicable for this phase.

<!-- Phase 1: Provision Key Vault and Managed Identity in Bicep -->
- Bicep build: PASSED — infrastructure/main.bicep built successfully after wiring the Key Vault module and RBAC assignments.
- File diagnostics: PASSED — 3/3 passed for infrastructure/main.bicep, infrastructure/modules/container-app.bicep, and infrastructure/modules/key-vault.bicep.
- Architecture Tests: PASSED — not applicable for this phase.

<!-- Phase 2: Seed and Reference Key Vault Secrets -->
- API build validation: PASSED — focused API build completed successfully after configuration wiring changes.
- Bicep build validation: PASSED — 2/2 passed for infrastructure/main.bicep and infrastructure/modules/container-app.bicep after switching to Key Vault secret references.
- Architecture Tests: PASSED — not applicable for this phase.

<!-- Phase 3: Application Hardening -->
- SecretConfigurationStartupTests: PASSED — 1/1 passed, proving non-development startup fails when Jwt:SecretKey is absent.
- TradePilot.Api.Tests build: PASSED.
- Architecture Tests: PASSED — not applicable for this phase.

<!-- Phase 4: Reduce or Eliminate Shared Secrets -->
- BlobInstallerStoreTests: PASSED — 2/2 passed for the new BlobServiceUri configuration path.
- SecretConfigurationStartupTests: PASSED — 1/1 passed as a regression check after API wiring changes.
- Focused API build: PASSED.
- Bicep build: PASSED — infrastructure/main.bicep built successfully with storage RBAC, SQL Entra-admin hooks, and SignalR changes.
- Architecture Tests: PASSED — not applicable for this phase.

<!-- Phase 5: Validation and Operational Runbooks -->
- Focused API runtime validation tests: PASSED — 8/8 passed after adding required LlmContext test-host settings.
- Bicep build: PASSED.
- Architecture Tests: PASSED — not applicable for this phase.

## Issues

<!-- Phase 0: Secret Containment and Inventory -->
- Provider-side secret revocation/rotation and GitHub environment updates were intentionally not performed because the implementation constraints prohibit rotating real external provider secrets or inventing replacement values; these remain operator follow-up items.
- The plan targeted the shared changes file for the runtime secret inventory, but the phase implementation preserved separation of concerns by producing a dedicated report artifact and this change record now references it.

<!-- Phase 1: Provision Key Vault and Managed Identity in Bicep -->
- The first Bicep validation pass failed because role assignment resources were expressed with an invalid read-only scope shape and non-deterministic naming; the module was corrected to use vault-scoped resources with deterministic guid-based names.
- The GitHub deployment principal object ID is not discoverable from the repository alone, so infrastructure now accepts an optional deploymentPrincipalObjectId for later operator-supplied vault bootstrap permissions.

<!-- Phase 2: Seed and Reference Key Vault Secrets -->
- One Bicep validation attempt failed because the CLI was pointed at the Windows DOS device path NUL for output; rerunning with a real file output path resolved the validation.
- End-to-end Azure validation was intentionally not performed, so the updated workflow still needs a real environment run to confirm that the deployment principal can seed the vault and that Container Apps resolves the referenced secrets at startup.
- The bootstrap workflow still depends on GitHub environment secrets for initial seeding values such as JWT, LLM, Telegram, SQL admin password, and GHCR credentials; confirming those environment values remains an operator follow-up.

<!-- Phase 3: Application Hardening -->
- The dedicated runTests tool did not directly discover the single MSTest file path, so focused validation used a dotnet test filter on the owning API test project instead.

<!-- Phase 4: Reduce or Eliminate Shared Secrets -->
- The first Blob Storage implementation used the wrong token-credential constructor shape; it was corrected by constructing BlobServiceClient with DefaultAzureCredential and resolving the container client from that service client.
- The first storage RBAC pass used non-deterministic module outputs in scope/name expressions; it was corrected by computing deployment-time deterministic names directly from the storage account name.
- The initial infrastructure test approach over-mocked Azure internals and caused compile failures; it was replaced with stable constructor-level tests for the BlobServiceUri path.
- Real Azure validation remains outstanding because deployment is intentionally out of scope for this implementation.

<!-- Phase 5: Validation and Operational Runbooks -->
- Focused API smoke tests initially failed because LlmContextOptions validation now runs at startup; the affected test hosts were updated with minimal non-secret settings.
- Runtime secret resolution still requires a real Azure environment run because deployment and live secret seeding were intentionally not executed here.
- Secret scanning currently covers the checked-out tree in CI; broader historical scanning or GitHub-native secret-scanning rollout remains a future hardening step.

## Design Decisions

<!-- Phase 0: Secret Containment and Inventory -->
- Empty placeholders were used in tracked config rather than fake secrets so the repository no longer contains live key material while still preserving the configuration shape for local development.
- Customer private keys and wallet signing material remain explicitly out of scope for Key Vault and stay local to the TradePilot.ExecutionAgent per the Option C security boundary.
- SQL admin credentials and GHCR credentials are treated as deployment/bootstrap concerns in the inventory, while steady-state API runtime secrets are the migration target for Key Vault.

<!-- Phase 1: Provision Key Vault and Managed Identity in Bicep -->
- A system-assigned managed identity was chosen for the API Container App as the smallest viable change for vault access in this phase.
- The API identity receives the Key Vault Secrets User role for read-only runtime access, while the optional deployment principal is scoped to Key Vault Secrets Officer for later bootstrap seeding without broader vault administration.
- Key Vault outputs remain limited to vault name and URI so deployment outputs do not expose secret material.

<!-- Phase 2: Seed and Reference Key Vault Secrets -->
- Container Apps Key Vault secret references were preferred over direct app-level Key Vault loading so the application configuration surface remains stable and most secret-management changes stay in infrastructure.
- SQL admin password remains a deployment-time parameter because Azure SQL creation still needs it, while JWT and LLM runtime secrets were removed from ARM deployment parameters.
- The current single bootstrap LLM credential is fanned out into the three existing app configuration slots so the present application structure keeps working while preserving a future path to split credentials later.

<!-- Phase 3: Application Hardening -->
- Non-development hosts now fail fast on missing Jwt:SecretKey instead of generating a fallback signing key, because silent fallback would hide secret-resolution failures and destabilize issued tokens.
- Direct app-level Key Vault loading remains deferred because Container Apps Key Vault references already satisfy the current runtime secret-delivery requirement with less application complexity.

<!-- Phase 4: Reduce or Eliminate Shared Secrets -->
- Installer artifact access now prefers managed identity plus RBAC, but shared key access was intentionally not disabled yet so operators can validate the new path before enforcing that storage hardening step.
- SQL passwordless was implemented as infrastructure planning support rather than an immediate app authentication switch because the full migration still depends on Azure-side Entra admin inputs and database principal creation.
- SignalR remains connection-string based through Key Vault for now; this phase tightened CORS and production SKU defaults without forcing an unsupported identity path.

<!-- Phase 5: Validation and Operational Runbooks -->
- Post-deployment runtime secret resolution was implemented as an operator runbook instead of an executed deployment check because the implementation constraints prohibit Azure deployment and real secret manipulation from this environment.
- Secret scanning was added through the maintained gitleaks GitHub Action with a narrow allowlist rather than a custom install script.
- Infrastructure preflight uses az bicep build plus az deployment group what-if before apply as the smallest practical workflow validation step.

## Review Hints

<!-- Phase 0: Secret Containment and Inventory -->
- Review the operator follow-up section in .agent-context/3-develop/build/reports/20260626-azure-key-vault-secret-inventory.md before validating Phase 1 so the eventual vault seeding names align with the documented inventory.

<!-- Phase 1: Provision Key Vault and Managed Identity in Bicep -->
- Review whether deploymentPrincipalObjectId should be populated from the GitHub OIDC deployment principal in the workflow phase so vault bootstrap permissions remain scoped to the vault rather than inherited from broader resource roles.
- Review whether Key Vault public network access and AzureServices bypass should be tightened later once the target Container Apps networking model is finalized.

<!-- Phase 2: Seed and Reference Key Vault Secrets -->
- Review the new workflow bootstrap secret list in .github/workflows/deploy-infra.yml against the Phase 0 secret inventory so vault secret names and GitHub environment names stay aligned.
- Review whether dev and prod should continue sharing one operator-managed LLM bootstrap secret or whether separate values should be introduced before the first real rollout.

<!-- Phase 3: Application Hardening -->
- Review whether any non-development environment besides Azure should intentionally retain the strict JWT fail-fast behavior, since the current guard applies to all non-development hosts.

<!-- Phase 4: Reduce or Eliminate Shared Secrets -->
- Review the storage RBAC path in infrastructure/main.bicep together with src/TradePilot.Infrastructure/Storage/BlobInstallerStore.cs before disabling shared key access on the storage account.
- Review the optional SQL Entra admin inputs expected by .github/workflows/deploy-infra.yml and infrastructure/modules/sql-server.bicep because passwordless SQL is only planned until operator-owned identity values and database principals exist.
- Review the SignalR SKU and CORS changes against the actual Static Web App origins per environment before the next deployment.

<!-- Phase 5: Validation and Operational Runbooks -->
- Review .agent-context/0-knowledge/39-azure-secret-operations.md before the first real Azure rollout so the validation sequence, secret ownership, and rotation responsibilities are agreed ahead of deployment.
- Review whether CI-only checked-tree secret scanning is sufficient for this repository or whether GitHub-native secret scanning and historical scans should be added later.

## Release Summary

Implemented 6 of 6 phases and completed all 22 planned tasks. The repository now provisions Azure Key Vault with RBAC, gives the API Container App a managed identity, seeds and consumes runtime secrets through Key Vault references instead of inline Container App values, removes committed LLM key material from tracked config, fails fast when non-development JWT secrets are missing, switches installer blob access to managed identity, adds workflow preflight validation and CI secret scanning, and documents operator runbooks for post-deployment validation and rotation.

Operator follow-up remains intentionally required for actions that cannot be safely completed from the repo: revoke and rotate any previously committed provider keys, populate GitHub environment bootstrap secrets, provide the GitHub deployment principal object ID for vault seeding RBAC, supply SQL Entra admin identity details if passwordless migration is pursued, validate actual Static Web App origins for tightened SignalR CORS, verify the managed-identity blob path in Azure before disabling storage shared keys, and run the documented post-deployment secret-resolution checks in Azure.
