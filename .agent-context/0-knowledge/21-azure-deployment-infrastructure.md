# Azure Deployment Infrastructure

This document describes the Azure cloud infrastructure, CI/CD pipelines, and local Execution Agent deployment that together form the TradingApp (TradePilot) production system.

---

## Architecture Overview

The platform follows a split-plane architecture:

| Plane | Hosting | Purpose |
|-------|---------|---------|
| **Control Plane** (API + UI) | Azure (cloud) | Dashboard, strategy management, analytics, real-time streaming |
| **Execution Plane** (Worker) | Client machine (Windows Service) | Order signing and execution — private keys never leave the client |

```
Browser
  ↓
Azure Static Web App (Angular UI)
  ↓  HTTPS
Azure Container App (API)
  ↓  SignalR (Azure SignalR Service)
  ↓  SQL (Azure SQL)
  ↓  Heartbeat / Commands
Client Windows Service (Execution Agent)
  ↓  Signed orders
Hyperliquid DEX
```

---

## Azure Resources

All resources are provisioned via **Bicep** templates in `infrastructure/`. The naming convention is `{appName}-{env}-{resource}` (e.g. `tradepilot-dev-api`).

### Resource Inventory

| Resource | Bicep Module | SKU / Tier | Purpose |
|----------|-------------|------------|---------|
| Log Analytics Workspace | `modules/log-analytics.bicep` | PerGB2018, 30-day retention | Centralised logging for Container Apps |
| Container Apps Environment | `modules/container-app-environment.bicep` | — | Hosting environment wired to Log Analytics |
| Container App (API) | `modules/container-app.bicep` | 0.25 vCPU / 0.5 Gi RAM | .NET 8 API, scales 0–2 replicas |
| Azure SQL Server + Database | `modules/sql-server.bicep` | GP_S_Gen5 (Serverless), 1 vCore, 2 GB | Persistence layer (auto-pause after 60s idle) |
| Azure SignalR Service | `modules/signalr.bicep` | Free_F1, Serverless mode | Real-time streaming to UI and Execution Agent |
| Static Web App | `modules/static-web-app.bicep` | Free | Angular frontend (deployed to `westeurope` — SWA not available in `uksouth`) |

### Resource Group

Resources are grouped by environment: `rg-tradepilot-dev` / `rg-tradepilot-prod`.  
Primary region: **UK South** (`uksouth`), except Static Web Apps which deploy to **West Europe**.

---

## Bicep Template Structure

```
infrastructure/
├── main.bicep            # Orchestrator — wires all modules together
├── main.bicepparam       # Default parameter values
└── modules/
    ├── container-app.bicep
    ├── container-app-environment.bicep
    ├── log-analytics.bicep
    ├── signalr.bicep
    ├── sql-server.bicep
    └── static-web-app.bicep
```

### Key Parameters (`main.bicep`)

| Parameter | Description |
|-----------|-------------|
| `environmentName` | `dev` or `prod` |
| `containerImage` | GHCR image ref (e.g. `ghcr.io/{owner}/tradepilot-api:latest`) |
| `sqlAdminLogin` / `sqlAdminPassword` | Azure SQL credentials |
| `jwtSecretKey` | JWT signing key for API authentication |
| `llmApiKey` | Gemini API key for LLM context provider |
| `corsAllowedOrigin` | Static Web App URL for CORS |
| `registryUsername` / `registryPassword` | GHCR pull credentials |

### Template Outputs

| Output | Value |
|--------|-------|
| `apiUrl` | Container App FQDN |
| `staticWebAppUrl` | SWA default hostname |
| `signalRHostName` | SignalR service hostname |
| `sqlServerFqdn` | SQL Server FQDN |

---

## Container App Configuration

The API Container App is configured with:

- **Ingress**: External HTTP on port 8080
- **CORS**: Allows the SWA origin + `http://localhost:4200` for local development
- **Health probes**: Liveness (`/healthz`, 30s initial delay, 60s interval) and Readiness (`/healthz`, 10s initial delay, 15s interval)
- **Scaling**: 0–2 replicas based on HTTP concurrency (10 concurrent requests threshold)
- **Registry**: Pulls images from `ghcr.io` using a PAT stored as a secret

### Secrets Injected

| Environment Variable | Source |
|---------------------|--------|
| `ConnectionStrings__DefaultConnection` | Azure SQL connection string |
| `Azure__SignalR__ConnectionString` | SignalR connection string |
| `Jwt__SecretKey` | JWT signing key |
| `LlmContext__ApiKey` | Gemini API key |
| `LlmReview__ApiKey` | Gemini API key (shared) |
| `Cors__AllowedOrigins__0` | SWA URL |

---

## CI/CD Pipelines

### `deploy.yml` — Build and Deploy (on push to `main`)

A multi-job pipeline triggered on every push to `main`:

```
build-and-test
  ├── .NET restore / build / test
  ├── Snyk security scan (.NET + npm)
  │
  ├─→ build-api-image
  │     └── Docker build → push to ghcr.io/{owner}/tradepilot-api:{sha}
  │     └─→ deploy-api
  │           └── az containerapp update (dev environment)
  │
  └─→ build-frontend
        └── npm ci → ng build --production (with Azure env config)
        └─→ deploy-frontend
              └── Azure Static Web Apps deploy
```

**Key details:**
- Container image is tagged with the Git SHA and `latest`
- Frontend build injects the API FQDN via environment file substitution
- Azure login uses **federated identity** (OIDC) — no stored credentials
- GHCR pull uses `GHCR_PAT` secret for Container App registry access

### `deploy-infra.yml` — Infrastructure Deployment (manual)

A `workflow_dispatch` pipeline for provisioning/updating Azure resources:

1. Creates the resource group (`rg-tradepilot-{env}`)
2. Deploys `infrastructure/main.bicep` with secrets from GitHub environment
3. Outputs resource FQDNs

Triggered manually via GitHub Actions UI with environment selection (`dev` / `prod`).

### GitHub Secrets Required

| Secret | Used By |
|--------|---------|
| `AZURE_CLIENT_ID` | Federated identity login |
| `AZURE_TENANT_ID` | Federated identity login |
| `AZURE_SUBSCRIPTION_ID` | Federated identity login |
| `SQL_ADMIN_LOGIN` | Bicep deployment |
| `SQL_ADMIN_PASSWORD` | Bicep deployment |
| `JWT_SECRET_KEY` | Bicep deployment |
| `LLM_API_KEY` | Bicep deployment |
| `SWA_URL` | CORS origin for Bicep |
| `SWA_DEPLOYMENT_TOKEN` | Static Web App deploy |
| `GHCR_PAT` | Container App registry pull |
| `API_FQDN` | Frontend environment config |
| `SNYK_TOKEN` | Security scanning |

---

## Docker Image

The API Dockerfile (`src/TradingApp.Api/Dockerfile`) uses a multi-stage build:

| Stage | Base Image | Purpose |
|-------|-----------|---------|
| `restore` | `mcr.microsoft.com/dotnet/sdk:8.0` | NuGet restore (cached layer) |
| `publish` | (from restore) | Build and publish Release |
| `final` | `mcr.microsoft.com/dotnet/aspnet:8.0` | Runtime — runs as non-root `appuser` on port 8080 |

---

## Execution Agent (Client-Side Worker)

The Execution Agent runs on the client's Windows machine as a Windows Service. It communicates with the Control Plane API via heartbeats and receives commands (Start/Stop/PlaceOrder/Cancel).

### Deployment Options

| Method | Tool | Use Case |
|--------|------|----------|
| **Inno Setup installer** | `deploy/worker/installer.iss` | Recommended for clients — GUI wizard |
| **PowerShell scripts** | `deploy/worker/install.ps1` | Manual/automated installs |
| **Silent install** | Inno Setup `/VERYSILENT` | Auto-update scenarios |

### Build Process

```powershell
.\deploy\worker\build-installer.ps1
```

Produces:
- `artifacts/installer/TradingApp-ExecutionAgent-v{version}-Setup.exe` (Inno Setup installer)
- `artifacts/installer/TradingApp-ExecutionAgent-v{version}-win-x64.zip` (fallback)
- `.sha256` checksum file

The Worker is published as a **self-contained single-file** `win-x64` executable.

### Installation Layout

| Item | Path |
|------|------|
| Service executable | `C:\Program Files\TradingApp\ExecutionAgent\` |
| Configuration | `...\appsettings.json` |
| SQLite database | `...\data\` |
| Logs | `...\logs\` |

### Environment Variables (Machine-Level)

| Variable | Purpose |
|----------|---------|
| `Hyperliquid__PrivateKey` | Private key for order signing (0x + 64 hex chars) |
| `ControlPlane__BaseUrl` | API URL for heartbeat/command sync |
| `Azure__SignalR__ConnectionString` | Real-time streaming to UI |

### Service Configuration

- **Service name**: `TradingApp.ExecutionAgent`
- **Start type**: Delayed auto-start
- **Recovery**: Restart on failure (30s → 60s → 120s backoff, resets after 24h)
- **Uninstall**: Preserves `data/` directory by default (trade history); use `-RemoveData` to delete

---

## Security Considerations

- **Private keys never leave the client machine** — the Execution Agent signs orders locally
- Azure login uses **OIDC federated identity** — no long-lived Azure credentials in CI
- SQL Server enforces **TLS 1.2 minimum**
- Container App runs as **non-root user** (`appuser`)
- Secrets are stored in **Container App secrets** (not in environment variables or config files)
- Snyk scans run on every PR for both .NET and npm dependencies
- Execution Agent private key is stored as a **machine-level environment variable** (not in config files)

---

## Cost Optimization

The infrastructure is designed for minimal cost during early stages:

- **Azure SQL**: Serverless Gen5 with auto-pause (60s idle) and 0.5 vCore minimum
- **Container App**: Scales to 0 replicas when idle
- **SignalR**: Free tier (F1)
- **Static Web App**: Free tier
- **Log Analytics**: Pay-per-GB with 30-day retention
