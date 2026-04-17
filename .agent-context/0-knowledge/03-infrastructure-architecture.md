# Infrastructure Architecture

## Overview

TradingApp now has two concrete runtime topologies:

- local development using direct host processes (`dotnet run` for the API and `ng serve` for the UI)
- Azure production deployment provisioned from Bicep

The repo does not contain a top-level `docker-compose.yml`. The only Docker artifact currently committed is `src/TradingApp.Api/Dockerfile`, and the production path is Azure-first rather than Docker Compose-first.

## Runtime Topologies

| Environment | Runtime Shape | Primary Storage | Real-Time Push |
|-------------|---------------|-----------------|----------------|
| Local development | `TradingApp.Api` + `TradingApp.Worker` + Angular dev server | SQLite | In-process SignalR from the API |
| Azure deployment | Azure Container App for API, Azure Static Web App for UI, client-side execution agent for order signing | Azure SQL for API-side data, local agent config for private key | Azure SignalR Service |

## Control Plane and Execution Split

The deployed architecture matches Business Model Option C.

Browser
-> Angular UI
-> `TradingApp.Api` control plane
-> agent command and state APIs
-> `TradingApp.ExecutionAgent` on the subscriber machine
-> Hyperliquid

Important boundary rules:

- the control plane stores wallet addresses and trading state
- the execution agent stores and uses the private key locally
- live signing happens through `MutableSignerProvider` and `LiveExecutionEngine`
- the API remains tenant-scoped and never needs access to customer private keys

## Local Development

Development runs directly from the repo:

- API: `src/TradingApp.Api`
- Worker: `src/TradingApp.Worker`
- UI: `frontend/trading-ui`

SQLite remains the default local database option. The current file locations are host-specific rather than a shared `/data/sqlite` mount:

| Host | Typical SQLite Path |
|------|---------------------|
| API | `src/TradingApp.Api/Data/tradingapp.db` |
| Worker | `src/TradingApp.Worker/Data/tradingapp.db` |

## Azure Deployment

`infrastructure/main.bicep` is the source of truth for the production Azure footprint. It provisions:

- Log Analytics workspace
- Azure SignalR Service
- Azure SQL Server and database
- Azure Container Apps environment
- Azure Container App for the API
- Azure Static Web App for the Angular UI

The Bicep entry point passes operational secrets and configuration, including the SQL admin password, JWT signing key, container registry credentials, and optional LLM API key.

## Real-Time Browser Updates

SignalR behavior is conditional and differs by hosting mode.

### Local and non-Azure mode

When `Azure:SignalR:ConnectionString` is not configured in the API host:

- the API registers plain SignalR
- `MarketDataStreamService` runs inside `TradingApp.Api`
- `UserEventStreamService` runs inside `TradingApp.Api`
- `HubContextSignalRPublisher` pushes directly to browser clients

This is the simplest local development path.

### Azure mode

When `Azure:SignalR:ConnectionString` is configured:

- the API uses `AddAzureSignalR(...)`
- the worker registers `AzureSignalRPublisher`
- `MarketDataStreamService` and `UserEventStreamService` run in the worker only when Azure SignalR is configured
- browser updates are pushed through Azure SignalR rather than a Redis backplane

The production backplane choice is Azure SignalR, not Redis.

## Streaming Components

| Component | Host | Purpose |
|-----------|------|---------|
| `MarketDataStreamService` | API locally, Worker in Azure mode | Subscribes to Hyperliquid trade streams, builds confirmed candles, and publishes market updates |
| `UserEventStreamService` | API locally, Worker in Azure mode | Streams order and fill events for the authenticated wallet |
| `HubContextSignalRPublisher` | API | Local publisher for direct SignalR hub messaging |
| `AzureSignalRPublisher` | Infrastructure, used by Worker | Production publisher backed by `Microsoft.Azure.SignalR.Management` |

SignalR still requires a credentialed CORS policy. The API configures allowed origins from `Cors:AllowedOrigins` and calls `AllowCredentials()`.

## Network Routing and Per-User Exchange Access

The API supports per-user network routing between Hyperliquid mainnet and testnet.

| Component | Role |
|-----------|------|
| `UserNetworkProvider` | Reads `User.PreferredNetwork` and falls back to configured defaults |
| `NetworkRoutingHandler` | Rewrites outgoing Hyperliquid HTTP requests to the correct base URL per request |
| `HyperliquidOptions` | Holds default network and base URL configuration |

This allows different users to operate against different Hyperliquid environments without forking the API host.

## Secrets and Configuration

Current secret handling is mixed:

- worker private keys are expected through local config or environment variables on the execution agent
- JWT secret, SQL credentials, and registry credentials are currently passed as Bicep parameters for Azure deployment
- Google OAuth and other app settings are configuration-bound in the API host

Azure Key Vault is not yet wired into the deployment path. That gap is important because cloud secret material is still injected directly through deployment configuration.

## Future Recommendations

- Add Azure Key Vault integration for API secrets and deployment-time secret references.
- Add Redis Cache or another shared state layer only if horizontal real-time fan-out needs exceed Azure SignalR alone.
- Add horizontal worker execution through Azure Container Apps Jobs or equivalent for server-side processing that does not require private keys.
- Add Application Insights and distributed tracing across API, background services, and agent check-in flows.