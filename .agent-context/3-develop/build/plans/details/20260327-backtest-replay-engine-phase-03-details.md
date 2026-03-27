<!-- markdownlint-disable-file -->

# Task Details: F3 — Backtest Replay Engine

## Phase 3: CandleReplayEngine and BacktestMetricsCalculator

## Standards and Knowledge References

- `.github/instructions/csharp.instructions.md` — `sealed` classes, `async/await`, `CancellationToken`, `ArgumentException.ThrowIfNullOrWhiteSpace`
- `.github/instructions/testing.instructions.md` — MSTest, Moq, FluentAssertions v6, `Given_When_Then` naming
- `.agent-context/0-knowledge/18-backtesting-architecture.md` — Replay model: load candle history, step forward candle by candle, build MarketContext
- `.agent-context/0-knowledge/19-scheduling-architecture.md` — Backtesting flow: HistoricalDataProvider → ReplayEngine → CandleClock
- `.agent-context/3-develop/backlog/draft/backtesting/F3-backtest-replay-engine.md` — Multi-timeframe alignment rules, warmup period, metrics definitions

## Design References

Multi-timeframe alignment rule (from PBI):
```
15m candle at T=12:00 →
  Latest closed 1h candle: 11:00 (covers 11:00-11:59)
  Latest closed 4h candle: 08:00 (covers 08:00-11:59)

15m candle at T=12:15 →
  Latest closed 1h candle: 12:00 (covers 12:00-12:59) — only if 12:00 candle has closed
  Latest closed 4h candle: 08:00 (still the latest closed)
```

Metrics definitions (from PBI):
- Win rate = `winning trades / total trades * 100`
- Max drawdown = largest peak-to-trough equity decline (absolute and % of peak)
- Average hold time = sum of (exit time - entry time) / number of completed trades

---

### Task 3.1: Create CandleReplayEngine with multi-timeframe alignment {#task-31-create-candlereplayengine-with-multi-timeframe-alignment}

Create the `CandleReplayEngine` that reads candles from `ICandleRepository` and provides sequential iteration with multi-timeframe context. The engine loads all three timeframes (15m, 1h, 4h) and at each 15m tick provides the latest closed higher-timeframe candles.

- **Complexity**: Medium
- **Risk Factors**: Multi-timeframe alignment logic must be correct — the latest closed 1h/4h candle at each 15m tick; must handle edge cases at timeframe boundaries
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/CandleReplayEngine.cs` — new file
  - `src/TradingApp.Application/Backtesting/Models/ReplayData.cs` — new file (separate from CandleReplayEngine per one-class-per-file standard)
- **Success**:
  - Loads candles from `ICandleRepository` for all requested timeframes
  - Iterates 15m candles in ascending time order
  - At each 15m tick, correctly identifies the latest closed 1h and 4h candles
  - No lookahead bias — only sees data up to current tick
  - Validates all timeframes have data for the requested range
- **Dependencies**: Phase 1 (interfaces), F1's `ICandleRepository` and `Candle` entity

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Services/CandleReplayEngine.cs — new file
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Backtesting.Services;

/// <summary>
/// Reads historical candles from the database and provides sequential replay
/// with multi-timeframe alignment. Drives the backtest by iterating 15m candles
/// and providing the latest closed higher-timeframe context at each tick.
/// </summary>
public sealed class CandleReplayEngine
{
    private readonly ICandleRepository _candleRepository;

    public CandleReplayEngine(ICandleRepository candleRepository)
    {
        _candleRepository = candleRepository ?? throw new ArgumentNullException(nameof(candleRepository));
    }

    /// <summary>
    /// Load all candles for the backtest. Includes warmup data before startDate.
    /// </summary>
    public async Task<ReplayData> LoadAsync(BacktestConfig config, CancellationToken cancellationToken = default)
    {
        // Calculate how far back we need for warmup (warmupPeriod × 15m interval in ms)
        var warmupDurationMs = config.WarmupPeriod * 15L * 60L * 1000L;
        var warmupStartMs = config.StartDateUtc - warmupDurationMs;

        // Load all timeframes
        var candles15m = await _candleRepository.GetCandlesAsync(
            config.Symbol, "15m", warmupStartMs, config.EndDateUtc, cancellationToken);

        var candles1h = await _candleRepository.GetCandlesAsync(
            config.Symbol, "1h", warmupStartMs, config.EndDateUtc, cancellationToken);

        var candles4h = await _candleRepository.GetCandlesAsync(
            config.Symbol, "4h", warmupStartMs, config.EndDateUtc, cancellationToken);

        // Validate data availability
        ValidateDataAvailability(config, candles15m, candles1h, candles4h);

        return new ReplayData
        {
            Candles15m = candles15m.OrderBy(c => c.Timestamp).ToList(),
            Candles1h = candles1h.OrderBy(c => c.Timestamp).ToList(),
            Candles4h = candles4h.OrderBy(c => c.Timestamp).ToList(),
            WarmupEndIndex = DetermineWarmupEndIndex(candles15m, config),
        };
    }

    /// <summary>
    /// Get the latest closed higher-timeframe candle at or before the given 15m trigger time.
    /// A candle is "closed" when its close time ≤ the trigger candle's open time.
    /// </summary>
    public static Candle? GetLatestClosedCandle(
        IReadOnlyList<Candle> higherTimeframeCandles,
        long triggerCandleOpenTimeUtc)
    {
        // Binary search or linear scan for the latest candle whose close time ≤ trigger open time
        Candle? latest = null;

        foreach (var candle in higherTimeframeCandles)
        {
            var closeTime = candle.Timestamp + GetIntervalMs(candle.Interval);

            if (closeTime <= triggerCandleOpenTimeUtc)
            {
                latest = candle;
            }
            else
            {
                break; // candles are sorted — no need to continue
            }
        }

        return latest;
    }

    private static void ValidateDataAvailability(
        BacktestConfig config,
        IReadOnlyList<Candle> candles15m,
        IReadOnlyList<Candle> candles1h,
        IReadOnlyList<Candle> candles4h)
    {
        if (candles15m.Count == 0)
            throw new InvalidOperationException(
                $"No candle data found for {config.Symbol}/15m between {config.StartDateUtc} and {config.EndDateUtc}");

        if (candles1h.Count == 0)
            throw new InvalidOperationException(
                $"Missing 1h candle data for {config.Symbol}. Cannot run backtest without higher-timeframe context.");

        if (candles4h.Count == 0)
            throw new InvalidOperationException(
                $"Missing 4h candle data for {config.Symbol}. Cannot run backtest without higher-timeframe context.");

        // Check warmup data sufficiency
        var warmupDurationMs = config.WarmupPeriod * 15L * 60L * 1000L;
        var warmupStartMs = config.StartDateUtc - warmupDurationMs;
        var candlesBefore = candles15m.Count(c => c.Timestamp < config.StartDateUtc);

        if (candlesBefore < config.WarmupPeriod)
            throw new InvalidOperationException(
                $"Insufficient warmup data for {config.Symbol}/15m. Need {config.WarmupPeriod} candles before start date, found {candlesBefore}.");
    }

    private static int DetermineWarmupEndIndex(IReadOnlyList<Candle> candles15m, BacktestConfig config)
    {
        var sorted = candles15m.OrderBy(c => c.Timestamp).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            if (sorted[i].Timestamp >= config.StartDateUtc)
                return i;
        }
        return sorted.Count;
    }

    private static long GetIntervalMs(string interval) => interval switch
    {
        "5m" => 5L * 60L * 1000L,
        "15m" => 15L * 60L * 1000L,
        "1h" => 60L * 60L * 1000L,
        "4h" => 4L * 60L * 60L * 1000L,
        _ => throw new ArgumentException($"Unsupported interval: {interval}")
    };
}

/// <summary>
/// Loaded and validated candle data ready for replay.
/// </summary>
// NOTE: This class should be in its own file: src/TradingApp.Application/Backtesting/Models/ReplayData.cs
public sealed class ReplayData
{
    public required IReadOnlyList<Candle> Candles15m { get; init; }
    public required IReadOnlyList<Candle> Candles1h { get; init; }
    public required IReadOnlyList<Candle> Candles4h { get; init; }

    /// <summary>
    /// Index into Candles15m where the evaluation period starts (after warmup).
    /// Candles before this index are warmup-only (feed indicators, no signals).
    /// </summary>
    public required int WarmupEndIndex { get; init; }
}
```

##### Pattern References

- `.agent-context/3-develop/backlog/draft/backtesting/F3-backtest-replay-engine.md` — Multi-timeframe alignment rules, warmup period, data validation requirements
- `src/TradingApp.Infrastructure/Hyperliquid/HyperliquidAssetMapper.cs` — `TimeframeToIntervalMs` mapping convention
- `.agent-context/3-develop/backlog/draft/backtesting/F1-candle-data-persistence.md` — `ICandleRepository.GetCandlesAsync(symbol, interval, startTime, endTime)` contract

---

### Task 3.2: Implement warmup period handling {#task-32-implement-warmup-period-handling}

The warmup logic is embedded in the `CandleReplayEngine.LoadAsync` method (Task 3.1) which calculates the warmup start time, loads extra candles, validates sufficiency, and determines the warmup end index. The `BacktestRunner` (Phase 4) will use `WarmupEndIndex` to separate warmup from evaluation.

- **Complexity**: Low
- **Risk Factors**: Edge case where exactly `warmupPeriod` candles exist before start date
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/CandleReplayEngine.cs` — already covered in Task 3.1
- **Success**:
  - Warmup data loaded automatically before user-specified start date
  - Insufficient warmup data causes fail-fast error
  - WarmupEndIndex correctly identifies first evaluation candle
- **Dependencies**: Task 3.1

---

### Task 3.3: Create BacktestMetricsCalculator {#task-33-create-backtestmetricscalculator}

Create a stateless calculator that computes summary metrics from the trade log and equity time-series.

- **Complexity**: Medium
- **Risk Factors**: Max drawdown calculation must correctly track peak-to-trough; win rate denominator must handle zero trades
- **Files**:
  - `src/TradingApp.Application/Backtesting/Services/BacktestMetricsCalculator.cs` — new file
- **Success**:
  - All PBI-required metrics computed: total trades, winning/losing, win rate, total PnL, max drawdown (abs + %), avg trade PnL, avg hold time, hedges opened, fees, grid cycles, final equity
  - Zero trades produces zeroed metrics (no division by zero)
  - Max drawdown calculated as largest peak-to-trough equity decline
- **Dependencies**: Phase 1 (BacktestResult, BacktestTrade, EquitySnapshot)

#### Implementation Details

```csharp
// src/TradingApp.Application/Backtesting/Services/BacktestMetricsCalculator.cs — new file
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Backtesting.Services;

/// <summary>
/// Stateless calculator that computes summary backtest metrics from trade log and equity series.
/// </summary>
public sealed class BacktestMetricsCalculator
{
    public BacktestResult Calculate(
        IReadOnlyList<BacktestTrade> tradeLog,
        IReadOnlyList<EquitySnapshot> equityTimeSeries,
        decimal initialCapital,
        int gridCycles)
    {
        var completedTrades = tradeLog.Where(t => t.ExitTimeUtc.HasValue && t.PnL.HasValue).ToList();
        var totalTrades = completedTrades.Count;
        var winningTrades = completedTrades.Count(t => t.PnL > 0);
        var losingTrades = completedTrades.Count(t => t.PnL <= 0);
        var winRate = totalTrades > 0 ? (decimal)winningTrades / totalTrades * 100m : 0m;

        var totalPnL = completedTrades.Sum(t => t.PnL ?? 0m);
        var averageTradePnL = totalTrades > 0 ? totalPnL / totalTrades : 0m;
        var totalFeesPaid = tradeLog.Sum(t => t.Fees);
        var hedgesOpened = tradeLog.Count(t => t.TradeType == TradeType.HedgeOpen);

        var averageHoldTime = CalculateAverageHoldTime(completedTrades);
        var (maxDrawdownAbsolute, maxDrawdownPercent) = CalculateMaxDrawdown(equityTimeSeries);
        var finalEquity = equityTimeSeries.Count > 0
            ? equityTimeSeries[^1].Equity
            : initialCapital;

        return new BacktestResult
        {
            TotalTrades = totalTrades,
            WinningTrades = winningTrades,
            LosingTrades = losingTrades,
            WinRate = Math.Round(winRate, 2),
            TotalPnL = totalPnL,
            MaxDrawdownAbsolute = maxDrawdownAbsolute,
            MaxDrawdownPercent = Math.Round(maxDrawdownPercent, 2),
            AverageTradePnL = Math.Round(averageTradePnL, 4),
            AverageHoldTime = averageHoldTime,
            HedgesOpened = hedgesOpened,
            TotalFeesPaid = totalFeesPaid,
            GridCycles = gridCycles,
            FinalEquity = finalEquity,
            EquityTimeSeries = equityTimeSeries,
            TradeLog = tradeLog.ToList()
        };
    }

    private static TimeSpan CalculateAverageHoldTime(IReadOnlyList<BacktestTrade> completedTrades)
    {
        if (completedTrades.Count == 0)
            return TimeSpan.Zero;

        var totalMs = completedTrades.Sum(t => (t.ExitTimeUtc ?? 0) - t.EntryTimeUtc);
        var avgMs = totalMs / completedTrades.Count;
        return TimeSpan.FromMilliseconds(avgMs);
    }

    private static (decimal absolute, decimal percent) CalculateMaxDrawdown(
        IReadOnlyList<EquitySnapshot> equityTimeSeries)
    {
        if (equityTimeSeries.Count == 0)
            return (0m, 0m);

        var peak = equityTimeSeries[0].Equity;
        var maxDrawdownAbsolute = 0m;
        var maxDrawdownPercent = 0m;

        foreach (var snapshot in equityTimeSeries)
        {
            if (snapshot.Equity > peak)
                peak = snapshot.Equity;

            var drawdown = peak - snapshot.Equity;
            if (drawdown > maxDrawdownAbsolute)
            {
                maxDrawdownAbsolute = drawdown;
                maxDrawdownPercent = peak > 0 ? drawdown / peak * 100m : 0m;
            }
        }

        return (maxDrawdownAbsolute, maxDrawdownPercent);
    }
}
```

##### Pattern References

- `.agent-context/3-develop/backlog/draft/backtesting/F3-backtest-replay-engine.md` — Metrics definitions: total trades, win rate, max drawdown, average hold time, etc.
- `src/TradingApp.Application/Backtesting/Models/BacktestResult.cs` — Result model (Phase 1)

---

### Task 3.4: Write CandleReplayEngine unit tests {#task-34-write-candlereplayengine-unit-tests}

Write unit tests for `CandleReplayEngine` covering data loading, multi-timeframe alignment, warmup validation, and error cases.

- **Complexity**: Medium
- **Risk Factors**: Multi-timeframe alignment edge cases at boundary times; must properly mock `ICandleRepository`
- **Files**:
  - `tests/TradingApp.Application.Tests/Backtesting/Services/CandleReplayEngineTests.cs` — new file
- **Success**:
  - Tests cover: successful data load, multi-TF alignment, warmup validation, missing data errors (15m, 1h, 4h), insufficient warmup error
  - All tests pass
- **Dependencies**: Tasks 3.1, 3.2

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Backtesting/Services/CandleReplayEngineTests.cs — new file
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.Backtesting.Services;

[TestClass]
public sealed class CandleReplayEngineTests
{
    private Mock<ICandleRepository> _candleRepoMock = default!;
    private CandleReplayEngine _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _candleRepoMock = new Mock<ICandleRepository>();
        _sut = new CandleReplayEngine(_candleRepoMock.Object);
    }

    [TestMethod]
    public async Task GivenAllTimeframesAvailable_WhenLoadAsync_ThenReturnsReplayData()
    {
        // Arrange
        var config = CreateConfig(startDate: 1000000, endDate: 2000000, warmup: 5);
        SetupCandles("15m", count: 20, startTime: 500000);
        SetupCandles("1h", count: 10, startTime: 500000);
        SetupCandles("4h", count: 5, startTime: 500000);

        // Act
        var data = await _sut.LoadAsync(config);

        // Assert
        data.Candles15m.Should().NotBeEmpty();
        data.Candles1h.Should().NotBeEmpty();
        data.Candles4h.Should().NotBeEmpty();
        data.Candles15m.Should().BeInAscendingOrder(c => c.Timestamp);
    }

    [TestMethod]
    public async Task GivenNo15mCandles_WhenLoadAsync_ThenThrowsWithDescriptiveError()
    {
        // Arrange
        var config = CreateConfig(startDate: 1000000, endDate: 2000000, warmup: 5);
        _candleRepoMock
            .Setup(r => r.GetCandlesAsync("BTC", "15m", It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Candle>());
        SetupCandles("1h", count: 10, startTime: 500000);
        SetupCandles("4h", count: 5, startTime: 500000);

        // Act & Assert
        var act = () => _sut.LoadAsync(config);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No candle data found*15m*");
    }

    [TestMethod]
    public async Task GivenMissing1hCandles_WhenLoadAsync_ThenThrowsWithTimeframeError()
    {
        // Arrange
        var config = CreateConfig(startDate: 1000000, endDate: 2000000, warmup: 5);
        SetupCandles("15m", count: 20, startTime: 500000);
        _candleRepoMock
            .Setup(r => r.GetCandlesAsync("BTC", "1h", It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Candle>());
        SetupCandles("4h", count: 5, startTime: 500000);

        // Act & Assert
        var act = () => _sut.LoadAsync(config);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Missing 1h*");
    }

    [TestMethod]
    public async Task GivenInsufficientWarmupData_WhenLoadAsync_ThenThrowsWithWarmupError()
    {
        // Arrange: need 200 warmup candles but only 50 exist before start
        var config = CreateConfig(startDate: 1000000, endDate: 2000000, warmup: 200);
        SetupCandles("15m", count: 50, startTime: 900000); // all after warmup start
        SetupCandles("1h", count: 10, startTime: 500000);
        SetupCandles("4h", count: 5, startTime: 500000);

        // Act & Assert
        var act = () => _sut.LoadAsync(config);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient warmup*");
    }

    [TestMethod]
    public void GivenHigherTimeframeCandles_WhenGetLatestClosed_ThenReturnsCorrectCandle()
    {
        // Arrange: 1h candles at 08:00, 09:00, 10:00, 11:00
        // 15m trigger at 11:15 → latest closed 1h = 11:00 (close time 12:00 > 11:15? no...)
        // Actually: 1h candle at 11:00 closes at 12:00. Trigger at 11:15 open time.
        // 11:00 close time = 12:00 > 11:15 → NOT closed yet.
        // Latest closed = 10:00 (close time 11:00 ≤ 11:15)
        var oneHourCandles = new List<Candle>
        {
            CreateCandle("1h", timestamp: HoursToMs(8)),
            CreateCandle("1h", timestamp: HoursToMs(9)),
            CreateCandle("1h", timestamp: HoursToMs(10)),
            CreateCandle("1h", timestamp: HoursToMs(11)),
        };

        // 15m candle opens at 11:15
        var triggerOpenTime = HoursToMs(11) + 15L * 60L * 1000L;

        // Act
        var latest = CandleReplayEngine.GetLatestClosedCandle(oneHourCandles, triggerOpenTime);

        // Assert
        latest.Should().NotBeNull();
        latest!.Timestamp.Should().Be(HoursToMs(10)); // 10:00 candle, closes at 11:00
    }

    // --- Helpers ---

    private static BacktestConfig CreateConfig(long startDate, long endDate, int warmup)
    {
        return new BacktestConfig
        {
            Symbol = "BTC",
            Intervals = new[] { "15m", "1h", "4h" },
            StartDateUtc = startDate,
            EndDateUtc = endDate,
            InitialCapital = 10000m,
            FeeModel = FeeModel.Default,
            WarmupPeriod = warmup,
            StrategyConfigJson = "{}"
        };
    }

    private void SetupCandles(string interval, int count, long startTime)
    {
        var intervalMs = interval switch
        {
            "15m" => 15L * 60 * 1000,
            "1h" => 60L * 60 * 1000,
            "4h" => 4L * 60 * 60 * 1000,
            _ => throw new ArgumentException($"Unknown interval: {interval}")
        };

        var candles = Enumerable.Range(0, count)
            .Select(i => CreateCandle(interval, startTime + i * intervalMs))
            .ToList();

        _candleRepoMock
            .Setup(r => r.GetCandlesAsync("BTC", interval, It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(candles);
    }

    private static Candle CreateCandle(string interval, long timestamp)
    {
        return new Candle
        {
            Symbol = "BTC",
            Interval = interval,
            Timestamp = timestamp,
            Open = 100m, High = 105m, Low = 95m, Close = 102m, Volume = 1000m
        };
    }

    private static long HoursToMs(int hours) => hours * 60L * 60L * 1000L;
}
```

##### Pattern References

- `tests/TradingApp.Api.Tests/Services/HyperliquidOrderServiceTests.cs` — Service test with Moq mocks, `[TestInitialize]`
- `tests/TradingApp.Application.Tests/Usings.cs` — Global usings

---

### Task 3.5: Write BacktestMetricsCalculator unit tests {#task-35-write-backtestmetricscalculator-unit-tests}

Write unit tests for `BacktestMetricsCalculator` covering all metric calculations and edge cases.

- **Complexity**: Low
- **Risk Factors**: Division by zero when no trades; max drawdown with monotonically increasing equity
- **Files**:
  - `tests/TradingApp.Application.Tests/Backtesting/Services/BacktestMetricsCalculatorTests.cs` — new file
- **Success**:
  - Tests cover: normal metrics, zero trades, max drawdown calculation, win rate edge cases
  - All tests pass
- **Dependencies**: Task 3.3

#### Implementation Details

```csharp
// tests/TradingApp.Application.Tests/Backtesting/Services/BacktestMetricsCalculatorTests.cs — new file
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Backtesting.Services;
using TradingApp.Application.Trading.Models;

namespace TradingApp.Application.Tests.Backtesting.Services;

[TestClass]
public sealed class BacktestMetricsCalculatorTests
{
    private BacktestMetricsCalculator _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new BacktestMetricsCalculator();
    }

    [TestMethod]
    public void GivenCompletedTrades_WhenCalculate_ThenReturnsCorrectMetrics()
    {
        // Arrange
        var trades = new List<BacktestTrade>
        {
            CreateTrade(entryTime: 1000, exitTime: 2000, pnl: 50m, fees: 1m, TradeType.GridFill),
            CreateTrade(entryTime: 3000, exitTime: 4000, pnl: -20m, fees: 1m, TradeType.GridFill),
            CreateTrade(entryTime: 5000, exitTime: 6000, pnl: 30m, fees: 1m, TradeType.GridFill),
        };
        var equity = new List<EquitySnapshot>
        {
            new(1000, 10000m), new(2000, 10049m), new(3000, 10049m),
            new(4000, 10028m), new(5000, 10028m), new(6000, 10057m)
        };

        // Act
        var result = _sut.Calculate(trades, equity, 10000m, gridCycles: 1);

        // Assert
        result.TotalTrades.Should().Be(3);
        result.WinningTrades.Should().Be(2);
        result.LosingTrades.Should().Be(1);
        result.WinRate.Should().Be(66.67m);
        result.TotalPnL.Should().Be(60m);
        result.TotalFeesPaid.Should().Be(3m);
    }

    [TestMethod]
    public void GivenNoTrades_WhenCalculate_ThenReturnsZeroedMetrics()
    {
        // Arrange
        var trades = new List<BacktestTrade>();
        var equity = new List<EquitySnapshot> { new(1000, 10000m) };

        // Act
        var result = _sut.Calculate(trades, equity, 10000m, gridCycles: 0);

        // Assert
        result.TotalTrades.Should().Be(0);
        result.WinRate.Should().Be(0m);
        result.TotalPnL.Should().Be(0m);
        result.AverageHoldTime.Should().Be(TimeSpan.Zero);
    }

    [TestMethod]
    public void GivenEquityDrawdown_WhenCalculate_ThenMaxDrawdownCorrect()
    {
        // Arrange: peak 10100, trough 9800 → drawdown = 300 absolute, 2.97%
        var equity = new List<EquitySnapshot>
        {
            new(1000, 10000m), new(2000, 10100m), new(3000, 9900m),
            new(4000, 9800m), new(5000, 10000m), new(6000, 10200m)
        };

        // Act
        var result = _sut.Calculate(new List<BacktestTrade>(), equity, 10000m, gridCycles: 0);

        // Assert
        result.MaxDrawdownAbsolute.Should().Be(300m);
        result.MaxDrawdownPercent.Should().BeApproximately(2.97m, 0.01m);
    }

    private static BacktestTrade CreateTrade(long entryTime, long? exitTime, decimal? pnl, decimal fees, TradeType type)
    {
        return new BacktestTrade
        {
            TradeId = Guid.NewGuid().ToString(),
            GridCycleId = "cycle-1",
            EntryTimeUtc = entryTime,
            EntryPrice = 100m,
            ExitTimeUtc = exitTime,
            ExitPrice = pnl.HasValue ? 100m + pnl.Value : null,
            Side = OrderSide.Buy,
            Size = 1m,
            PnL = pnl,
            Fees = fees,
            TradeType = type
        };
    }
}
```

##### Pattern References

- `tests/TradingApp.Infrastructure.Tests/Services/NonceProviderTests.cs` — Minimal unit test pattern, direct instantiation, no mocks

---

### Task 3.6: Verify solution builds and all tests pass {#task-36-verify-solution-builds-and-all-tests-pass}

Build the full solution and run all tests.

- **Complexity**: Low
- **Risk Factors**: None
- **Files**: None (verification only)
- **Success**:
  - `dotnet build` succeeds
  - `dotnet test` passes all tests (Phase 1 + Phase 2 + Phase 3)
  - No regressions
- **Dependencies**: All Phase 3 tasks

---

## Phase Success Criteria

- CandleReplayEngine loads candle data for all three timeframes from ICandleRepository
- Multi-timeframe alignment correctly identifies latest closed 1h and 4h candles at each 15m tick
- Warmup period automatically loads extra candles and validates sufficiency
- Missing data fails fast with descriptive error messages (15m, 1h, 4h, warmup)
- BacktestMetricsCalculator correctly computes all PBI-required metrics
- Max drawdown calculated as largest peak-to-trough equity decline
- Zero trades handled gracefully (no division by zero)
- All unit tests pass
