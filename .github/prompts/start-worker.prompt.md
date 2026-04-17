---
mode: agent
description: Start the TradePilot Worker with Hyperliquid testnet credentials
---

Run the following commands in a terminal:

```powershell
$env:Hyperliquid__PrivateKey = "62e5b67b3fcfe4e7621d61be4f5a0d1f768e70f693dbee14b3e6b085431f0ae5"
cd src/TradePilot.Worker
dotnet run
```
