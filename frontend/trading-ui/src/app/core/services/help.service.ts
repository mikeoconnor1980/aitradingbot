import { inject, Injectable } from "@angular/core";
import { BehaviorSubject } from "rxjs";
import { HelpChatMessage, HelpChatResponse, HelpTopic } from "../models/help-topic.model";
import { ApiRestClient } from "./api-rest-client.service";

@Injectable({ providedIn: "root" })
export class HelpService {
  private readonly _api = inject(ApiRestClient);
  private readonly _open$ = new BehaviorSubject<boolean>(false);
  private readonly _activeTopic$ = new BehaviorSubject<HelpTopic | null>(null);
  private readonly _chatMessages$ = new BehaviorSubject<HelpChatMessage[]>([]);
  private readonly _chatLoading$ = new BehaviorSubject<boolean>(false);

  public readonly open$ = this._open$.asObservable();
  public readonly activeTopic$ = this._activeTopic$.asObservable();
  public readonly chatMessages$ = this._chatMessages$.asObservable();
  public readonly chatLoading$ = this._chatLoading$.asObservable();

  public readonly topics: HelpTopic[] = [
    {
      id: "subscriptions",
      title: "Subscriptions & Tiers",
      icon: "workspace_premium",
      content: `## Subscriptions & Tiers

TradePilot currently offers two testing tiers with a **1-year trial** period.

### Beginner
- Access to **2 admin-selected strategy-library templates**
- Can trade **BTC** and **ETH** only
- Maximum **5x leverage**
- **No AI review**
- **No Macro Calendar**
- **No Strategy Optimizer**

### Pro
- Access to the **full strategy library**
- Can trade **all supported assets**
- Uses the normal asset-level exchange leverage limits
- Includes **AI review**, **Macro Calendar**, and **Strategy Optimizer**

### Managing your tier
- Open **Profile** to start a Beginner or Pro trial
- You can cancel your current tier from **Profile**
- The platform enforces tier restrictions in both the UI and the API`
    },
    {
      id: "dashboard",
      title: "Dashboard",
      icon: "dashboard",
      content: `## Dashboard

The **Dashboard** is your main overview page that shows the current state of your trading activity at a glance.

### What you'll see
- **Account balance** and equity summary from your Hyperliquid account
- **Open positions** with real-time PnL updates via SignalR
- **Active strategies** and their current status
- **Recent orders** and fill history

### Tips
- The dashboard auto-refreshes via the WebSocket connection — no need to reload.
- Click the connection status pill in the top-right to diagnose connectivity issues.
- Position PnL colours: green for profit, red for loss.`
    },
    {
      id: "market-data",
      title: "Market Data",
      icon: "candlestick_chart",
      content: `## Market Data

The **Market Data** page provides real-time price charts and market information for available trading pairs.

### Features
- **Interactive candlestick charts** powered by Lightweight Charts
- **Multiple timeframes** — switch between 1m, 5m, 15m, 1h, 4h, 1d
- **Asset selector** — choose from all available Hyperliquid perpetual pairs
- **Real-time updates** — candles update live via the SignalR WebSocket connection

### How to use
1. Select an asset from the dropdown at the top
2. Choose your preferred timeframe
3. The chart will load historical candles and stream live updates
4. Hover over candles to see OHLCV details`
    },
    {
      id: "order-entry",
      title: "Order Entry",
      icon: "swap_vert",
      content: `## Order Entry

The **Order Entry** page lets you place manual orders on Hyperliquid.

    ### Subscription-aware behavior
    - The **asset dropdown** only shows assets allowed by your current tier
    - The **leverage slider** is capped by your tier
    - Beginner users are limited to **BTC/ETH** and **5x leverage**
    - The API will reject disallowed assets or leverage even if a stale page tries to submit them

### Order types
- **Market orders** — execute immediately at the best available price
- **Limit orders** — set a specific price; the order fills when the market reaches it

### Fields
- **Asset** — the perpetual contract to trade
- **Side** — Long (buy) or Short (sell)
- **Size** — position size in the asset's base currency
- **Price** — required for limit orders

### Important
- All orders pass through the **Risk Engine** before submission.
- Double-check your size and side before submitting.
- You can view open orders and cancel them from this page.`
    },
    {
      id: "backtesting",
      title: "Backtesting",
      icon: "history",
      content: `## Backtesting

The **Backtesting** page lets you test strategies against historical market data before risking real capital.

### How it works
1. **Select a strategy** from your saved strategies
2. **Choose a date range** and asset pair
3. **Configure parameters** — initial capital, fee rates, etc.
4. **Run the backtest** — the replay engine processes historical candles through the same strategy engine and derived-signal evaluation used in live trading

### Results
- **Equity curve** — visual representation of your portfolio value over time
- **Trade list** — every entry and exit with timestamps and PnL
- **Performance metrics** — total return, max drawdown, Sharpe ratio, win rate, profit factor

### Key principle
Backtesting uses the **same StrategyEngine, derived-signal engine, GridController, and RiskEngine** as live trading — what you see in backtesting closely reflects real execution.`
    },
    {
      id: "candle-data",
      title: "Candle Data",
      icon: "storage",
      content: `## Candle Data

The **Candle Data** page manages your local historical candle database, which is essential for backtesting and strategy analysis.

### Features
- **View ingested data** — see which assets and timeframes have been downloaded
- **Trigger ingestion** — download historical candles from the exchange
- **Gap detection** — identify missing periods in your candle data
- **Data quality** — verify candle counts and date ranges

### Why it matters
Backtesting quality depends entirely on the quality and completeness of your candle data. Use this page to ensure you have full coverage for your target assets and timeframes before running backtests.`
    },
    {
      id: "optimizer",
      title: "Strategy Optimizer",
      icon: "tune",
      content: `## Strategy Optimizer

The **Optimizer** helps you find the best parameter combinations for your trading strategies.

    ### Access
    - The Optimizer is available to **Pro** users only

### How it works
1. **Select a strategy** to optimize
2. **Define parameter ranges** — min, max, and step for each parameter you want to test
3. **Choose an objective** — maximize return, minimize drawdown, maximize Sharpe, etc.
4. **Run the optimization** — the system backtests every parameter combination

### Results
- **Parameter heatmaps** — visualize which parameter values perform best
- **Top combinations** — ranked list of the best-performing parameter sets
- **Out-of-sample validation** — guards against overfitting

### Tips
- Start with wide parameter ranges, then narrow in on promising regions.
- Always validate top results on out-of-sample data.
- Beware of overfitting — the best in-sample result isn't always the best going forward.`
    },
    {
      id: "strategies",
      title: "Strategies",
      icon: "psychology",
      content: `## Strategies

The **Strategies** page is where you create, edit, and manage your trading strategies.

### Strategy builder
- **Visual configuration** — build strategies with entry conditions, exit rules, and risk parameters
- **AI review** — get an LLM-powered review of your strategy before deploying (Pro only)
- **Natural language** — describe a strategy in plain English and let the AI interpret it into configuration

### Subscription-aware behavior
- Beginner users only see the **2 strategy-library templates** explicitly marked as Beginner-visible by admins
- Beginner strategies are limited to **BTC/ETH** and **5x leverage**
- Strategy validation and template cloning enforce those limits on the server as well as in the UI

### Strategy modes
The platform supports two strategy modes:

**Signal mode** — the primary strategy mode:
- Define **entry conditions** using technical indicators and price-structure signals: RSI, MACD, Price vs EMA, Support/Resistance, Candle Pattern, Liquidity Sweep, and Structure Shift
- Combine conditions with **all** (every condition must match) or **any** (at least one) logic
- Use the **derived-signal engine** for higher-order structure detection such as candle patterns, sweep/reclaim setups, and local market-structure shifts
- Set **trend filters** (EMA cross, SMA cross, price above EMA) to only trade in favourable conditions
- Configure **exit rules**: fixed percent take-profit/stop-loss, swing-low trailing stops, or ATR-based trailing stops
- Choose direction: Long, Short, or Both

**Grid mode** — deploys a pullback grid:
- Set the number of grid levels and spacing
- Configure entry mode (auto from signal candle or manual anchor price)
- Includes breakdown threshold protection

### Risk management
- **Position sizing** — percent of wallet or fixed notional
- **Leverage** control
- **Max open trades** limit
- **Cooldown** between entries (candles or minutes)

### Lifecycle
1. **Draft** — create and configure your strategy
2. **Review** — optionally get an AI review
3. **Backtest** — test against historical data
4. **Deploy** — activate for live or paper trading

### Key principle
Strategies execute on **confirmed candle closes only** — this ensures deterministic, reproducible execution across backtesting and live trading.`
    },
    {
      id: "connection",
      title: "Connection Status",
      icon: "wifi",
      content: `## Connection Status

The **Connection Status** page shows the health of your connection to the Hyperliquid exchange and the platform's backend services.

### Status indicators
- **Green** — fully connected and receiving data
- **Yellow** — reconnecting (temporary interruption)
- **Red** — disconnected

### What's monitored
- **SignalR WebSocket** — real-time data feed for prices, positions, and orders
- **REST API** — backend service health
- **Exchange connectivity** — connection to Hyperliquid's API
- **Network** — whether you're on Testnet or Mainnet

### Troubleshooting
- If disconnected, the system will automatically attempt to reconnect.
- Check that the backend API is running.
- Verify your Hyperliquid API keys are configured correctly.`
    },
    {
      id: "macro-calendar",
      title: "Macro Calendar",
      icon: "event_note",
      content: `## Macro Calendar

The **Macro Calendar** shows upcoming high-impact economic events and the active trade-block windows around them.

### Access
- The Macro Calendar is available to **Pro** users only

### What it does
- Shows upcoming events such as CPI, FOMC, and payroll releases
- Surfaces active block windows in the UI
- Supports manual refresh with **Sync Now**

### Trading behavior
- High-impact events can block new entries for a configured time window
- Existing risk controls such as stop loss, exit, and reduce-only actions remain allowed`
    }
  ];

  public toggle(): void {
    this._open$.next(!this._open$.value);
  }

  public open(): void {
    this._open$.next(true);
  }

  public close(): void {
    this._open$.next(false);
    this._activeTopic$.next(null);
  }

  public selectTopic(topic: HelpTopic): void {
    this._activeTopic$.next(topic);
  }

  public clearTopic(): void {
    this._activeTopic$.next(null);
  }

  public sendChatMessage(question: string): void {
    const userMsg: HelpChatMessage = {
      role: "user",
      content: question,
      timestamp: new Date()
    };
    this._chatMessages$.next([...this._chatMessages$.value, userMsg]);
    this._chatLoading$.next(true);

    this._api.post<HelpChatResponse>("help/chat", { question }).subscribe({
      next: (response: HelpChatResponse) => {
        const assistantMsg: HelpChatMessage = {
          role: "assistant",
          content: response.answer,
          timestamp: new Date()
        };
        this._chatMessages$.next([...this._chatMessages$.value, assistantMsg]);
        this._chatLoading$.next(false);
      },
      error: () => {
        const errorMsg: HelpChatMessage = {
          role: "assistant",
          content: "Sorry, I wasn't able to get an answer right now. Please try again later.",
          timestamp: new Date()
        };
        this._chatMessages$.next([...this._chatMessages$.value, errorMsg]);
        this._chatLoading$.next(false);
      }
    });
  }
}
