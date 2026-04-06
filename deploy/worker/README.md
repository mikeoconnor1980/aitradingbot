# TradingApp Execution Agent

A Windows Service that executes trading strategies on Hyperliquid.  
Your private key **never leaves this machine** — all order signing happens locally.

## Quick Install

1. Open **PowerShell as Administrator**
2. Navigate to this folder
3. Run:
   ```powershell
   .\install.ps1
   ```
4. Enter your Hyperliquid private key when prompted

The service will start automatically and survive reboots.

## What Gets Installed

| Item | Location |
|------|----------|
| Service executable | `C:\Program Files\TradingApp\ExecutionAgent\` |
| Configuration | `C:\Program Files\TradingApp\ExecutionAgent\appsettings.json` |
| SQLite database | `C:\Program Files\TradingApp\ExecutionAgent\data\` |
| Private key | Machine environment variable `Hyperliquid__PrivateKey` |

## Configuration

Edit `appsettings.json` in the install directory to change:

- **Strategy settings** (market, timeframe, grid parameters)
- **Hyperliquid network** (testnet/mainnet)
- **Logging levels**

After editing, restart the service:
```powershell
Restart-Service -Name 'TradingApp.ExecutionAgent'
```

### Switching to Mainnet

In `appsettings.json`, change:
```json
{
  "Hyperliquid": {
    "BaseUrl": "https://api.hyperliquid.xyz",
    "WsBaseUrl": "wss://api.hyperliquid.xyz/ws",
    "Network": "mainnet"
  }
}
```

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

```powershell
# Removes service, keeps trade data and private key
.\uninstall.ps1

# Full removal including trade data and private key
.\uninstall.ps1 -RemoveData -RemoveKey
```

## Upgrading

Simply run `install.ps1` again — it stops the existing service, copies new files, and restarts.  
Your private key and trade data are preserved.

## Security

- Private key is stored as a **machine-level environment variable** (only accessible by admin/SYSTEM)
- The service runs as **Local System** by default
- All order signing happens locally — the key is never transmitted
- The service connects to Hyperliquid via HTTPS/WSS only
