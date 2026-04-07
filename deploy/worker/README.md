# TradingApp Execution Agent

A Windows Service that executes trading strategies on Hyperliquid.  
Your private key **never leaves this machine** — all order signing happens locally.

The agent connects to the TradingApp API (control plane) via periodic heartbeats,
picks up commands (Start / Stop / PlaceOrder / Cancel) from the dashboard,
and reports order results back. Trading sessions are started and stopped on-demand.

## Building the Installer

From the repo root, run:

```powershell
.\deploy\worker\build-installer.ps1
```

This publishes a self-contained single-file executable (`win-x64`) and produces:

- **Inno Setup installer** (`artifacts/installer/TradingApp-ExecutionAgent-v{version}-Setup.exe`) — recommended for clients
- **ZIP archive** (`artifacts/installer/TradingApp-ExecutionAgent-v{version}-win-x64.zip`) — manual install fallback
- **SHA256 hash** (`.sha256` file alongside the installer EXE)

Options:

```powershell
.\deploy\worker\build-installer.ps1 -NoZip          # skip ZIP creation
.\deploy\worker\build-installer.ps1 -NoInnoSetup     # skip installer EXE (requires Inno Setup 6)
```

**Prerequisites:** [Inno Setup 6](https://jrsoftware.org/isdl.php) must be installed for the Setup EXE.
Install via `winget install JRSoftware.InnoSetup`.

## Quick Install (Recommended)

Double-click `TradingApp-ExecutionAgent-v{version}-Setup.exe` and follow the wizard:

1. Accept the install location (default: `C:\Program Files\TradingApp\ExecutionAgent`)
2. Enter your Hyperliquid private key when prompted (skipped on upgrade if already set)
3. Click **Install**

The Windows Service registers automatically with delayed-auto start and failure recovery.

### Silent Install

For automated deployments or auto-update:

```powershell
.\TradingApp-ExecutionAgent-v0.1.0-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

### Manual Install (PowerShell)

If Inno Setup is not available, extract the ZIP and run:

```powershell
.\install.ps1                                  # interactive
.\install.ps1 -InstallDir "D:\TradingAgent"    # custom location
.\install.ps1 -NoStart                         # register but don't start
```

## What Gets Installed

| Item | Location |
|------|----------|
| Service executable | `C:\Program Files\TradingApp\ExecutionAgent\` |
| Configuration | `C:\Program Files\TradingApp\ExecutionAgent\appsettings.json` |
| SQLite database | `C:\Program Files\TradingApp\ExecutionAgent\data\` |
| Logs directory | `C:\Program Files\TradingApp\ExecutionAgent\logs\` |
| Private key | Machine environment variable `Hyperliquid__PrivateKey` |

**Service name:** `TradingApp.ExecutionAgent`  
**Display name:** `TradingApp Execution Agent`

## Configuration

Edit `appsettings.json` in the install directory. The main sections are:

### Hyperliquid Connection

```json
{
  "Hyperliquid": {
    "BaseUrl": "https://api.hyperliquid-testnet.xyz",
    "WsBaseUrl": "wss://api.hyperliquid-testnet.xyz/ws",
    "Network": "testnet"
  }
}
```

To switch to **mainnet**, change to:

```json
{
  "Hyperliquid": {
    "BaseUrl": "https://api.hyperliquid.xyz",
    "WsBaseUrl": "wss://api.hyperliquid.xyz/ws",
    "Network": "mainnet"
  }
}
```

### Control Plane (API)

The agent checks in with the API to receive commands and report status:

```json
{
  "Agent": {
    "ControlPlaneUrl": "http://localhost:5062"
  }
}
```

### Risk Limits

The live risk engine enforces these limits before any order is submitted:

```json
{
  "RiskLimits": {
    "MaxDailyLossUsd": 500,
    "MaxOpenOrders": 20,
    "MaxOrderSizeUsd": 10000,
    "CircuitBreakerCooldownMinutes": 60
  }
}
```

### Logging

Event Log writes go to the `Application` log under source `TradingApp.ExecutionAgent`.

After editing configuration, restart the service:

```powershell
Restart-Service -Name 'TradingApp.ExecutionAgent'
```

## Architecture

The agent runs three background services:

| Service | Purpose |
|---------|---------|
| **AgentCheckInService** | Heartbeats to the API every 5 s, picks up pending commands, reports order results and agent version |
| **LiveTradingService** | Connects to Hyperliquid WebSocket, assembles candles, runs strategy evaluation on each candle close |
| **HealthMonitorService** | Watchdog that logs warnings when trades or candles go stale, or the WebSocket disconnects |
| **UpdateCheckerService** | Downloads and applies agent updates when signalled by the API, with SafeToUpdate deferral |

Trading sessions are created on-demand when the dashboard sends a **Start** command and torn down on **Stop**.
Strategy configuration (market, timeframe, grid parameters) is delivered by the control plane — not stored locally.

## Managing the Service

```powershell
# Check status
Get-Service -Name 'TradingApp.ExecutionAgent'

# View recent logs
Get-EventLog -LogName Application -Source 'TradingApp.ExecutionAgent' -Newest 20

# Stop / Start / Restart
Stop-Service -Name 'TradingApp.ExecutionAgent'
Start-Service -Name 'TradingApp.ExecutionAgent'
Restart-Service -Name 'TradingApp.ExecutionAgent'
```

## Uninstall

Via **Add/Remove Programs** (if installed with the Setup EXE) or manually:

```powershell
# Removes service, keeps trade data and private key
.\uninstall.ps1

# Full removal including trade data and private key
.\uninstall.ps1 -RemoveData -RemoveKey
```

## Upgrading

### Auto-Update

The agent checks for updates via the API heartbeat. When a new version is configured
in the API's `AgentUpdate` appsettings:

1. The API signals `UpdateAvailable` on the next heartbeat
2. The agent checks if it's **safe to update** (no active trading session)
3. If positions are open, the update is **deferred** (re-checked every 5 min, max 4 hours)
4. Once safe, the installer is downloaded and its SHA256 hash verified
5. The Inno Setup installer runs silently — stops the service, replaces files, restarts

The dashboard shows the agent's update state: `None`, `Downloading`, `Applying`, `Failed`, or `Deferred`.

If the max deferral is exceeded, the agent logs a warning and waits for operator intervention
(use the kill switch to stop trading, then the update will proceed).

### Manual Upgrade

Run the Setup EXE again (or `install.ps1`) — it stops the existing service, copies new files, and restarts.
Your private key and trade data are preserved.

## Security

- Private key is stored as a **machine-level environment variable** (only accessible by admin/SYSTEM)
- The service runs as **Local System** by default
- All order signing happens locally — the key is never transmitted
- The service connects to Hyperliquid via HTTPS/WSS only
- The control plane connection uses HTTP by default (configure HTTPS for production)
