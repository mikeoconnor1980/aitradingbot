# Hyperliquid RWA & Stock Perpetuals (HIP-3)

> Last updated: 2026-04-09

## Overview

Stock, commodity, index, and FX perpetual futures are available on Hyperliquid via **HIP-3 builder-deployed perpetuals**. These are _not_ part of the main validator-operated perps universe (229 crypto assets). They are separate perp DEXs built on HyperCore by third-party deployers who stake 500k HYPE.

The primary deployer is **Trade.xyz** (prefix `XYZ:`). A secondary deployer **Cash** (prefix `CASH:`) also operates competing markets.

## HIP-3 Mechanism

- Deployers provide oracle prices, set leverage limits, and can settle markets via `haltTrading`.
- Fees are 2x normal perp fees (taker 0.0090%, maker 0.0030%) with 50% going to the deployer.
- Funding rate uses a modified premium formula with deployer-configurable funding rate multiplier.
- Asset ID scheme: `100000 + perp_dex_index * 10000 + index_in_meta`.
- API naming: `{dex}:{coin}` format (e.g. `xyz:TSLA`, `cash:USA500`).
- HIP-3 perps appear in `allMids` endpoint but NOT in the main `meta` endpoint.
- Cross-margin eligibility is determined by the deployer.

## Currently Available Assets (as of April 2026)

### US Equities

| Ticker        | Company              |
|---------------|----------------------|
| `XYZ:TSLA`    | Tesla                |
| `XYZ:NVDA`    | Nvidia               |
| `XYZ:GOOGL`   | Alphabet / Google    |
| `XYZ:META`    | Meta Platforms       |
| `XYZ:AMZN`    | Amazon               |
| `XYZ:AAPL`    | Apple                |
| `XYZ:MSFT`    | Microsoft            |
| `XYZ:NFLX`    | Netflix              |
| `XYZ:AMD`     | AMD                  |
| `XYZ:INTC`    | Intel                |
| `XYZ:MU`      | Micron Technology    |
| `XYZ:TSM`     | TSMC                 |
| `XYZ:ORCL`    | Oracle               |
| `XYZ:PLTR`    | Palantir             |
| `XYZ:COIN`    | Coinbase             |
| `XYZ:HOOD`    | Robinhood            |
| `XYZ:MSTR`    | MicroStrategy        |
| `XYZ:RIVN`    | Rivian               |
| `XYZ:HIMS`    | Hims & Hers          |
| `XYZ:DKNG`    | DraftKings           |
| `XYZ:COST`    | Costco               |
| `XYZ:LLY`     | Eli Lilly            |
| `XYZ:SNDK`    | SanDisk / WD         |
| `XYZ:BABA`    | Alibaba              |
| `XYZ:CRCL`    | Circle (pre-IPO)     |
| `XYZ:CRWV`    | CoreWeave            |

### International Equities

| Ticker          | Company       |
|-----------------|---------------|
| `XYZ:SKHX`     | SK Hynix      |
| `XYZ:SMSN`     | Samsung       |
| `XYZ:HYUNDAI`  | Hyundai       |

### Index Perps

| Ticker        | Index                        |
|---------------|------------------------------|
| `XYZ:SP500`   | S&P 500                      |
| `XYZ:XYZ100`  | XYZ custom 100 index         |
| `XYZ:JP225`   | Nikkei 225                   |
| `XYZ:KR200`   | KOSPI 200                    |
| `CASH:USA500`  | S&P 500 (Cash deployer)     |

### ETFs

| Ticker       | ETF                            |
|--------------|--------------------------------|
| `XYZ:EWY`   | iShares MSCI South Korea       |
| `XYZ:EWJ`   | iShares MSCI Japan             |
| `XYZ:URNM`  | Uranium Miners ETF             |
| `XYZ:USAR`  | US ETF                         |

### Commodities

| Ticker          | Commodity        |
|-----------------|------------------|
| `XYZ:CL`       | WTI Crude Oil    |
| `XYZ:BRENTOIL` | Brent Crude      |
| `XYZ:GOLD`     | Gold             |
| `XYZ:SILVER`   | Silver           |
| `XYZ:PLATINUM` | Platinum         |
| `XYZ:PALLADIUM`| Palladium        |
| `XYZ:COPPER`   | Copper           |
| `XYZ:NATGAS`   | Natural Gas      |

### FX

| Ticker     | Pair     |
|------------|----------|
| `XYZ:EUR`  | EUR/USD  |
| `XYZ:JPY`  | USD/JPY  |

> Trade.xyz actively adds new assets. This list may be outdated — query the `allMids` endpoint and filter for colon-delimited keys to get the current set.

## Dividend Implications for Stock Perps

These are perpetual futures tracking stock prices via oracle feeds — holders do **not** own the underlying shares.

### Key Risks

1. **No direct dividend payments.** HIP-3 stock perp holders never receive dividends from the underlying company.

2. **Oracle price drops on ex-dividend dates.** The underlying stock price drops by approximately the dividend amount on the ex-date. The oracle feeding the perp price reflects this drop:
   - **LONG positions** suffer an unrealized loss ≈ dividend amount per share with no offsetting payment.
   - **SHORT positions** benefit from the price drop, gaining ≈ dividend amount per share.

3. **Deployer adjustments are possible but not guaranteed.** Whether Trade.xyz adjusts oracle prices or makes cash settlements to account for dividends depends entirely on their contract specification. Traditional CFD brokers typically credit longs and debit shorts for dividends, but HIP-3 deployers have no protocol-level obligation to do this.

4. **Funding rate may partially offset.** The perpetual funding mechanism could indirectly reflect dividend expectations (shorts pay longs if the perp trades at a discount), but this is unreliable and market-dependent.

### Practical Guidance

- Check Trade.xyz contract terms for explicit dividend handling policy.
- Monitor ex-dividend dates for any stock perp you hold through earnings season.
- Consider closing or hedging long positions in high-dividend stocks ahead of ex-dates if no deployer adjustment is confirmed.
- Being short a dividend-paying stock perp over ex-dates is economically advantageous if no adjustment occurs.

## Ex-Dividend Date Data Feeds

APIs that provide ex-dividend dates for integration into the trading bot:

| Provider                  | Endpoint / Function                                 | Cost                          | Notes                                                        |
|---------------------------|-----------------------------------------------------|-------------------------------|--------------------------------------------------------------|
| **Alpha Vantage**         | `function=DIVIDENDS&symbol={TICKER}`                | Free (25 req/day), paid plans | Returns historical + declared future dividends with ex-dates. C#/.NET examples provided. |
| **Polygon.io**            | `GET /v3/reference/dividends`                       | Free (5 req/min), paid plans  | REST API with ex_dividend_date, pay_date, amount. Good for bulk. |
| **Financial Modeling Prep**| `/api/v3/stock_dividend_calendar`                  | Free tier available           | Calendar endpoint for upcoming ex-dates.                     |
| **IEX Cloud**             | `/stock/{symbol}/dividends`                         | Paid                          | Comprehensive corporate action data.                         |

### Recommended: Alpha Vantage

Best fit for the .NET stack. Free API key to start. Returns ex_date, declaration_date, record_date, payment_date, and dividend amount per share.

```
GET https://www.alphavantage.co/query?function=DIVIDENDS&symbol=MSFT&apikey=YOUR_KEY
```

## API Integration Notes

### Querying HIP-3 Assets

HIP-3 assets do NOT appear in the standard `meta` endpoint. To discover them:

```
POST https://api.hyperliquid.xyz/info
{"type": "allMids"}
```

Filter the response keys for colon-delimited names (e.g. `xyz:TSLA`). The `meta` endpoint only returns the ~229 main validator-operated crypto perps.

### HIP-3 Fill Format

Fills for HIP-3 perps use the `{dex}:{coin}` naming in the `coin` field:

```json
{
  "coin": "xyz:XYZ100",
  "px": "25006.76",
  "sz": "0.1",
  "side": "B"
}
```

### Trading Hours

Stock perps on Trade.xyz may follow traditional market hours for oracle price updates, though the perp contract itself is tradeable 24/7 on Hyperliquid. Liquidity and spread quality will vary outside US market hours.

## Scale & Traction (as of March 2026)

- Hyperliquid total open interest: ~$7.4B
- RWA open interest exceeded $2.3B (all-time high)
- S&P 500 perp (`XYZ:SP500`) hit $100M OI within one day of launch (March 20, 2026)
- Trade.xyz `XYZ:SP500` OI: ~$224M
- Trade.xyz `XYZ:CL` (crude oil) OI: ~$427M
