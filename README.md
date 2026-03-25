# AI Trading Bot

Hyperliquid perpetuals trading platform with an Angular dashboard and .NET API backend.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/) (includes npm)
- A MetaMask (or similar EVM) wallet for Hyperliquid testnet

## Getting Started

### 1. Clone and restore

```bash
git clone <repo-url> && cd aitradingbot
dotnet restore
cd frontend/trading-ui && npm install && cd ../..
```

### 2. Configure your Hyperliquid private key

Add your wallet private key to `src/TradingApp.Api/appsettings.Development.json`:

```json
{
  "Hyperliquid": {
    "PrivateKey": "YOUR_PRIVATE_KEY_HERE"
  }
}
```

Or set it as an environment variable:

```bash
export Hyperliquid__PrivateKey=YOUR_PRIVATE_KEY_HERE
```

> **How to get a testnet private key:**
> 1. Install MetaMask and create a **dedicated test wallet** (don't use your main wallet)
> 2. Export the private key: MetaMask → Account Details → Show Private Key
> 3. Connect the same wallet to [Hyperliquid Testnet](https://app.hyperliquid-testnet.xyz) to get testnet USDC
>
> **Never commit your private key to source control.**

### 3. Start the backend

```bash
dotnet run --project src/TradingApp.Api
```

The API starts at `http://localhost:5062`.

### 4. Start the frontend

```bash
cd frontend/trading-ui
npx ng serve
```

The dashboard opens at `http://localhost:4200`. API requests are proxied to the backend automatically.

## Running Tests

```bash
# Backend tests
dotnet test

# Frontend build + lint
cd frontend/trading-ui
npx ng build
npx ng lint
```

## Project Structure

| Project | Role |
|---------|------|
| `TradingApp.Api` | ASP.NET Core Web API host |
| `TradingApp.Application` | CQRS commands/queries, interfaces |
| `TradingApp.Infrastructure` | Hyperliquid client, signing |
| `TradingApp.Domain` | Core domain entities |
| `TradingApp.Persistence` | EF Core context (scaffolded) |
| `TradingApp.Worker` | Background strategy execution |
| `frontend/trading-ui` | Angular 19 dashboard |

## API Endpoints

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/health` | Exchange connectivity check |
| `GET` | `/api/account` | Account summary (equity, margin, PnL) |
| `GET` | `/api/account/positions` | Open positions |
| `GET` | `/api/account/orders` | Open orders |