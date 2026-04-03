using TradingApp.Application.Trading.Models;
using TradingApp.Indicators;

namespace TradingApp.Application.Trading.Services;

public static class ChartIndicatorSeriesCalculator
{
    private const int FastEmaPeriod = 20;
    private const int SlowEmaPeriod = 50;
    private const int TrendEmaPeriod = 200;

    public static IReadOnlyList<ChartIndicatorValues> Calculate(IReadOnlyList<(decimal High, decimal Low, decimal Close)> bars)
    {
        if (bars.Count == 0)
        {
            return [];
        }

        var closes = bars.Select(bar => bar.Close).ToList();
    var emaFastSeries = EmaCalculator.CalculateSeries(closes, FastEmaPeriod);
    var emaSlowSeries = EmaCalculator.CalculateSeries(closes, SlowEmaPeriod);
    var emaTrendSeries = EmaCalculator.CalculateSeries(closes, TrendEmaPeriod);
        var rsiSeries = RsiCalculator.CalculateSeries(closes, 14);
        var atrSeries = AtrCalculator.CalculateSeries(bars, 14);
        var macdSeries = MacdCalculator.CalculateSeries(closes, 12, 26, 9);
        var bollingerSeries = BollingerBandsCalculator.CalculateSeries(closes, 20, 2m);

        var result = new List<ChartIndicatorValues>(bars.Count);
        for (var index = 0; index < bars.Count; index++)
        {
            var macd = macdSeries[index];
            var bands = bollingerSeries[index];
            result.Add(new ChartIndicatorValues
            {
                EmaFast = emaFastSeries[index],
                EmaSlow = emaSlowSeries[index],
                EmaTrend = emaTrendSeries[index],
                Rsi = rsiSeries[index],
                Atr = atrSeries[index],
                MacdLine = macd?.Line,
                MacdSignal = macd?.Signal,
                MacdHistogram = macd?.Histogram,
                BollingerUpper = bands?.Upper,
                BollingerMiddle = bands?.Middle,
                BollingerLower = bands?.Lower,
            });
        }

        return result;
    }
}