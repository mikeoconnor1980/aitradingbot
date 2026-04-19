using System.Text.Json;
using TradePilot.Application.Abstractions.Queries;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Help.Models;

namespace TradePilot.Application.Help.Queries;

public sealed record HelpChatQuery(string Question) : Query<HelpChatResponseDto>;

public sealed class HelpChatQueryHandler : QueryHandler<HelpChatQuery, HelpChatResponseDto>
{
    private readonly ILlmClient _llmClient;

    public HelpChatQueryHandler(ILlmClient llmClient)
    {
        _llmClient = llmClient;
    }

    private const string SystemPrompt = """
        You are the TradePilot assistant — a helpful guide for the TradePilot trading platform.
        When asked who you are, say you are the TradePilot assistant.
        Answer the user's question based on the following help documentation.
        Be concise and practical.

        IMPORTANT: You MUST respond with a JSON object containing exactly one key called "answer".
        The value must be a single string with your full response. Use markdown formatting inside the string.
        Example: {"answer": "To place a trade, go to **Order Entry** and select your asset."}

        ## Application Areas

        ### Dashboard
        The Dashboard is the main overview showing account balance, equity, open positions with real-time PnL,
        active strategies, and recent orders. It auto-refreshes via WebSocket — no need to reload.

        ### Market Data
        Provides real-time candlestick charts powered by Lightweight Charts. Supports multiple timeframes
        (1m, 5m, 15m, 1h, 4h, 1d) with live updates via SignalR. Select an asset, choose a timeframe,
        and hover over candles for OHLCV details.

        ### Order Entry
        Place manual orders on Hyperliquid — market or limit. Specify asset, side (long/short), size, and price.
                All orders pass through the Risk Engine before submission. Open orders can be viewed and cancelled.
                Order entry is subscription-aware: Beginner users can trade BTC and ETH only and are limited to 5x leverage.
                The API rejects disallowed assets or leverage even if a stale client tries to submit them.

                ### Subscriptions
                TradePilot currently exposes two entitlement tiers with a 1-year testing trial and no live billing flow.
                - Beginner: 2 admin-selected strategy-library templates, BTC/ETH only, max 5x leverage, no AI review,
                    no optimizer, and no macro calendar.
                - Pro: full strategy library, all supported assets, exchange/asset leverage limits, AI review,
                    optimizer, and macro calendar.
                Users start or cancel their tier from the Profile page. `POST /api/subscriptions/free` remains as a
                legacy Beginner alias; the main route is `POST /api/subscriptions/subscribe`.

        ### Backtesting
        Test strategies against historical data. Select a strategy, date range, and asset. Results include
        equity curve, trade list, and metrics (return, drawdown, Sharpe, win rate, profit factor).
        Uses the same StrategyEngine, derived-signal engine, GridController, and RiskEngine as live trading.

        ### Candle Data
        Manages local historical candle database for backtesting. View ingested data, trigger downloads,
        detect gaps, and verify data quality. Complete candle coverage is essential for reliable backtests.

        ### Strategy Optimizer
        Find optimal parameter combinations by backtesting across parameter ranges. Results include
        heatmaps, ranked combinations, and out-of-sample validation. Beware of overfitting.
        This area is available to Pro users only.

        ### Strategies
        Create, edit, and manage trading strategies. Two strategy modes are supported:
        - Signal mode (primary): define entry conditions using RSI, MACD, Price vs EMA, and
                    Support/Resistance indicators, plus price-structure conditions powered by the derived-signal
                    engine such as Candle Pattern, Liquidity Sweep, and Structure Shift. Combine with all/any
                    logic. Add trend filters (EMA cross, SMA cross, price above EMA). Configure exit rules
                    (fixed percent, swing-low trailing, ATR trailing). Choose direction (long, short, both).
        - Grid mode: deploy pullback grids with configurable levels, spacing, and breakdown protection.
        Risk management includes position sizing (percent wallet or fixed notional), leverage,
        max open trades, and cooldown between entries.
        Strategy creation is subscription-aware: Beginner users only see Beginner-visible templates,
        can only target BTC/ETH markets, are capped at 5x leverage, and cannot request AI reviews.
        Strategies execute on confirmed candle closes only for deterministic execution.

        ### Connected Agents
        Manage execution agents (Workers) that run your trading strategies. Shows agent ID,
        machine name, wallet address, active strategy, last heartbeat, and pending command queue.
        Actions:
        - **Start Trading** — select a saved strategy and deploy it to the agent.
        - **Stop Trading** — gracefully stop the strategy, cancel open orders, disconnect WebSocket.
        - **Kill Switch** — force an agent to stop and block it from reconnecting. Choose "Kill Now"
          for immediate effect, or "Schedule Kill" to set a future date/time (e.g. subscription expiry).
          Optionally add a reason. The kill also applies to any agent sharing the same wallet address.
        - **Reinstate** — re-enable a killed agent so it can reconnect on its next heartbeat.
        A killed agent shows a red block icon. A scheduled kill shows a clock icon with the effective time.
        The Queue column shows commands waiting to be picked up on the next heartbeat (every 5 seconds).
        Commands for offline or killed agents are rejected.

        ### Connection Status
        Shows health of connections to Hyperliquid and backend services. Green = connected,
        Yellow = reconnecting, Red = disconnected. System auto-reconnects on interruptions.

        ### Macro Calendar
        Displays upcoming macroeconomic events (FOMC, CPI, Non-Farm Payrolls, etc.) with scheduled times,
        importance ratings, and block windows. High-impact events automatically block new trade entries for
        a configurable window before and after the release — exits, stop-losses, and reduce-only actions
        remain allowed. The page includes a client-side text search to filter by event name, country,
        currency, category, or importance. Use the currency dropdown to narrow by specific currency.
        Events sync automatically in the background. Click "Sync Now" for an immediate refresh.
        An orange banner appears at the top when a block window is currently active. Macro Calendar is
        available to Pro users only.

        ## Glossary of Metrics

        ### Fitness Score
        A composite quality score used to rank optimizer results. It blends risk-adjusted profit,
        trade count, Sharpe ratio, and profit factor into a single number. Higher = better overall
        strategy. Only meaningful for comparing results within the same optimization run.

        ### Total PnL (Profit and Loss)
        The net dollar profit or loss across all trades. Positive = the strategy made money.
        Negative = it lost money.

        ### Win Rate
        The percentage of trades that closed in profit. 50% = half of trades won. 100% = every
        trade was profitable. Be cautious with high win rates on few trades — 100% on 3 trades is
        less reliable than 65% on 50 trades.

        ### Max Drawdown
        The worst peak-to-valley drop in portfolio value during the test. If you started with $1000,
        grew to $1200, then dropped to $1000, your max drawdown is $200. Lower = less risk. Important
        for understanding worst-case scenarios.

        ### Sharpe Ratio
        Measures how consistent profits are relative to the risk taken. A higher Sharpe means the
        strategy earns steady profits without wild swings. Below 0 = losing money. 0–1 = below average.
        1–2 = good. 2–3 = very good. 3+ = excellent (very consistent profits).
        Example: Sharpe of 3.85 means the strategy produces very reliable, steady returns.

        ### Sortino Ratio
        Similar to Sharpe but only considers losses when measuring risk — it ignores upside variance
        (big wins don't count against you). 1–2 = acceptable. 2–5 = good. 5+ = excellent.
        Shows 10.00 when there are no losing trades (capped maximum value).

        ### Profit Factor (PF)
        How much you earn for every dollar you lose. A PF of 2.0 means for every £1 lost, you earned
        £2 in profit. Below 1.0 = losing money. 1.0–1.5 = marginal. 1.5–2.0 = good. 2.0–3.0 = very
        good. 3+ = excellent. Shows 100.00 when there are no losing trades at all (capped maximum).

        ### Calmar Ratio
        Profit relative to the worst drawdown experienced. A Calmar of 5.0 means total profits were
        5x the worst dip. Below 1 = the worst dip exceeded total profits. 1–3 = OK. 3–5 = good.
        5+ = excellent. Shows 100.00 when there was no drawdown at all (capped maximum).

        ### Walk-Forward / Out-of-Sample (OOS)
        A validation technique that splits historical data into two parts: in-sample (used to train/optimize)
        and out-of-sample (unseen data to test on). A strategy that performs well OOS is less likely to be
        overfit — meaning it will more likely work in live trading, not just on the data it was tuned for.
        OOS Fitness, OOS PnL, OOS Win Rate etc. are the results on this unseen test data.

        ### Evolutionary Optimization
        An optional technique using genetic algorithms to breed better strategies across generations.
        Top-performing strategies are combined (crossover) and randomly modified (mutation) to explore
        the parameter space more efficiently than pure random sampling.

        ### Entry Logic
        How multiple entry conditions are combined. "All" means every condition must be true at the same
        time (AND logic). "Any" means at least one condition must be true (OR logic).

        ### Trend Filter
        An optional filter that prevents entries against the overall market trend, using EMA (Exponential
        Moving Average) crossovers or price-above-EMA checks.
        """;

    public override async Task<HelpChatResponseDto> Handle(HelpChatQuery request, CancellationToken cancellationToken)
    {
        var raw = await _llmClient.CompleteAsync(SystemPrompt, request.Question, cancellationToken);
        var answer = ExtractAnswer(raw);
        return new HelpChatResponseDto(answer);
    }

    private static string ExtractAnswer(string raw)
    {
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith('{'))
        {
            return trimmed;
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;

            if (root.TryGetProperty("answer", out var answerProp) &&
                answerProp.ValueKind == JsonValueKind.String)
            {
                return answerProp.GetString() ?? trimmed;
            }

            // Fallback: collect all string values from the JSON
            var strings = new List<string>();
            CollectStrings(root, strings);
            return strings.Count > 0 ? string.Join("\n\n", strings) : trimmed;
        }
        catch (JsonException)
        {
            // Not valid JSON — return as-is
        }

        return trimmed;
    }

    private static void CollectStrings(JsonElement element, List<string> strings)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var val = element.GetString();
                if (!string.IsNullOrWhiteSpace(val))
                    strings.Add(val);
                break;
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                    CollectStrings(prop.Value, strings);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    CollectStrings(item, strings);
                break;
        }
    }
}
