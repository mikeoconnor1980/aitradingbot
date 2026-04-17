# Azure Deployment Infrastructure

This document covers the current Azure deployment path for the control plane and the parallel installer-based distribution path for the client-side execution agent.

## Architecture Overview

The system is deployed as two operational planes:

| Plane | Hosting | Purpose |
|---|---|---|
| Control Plane | Azure Container Apps + Azure Static Web Apps + Azure SQL + Azure SignalR | API, UI, persistence, and browser-facing real-time features |
| Execution Plane | Subscriber Windows machine | `TradePilot.ExecutionAgent` Windows Service for local signing and live execution |

## Azure Resource Inventory

Infrastructure is provisioned from `infrastructure/main.bicep` and the modules under `infrastructure/modules/`.

| Resource | Module | Purpose |
|---|---|---|
| Log Analytics Workspace | `modules/log-analytics.bicep` | Container Apps logging |
| Container Apps Environment | `modules/container-app-environment.bicep` | Shared host environment for the API |
| Container App | `modules/container-app.bicep` | Hosts the API |
| Azure SQL Server and Database | `modules/sql-server.bicep` | API persistence in Azure environments |
| Azure SignalR Service | `modules/signalr.bicep` | Browser push in Azure mode |
| Static Web App | `modules/static-web-app.bicep` | Angular UI deployment |

## Runtime and Build Versions

The current repo is on **.NET 10**, not .NET 8.

Evidence in the build chain includes:

- `.github/workflows/deploy.yml` sets `DOTNET_VERSION: 10.0.x`
- the worker and API projects build against .NET 10 packages
- local and CI artifacts are emitted under `net10.0`

Any older references to .NET 8 in deployment docs are stale.

## CI/CD Pipelines

### `deploy.yml`

The main GitHub Actions workflow handles:

1. restore, build, and test
2. Snyk scanning
3. API container build and push to GHCR
4. frontend build and Azure Static Web Apps deployment
5. API deployment to Azure Container Apps

### Registry Credential Distinction

Two different GitHub credentials are used for two different jobs:

| Secret | Used For |
|---|---|
| `GITHUB_TOKEN` | Logging in during the GitHub Actions image push step to GHCR |
| `GHCR_PAT` | Configuring Azure Container Apps so Azure can pull the GHCR image at runtime |

This distinction matters because the workflow push and the Azure runtime pull are separate authentication paths.

## Bicep Inputs and Outputs

The Bicep entry point still provides the core deployment parameters for the Azure control plane:

- container image reference
- SQL admin credentials
- JWT signing secret
- optional LLM API settings
- CORS origin
- registry credentials for image pull

The output surface remains focused on operational endpoints such as the API URL, Static Web App hostname, SignalR hostname, and SQL server FQDN.

## Control Plane Hosting Notes

The deployed Azure control plane uses:

- Azure Container Apps for the API
- Azure Static Web Apps for the Angular UI
- Azure SQL for persistence
- Azure SignalR for browser-facing real-time updates

The worker is not hosted in Azure as a live execution service. Azure hosts the control plane only.

## Execution Agent Packaging and Distribution

The worker distribution path is operational and should be treated as part of deployment architecture, not as an afterthought.

### Packaging Pipeline

`deploy/worker/build-installer.ps1` builds the execution-agent release package.

Artifacts include:

- `TradePilot-ExecutionAgent-v{version}-Setup.exe`
- `TradePilot-ExecutionAgent-v{version}-win-x64.zip`
- SHA256 checksum files for validation and update verification

### Worker Build Shape

`TradePilot.ExecutionAgent` is published as:

- self-contained
- single-file in Release
- `win-x64`
- Windows Service-ready

### Installation and Update Model

The execution agent is installed through Inno Setup or PowerShell helper scripts. Silent installer flags are also used by the auto-update path driven by `UpdateCheckerService`.

## Security Notes

- The control plane does not hold customer private keys.
- Azure login in CI uses federated identity rather than stored Azure credentials.
- Container App pulls use configured secrets rather than public registry access.
- Worker updates are SHA256-verified before installer launch.

## Future Recommendations

- Add Azure Key Vault integration so Bicep and runtime configuration stop relying on directly passed secrets.
- Add a dedicated release workflow for worker installer publishing and version metadata management.
- Add environment-specific deployment notes for production versus development resource sizing.
- Add operational guidance for how agent update binaries are hosted and retained over time.
