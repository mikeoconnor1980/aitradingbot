using TradePilot.Application.Trading.Models;

namespace TradePilot.Application.Trading.Services;

/// <summary>
/// Derives <see cref="LlmContext"/> from indicator state during backtesting.
/// Acts as a "poor man's LLM" using trend direction (EMA alignment),
/// volatility (ATR percentile), and RSI to classify the market regime.
/// </summary>
public sealed class SyntheticRegimeProvider
{
    private readonly RollingAtrPercentile _atrPercentile = new(96); // 96 × 15m = 24h lookback

    public void Update(decimal atr)
    {
        _atrPercentile.Add(atr);
    }

    public LlmContext Evaluate(IndicatorSnapshot indicators, long timestampUtc)
    {
        var trend = ClassifyTrend(indicators);
        var volatility = ClassifyVolatility();
        var sentiment = DeriveSentiment(trend, indicators.Rsi);
        var regime = DeriveRegime(trend, volatility);

        return new LlmContext
        {
            MarketSentiment = sentiment,
            MacroRegime = trend,
            EventRisk = volatility == "High" ? "High" : "Low",
            Confidence = _atrPercentile.IsMature ? 0.75m : 0.5m,
            DerivedRegime = regime,
            Summary = $"Synthetic: trend={trend}, volatility={volatility}, RSI={indicators.Rsi:F1}",
            GeneratedAtUtc = timestampUtc
        };
    }

    private static string ClassifyTrend(IndicatorSnapshot indicators)
    {
        if (indicators.EmaFast <= 0 || indicators.EmaSlow <= 0 || indicators.EmaTrend <= 0)
        {
            return "Neutral";
        }

        // Bullish: fast > slow > trend (EMA stack)
        if (indicators.EmaFast > indicators.EmaSlow && indicators.EmaSlow > indicators.EmaTrend)
        {
            return "Bullish";
        }

        // Bearish: fast < slow < trend (inverted EMA stack)
        if (indicators.EmaFast < indicators.EmaSlow && indicators.EmaSlow < indicators.EmaTrend)
        {
            return "Bearish";
        }

        return "Neutral";
    }

    private string ClassifyVolatility()
    {
        if (!_atrPercentile.IsMature)
        {
            return "Normal";
        }

        var percentile = _atrPercentile.CurrentPercentile;

        return percentile switch
        {
            >= 0.80m => "High",
            <= 0.20m => "Low",
            _ => "Normal"
        };
    }

    private static string DeriveSentiment(string trend, decimal rsi)
    {
        return trend switch
        {
            "Bullish" when rsi > 50 => "Bullish",
            "Bullish" => "Neutral",         // Trend up but RSI weak — cautious
            "Bearish" when rsi < 50 => "Bearish",
            "Bearish" => "Neutral",         // Trend down but RSI recovering
            _ => "Neutral"
        };
    }

    private static MarketRegime DeriveRegime(string trend, string volatility)
    {
        return (trend, volatility) switch
        {
            ("Bullish", "Low" or "Normal") => MarketRegime.Aggressive,
            ("Bullish", "High") => MarketRegime.Normal,
            ("Neutral", "High") => MarketRegime.Defensive,
            ("Bearish", "Low" or "Normal") => MarketRegime.Defensive,
            ("Bearish", "High") => MarketRegime.RiskOff,
            _ => MarketRegime.Normal
        };
    }
}

/// <summary>
/// Maintains a rolling window of ATR values and computes the percentile rank of the latest value.
/// </summary>
internal sealed class RollingAtrPercentile
{
    private readonly Queue<decimal> _values;
    private readonly int _windowSize;

    public RollingAtrPercentile(int windowSize)
    {
        _windowSize = windowSize;
        _values = new Queue<decimal>(windowSize + 1);
    }

    public bool IsMature => _values.Count >= _windowSize;

    public decimal CurrentPercentile
    {
        get
        {
            if (_values.Count == 0)
            {
                return 0.5m;
            }

            var latest = _values.Peek(); // oldest — we want newest
            var arr = _values.ToArray();
            latest = arr[^1];

            var countBelow = 0;
            foreach (var v in arr)
            {
                if (v < latest)
                {
                    countBelow++;
                }
            }

            return (decimal)countBelow / arr.Length;
        }
    }

    public void Add(decimal atr)
    {
        _values.Enqueue(atr);
        if (_values.Count > _windowSize)
        {
            _values.Dequeue();
        }
    }
}
