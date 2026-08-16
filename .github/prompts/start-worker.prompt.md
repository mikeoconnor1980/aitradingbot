---
mode: agent
description: Start the TradePilot Worker with Hyperliquid testnet credentials
---

Run the following commands in a terminal:

```powershell
$env:Hyperliquid__PrivateKey = "<testnet-api-wallet-private-key>"
cd src/TradePilot.Worker
dotnet run
```
