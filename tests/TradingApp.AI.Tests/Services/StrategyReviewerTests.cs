using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingApp.AI.Services;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Enums;

namespace TradingApp.AI.Tests.Services;

[TestClass]
public sealed class StrategyReviewerTests
{
    private Mock<IReviewLlmClient> _llmClientMock = default!;
    private StrategyReviewer _sut = default!;

    [TestInitialize]
    public void Setup()
    {
        _llmClientMock = new Mock<IReviewLlmClient>();
        _sut = new StrategyReviewer(
            _llmClientMock.Object,
            Mock.Of<ILogger<StrategyReviewer>>());
    }

    [TestMethod]
    public async Task GivenValidStrategyJson_WhenReviewAsync_ThenReturnsReviewMarkdown()
    {
        const string strategyJson = "{\"grid\":{}}";
        const string expectedReview = "## 1. Strategy Summary\n- Grid strategy";

        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReview);

        var result = await _sut.ReviewAsync(strategyJson, null, CancellationToken.None);

        result.ReviewMarkdown.Should().Be(expectedReview);
        result.IsFallback.Should().BeFalse();
        _llmClientMock.Verify(
            client => client.CompleteAsync(
                It.Is<string>(prompt => prompt.Contains("You are an expert trading strategy reviewer.", StringComparison.Ordinal)),
                It.Is<string>(message =>
                    message.Contains("Return only the final end-user markdown review.", StringComparison.Ordinal) &&
                    message.Contains("```json", StringComparison.Ordinal) &&
                    message.Contains(strategyJson, StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenMarkdownCodeFence_WhenReviewAsync_ThenStripsFenceBeforeReturning()
    {
        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("```markdown\n## 1. Strategy Summary\n- Clean output\n```");

        var result = await _sut.ReviewAsync("{\"grid\":{}}", null, CancellationToken.None);

        result.ReviewMarkdown.Should().Be("## 1. Strategy Summary\n- Clean output");
        result.IsFallback.Should().BeFalse();
    }

    [TestMethod]
    public async Task GivenRawJsonResponse_WhenReviewAsync_ThenRecoversFromJsonStructure()
    {
        const string strategyJson = """
            {
              "strategyName": "ETH RSI Buy",
              "strategyMode": "signal",
              "market": "ETH",
              "timeframe": "15m",
              "direction": "long",
              "entryConditions": [ { "type": "rsi" } ],
              "exit": {
                "takeProfit": { "enabled": true, "value": 3 },
                "stopLoss": { "enabled": true, "value": 5 }
              },
              "risk": {
                "leverage": 1,
                "maxOpenTrades": 1
              }
            }
            """;

        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategyJson);

        var result = await _sut.ReviewAsync(strategyJson, null, CancellationToken.None);

        result.ReviewMarkdown.Should().NotStartWith("{");
        result.ReviewMarkdown.Should().Contain("##");
    }

    [TestMethod]
    public async Task GivenStructuredJsonReview_WhenReviewAsync_ThenConvertsToMarkdownWithoutFallback()
    {
        const string jsonReview = """
            {
              "review": {
                "strategySummary": {
                  "type": "Mean Reversion / Oversold",
                  "description": "This strategy buys ETH when RSI drops below 40."
                },
                "entryLogicQuality": {
                  "clarity": "The entry signal is clear: RSI < 40.",
                  "weaknesses": ["Single indicator — no confirmation signal."]
                },
                "riskManagement": {
                  "leverage": "1x — conservative",
                  "stopLoss": "5% fixed — reasonable"
                }
              }
            }
            """;

        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonReview);

        var result = await _sut.ReviewAsync("{\"grid\":{}}", null, CancellationToken.None);

        result.IsFallback.Should().BeFalse();
        result.ReviewMarkdown.Should().Contain("## 1. Strategy Summary");
        result.ReviewMarkdown.Should().Contain("Mean Reversion / Oversold");
        result.ReviewMarkdown.Should().Contain("## 2. Entry Logic Quality");
        result.ReviewMarkdown.Should().Contain("Single indicator");
        result.ReviewMarkdown.Should().Contain("## 3. Risk Management");
        result.ReviewMarkdown.Should().NotStartWith("{");
    }

    [TestMethod]
    public async Task GivenMarkdownWrappedInJsonString_WhenReviewAsync_ThenExtractsMarkdownWithoutFallback()
    {
        const string wrappedReview = """
            {
              "review": "## 1. Strategy Summary\n\n- **Type:** Mean reversion strategy.\n- **Description:** Buys ETH when RSI drops below 40.\n\n## 2. Entry Logic Quality\n\n- The entry signal is clear and straightforward."
            }
            """;

        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(wrappedReview);

        var result = await _sut.ReviewAsync("{\"grid\":{}}", null, CancellationToken.None);

        result.IsFallback.Should().BeFalse();
        result.ReviewMarkdown.Should().Contain("## 1. Strategy Summary");
        result.ReviewMarkdown.Should().Contain("Mean reversion strategy");
        result.ReviewMarkdown.Should().NotStartWith("{");
    }

    [TestMethod]
    public async Task GivenLlmFailure_WhenReviewAsync_ThenThrowsHttpRequestException()
    {
        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM unavailable"));

        var act = () => _sut.ReviewAsync("{\"grid\":{}}", null, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task GivenInvalidInput_WhenReviewAsync_ThenThrowsArgumentException(string? strategyJson)
    {
        var act = () => _sut.ReviewAsync(strategyJson!, null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [TestMethod]
    public async Task GivenBacktestSummary_WhenReviewAsync_ThenIncludesBacktestDataInPrompt()
    {
        const string strategyJson = "{\"grid\":{}}";
        const string expectedReview = "## 1. Strategy Summary\n- Grid strategy with backtest";

        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReview);

        var backtestSummary = CreateTestBacktestSummary(durationDays: 45);

        var result = await _sut.ReviewAsync(strategyJson, backtestSummary, CancellationToken.None);

        result.ReviewMarkdown.Should().Be(expectedReview);
        result.IsFallback.Should().BeFalse();
        _llmClientMock.Verify(
            client => client.CompleteAsync(
                It.IsAny<string>(),
                It.Is<string>(message =>
                    message.Contains("BACKTEST PERFORMANCE DATA", StringComparison.Ordinal) &&
                    message.Contains("Win Rate: 66.7%", StringComparison.Ordinal) &&
                    message.Contains("reliable", StringComparison.Ordinal) &&
                    message.Contains("Profit Factor:", StringComparison.Ordinal) &&
                    message.Contains("Sharpe Ratio", StringComparison.Ordinal) &&
                    message.Contains("Reward:Risk Ratio:", StringComparison.Ordinal) &&
                    message.Contains("Fee-to-Gross-Profit Ratio:", StringComparison.Ordinal) &&
                    message.Contains("Max Consecutive Losses:", StringComparison.Ordinal) &&
                    message.Contains("Section 11", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenInsufficientBacktest_WhenReviewAsync_ThenIncludesBacktestNoteInPrompt()
    {
        const string strategyJson = "{\"grid\":{}}";
        const string expectedReview = "## 1. Strategy Summary\n- Grid strategy note only";

        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReview);

        var backtestSummary = CreateTestBacktestSummary(durationDays: 7);

        var result = await _sut.ReviewAsync(strategyJson, backtestSummary, CancellationToken.None);

        result.ReviewMarkdown.Should().Be(expectedReview);
        _llmClientMock.Verify(
            client => client.CompleteAsync(
                It.IsAny<string>(),
                It.Is<string>(message =>
                    message.Contains("BACKTEST NOTE", StringComparison.Ordinal) &&
                    message.Contains("insufficient for statistical analysis", StringComparison.Ordinal) &&
                    !message.Contains("BACKTEST PERFORMANCE DATA", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenLimitedBacktest_WhenReviewAsync_ThenIncludesCautionNote()
    {
        const string strategyJson = "{\"grid\":{}}";
        const string expectedReview = "## 1. Strategy Summary\n- Grid strategy with caution";

        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReview);

        var backtestSummary = CreateTestBacktestSummary(durationDays: 20);

        var result = await _sut.ReviewAsync(strategyJson, backtestSummary, CancellationToken.None);

        _llmClientMock.Verify(
            client => client.CompleteAsync(
                It.IsAny<string>(),
                It.Is<string>(message =>
                    message.Contains("BACKTEST PERFORMANCE DATA", StringComparison.Ordinal) &&
                    message.Contains("CAUTION: limited sample", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenRegimeSegmentation_WhenReviewAsync_ThenIncludesSegmentedBacktestDataInPrompt()
    {
        const string strategyJson = "{\"grid\":{}}";
        const string expectedReview = "## 1. Strategy Summary\n- Grid strategy with regime analysis";

        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReview);

        var backtestSummary = CreateSegmentedBacktestSummary();

        var result = await _sut.ReviewAsync(strategyJson, backtestSummary, CancellationToken.None);

        result.ReviewMarkdown.Should().Be(expectedReview);
        _llmClientMock.Verify(
            client => client.CompleteAsync(
                It.IsAny<string>(),
                It.Is<string>(message =>
                    message.Contains("REGIME SEGMENTATION", StringComparison.Ordinal)
                    && message.Contains("Trend / Range", StringComparison.Ordinal)
                    && message.Contains("Funding Bucket", StringComparison.Ordinal)
                    && message.Contains("Historical open-interest snapshots", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenNullBacktest_WhenReviewAsync_ThenNoBacktestSectionInPrompt()
    {
        const string strategyJson = "{\"grid\":{}}";
        const string expectedReview = "## 1. Strategy Summary\n- Grid strategy";

        _llmClientMock
            .Setup(client => client.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedReview);

        var result = await _sut.ReviewAsync(strategyJson, null, CancellationToken.None);

        _llmClientMock.Verify(
            client => client.CompleteAsync(
                It.IsAny<string>(),
                It.Is<string>(message =>
                    !message.Contains("BACKTEST", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static BacktestSummaryForReview? CreateTestBacktestSummary(int durationDays)
    {
        var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = startDate.AddDays(durationDays);

        var equitySeries = Enumerable.Range(0, durationDays * 96) // 96 candles per day @ 15m
            .Select(i => new EquitySnapshot(
                startDate.AddMinutes(i * 15).ToUnixTimeMilliseconds(),
                10000m + i * 0.5m))
            .ToList();
        var equityJson = JsonSerializer.Serialize(equitySeries);

        var trades = new List<BacktestTrade>
        {
            new() { TradeId = "t1", GridCycleId = "g1", EntryTimeUtc = startDate.AddHours(1).ToUnixTimeMilliseconds(), EntryPrice = 3000m, ExitTimeUtc = startDate.AddHours(5).ToUnixTimeMilliseconds(), ExitPrice = 3100m, Side = OrderSide.Buy, Size = 1m, PnL = 100m, Fees = 2m, TradeType = TradeType.GridFill },
            new() { TradeId = "t2", GridCycleId = "g1", EntryTimeUtc = startDate.AddHours(10).ToUnixTimeMilliseconds(), EntryPrice = 3100m, ExitTimeUtc = startDate.AddHours(14).ToUnixTimeMilliseconds(), ExitPrice = 3050m, Side = OrderSide.Buy, Size = 1m, PnL = -50m, Fees = 2m, TradeType = TradeType.GridFill },
            new() { TradeId = "t3", GridCycleId = "g2", EntryTimeUtc = startDate.AddDays(2).ToUnixTimeMilliseconds(), EntryPrice = 3050m, ExitTimeUtc = startDate.AddDays(2).AddHours(6).ToUnixTimeMilliseconds(), ExitPrice = 3150m, Side = OrderSide.Buy, Size = 1m, PnL = 100m, Fees = 2m, TradeType = TradeType.GridFill },
            new() { TradeId = "t4", GridCycleId = "g2", EntryTimeUtc = startDate.AddDays(3).ToUnixTimeMilliseconds(), EntryPrice = 3150m, ExitTimeUtc = startDate.AddDays(3).AddHours(4).ToUnixTimeMilliseconds(), ExitPrice = 3200m, Side = OrderSide.Buy, Size = 1m, PnL = 50m, Fees = 2m, TradeType = TradeType.GridFill },
            new() { TradeId = "t5", GridCycleId = "g3", EntryTimeUtc = startDate.AddDays(5).ToUnixTimeMilliseconds(), EntryPrice = 3200m, ExitTimeUtc = startDate.AddDays(5).AddHours(8).ToUnixTimeMilliseconds(), ExitPrice = 3170m, Side = OrderSide.Buy, Size = 1m, PnL = -30m, Fees = 2m, TradeType = TradeType.GridFill },
            new() { TradeId = "t6", GridCycleId = "g3", EntryTimeUtc = startDate.AddDays(7).ToUnixTimeMilliseconds(), EntryPrice = 3170m, ExitTimeUtc = startDate.AddDays(7).AddHours(6).ToUnixTimeMilliseconds(), ExitPrice = 3250m, Side = OrderSide.Buy, Size = 1m, PnL = 80m, Fees = 2m, TradeType = TradeType.GridFill },
        };
        var tradesJson = JsonSerializer.Serialize(trades);

        var run = BacktestRun.Create(
            symbol: "ETH",
            intervalsJson: "[\"15m\"]",
            startDateUtc: startDate.ToUnixTimeMilliseconds(),
            endDateUtc: endDate.ToUnixTimeMilliseconds(),
            strategyConfigJson: "{\"grid\":{}}",
            executionConfigJson: "{\"makerFee\":0.0002}",
            initialCapital: 10000m,
            candlesReplayed: durationDays * 96,
            elapsedMs: 1500,
            totalTrades: 6,
            winningTrades: 4,
            losingTrades: 2,
            winRate: 66.7m,
            totalPnl: 234.56m,
            maxDrawdown: 150m,
            averageTradePnl: 4.99m,
            averageHoldTimeMinutes: 252.0,
            hedgesOpened: 0,
            totalFeesPaid: 12.34m,
            tradesJson: tradesJson,
            equityTimeSeriesJson: equityJson);

        return BacktestSummaryForReview.FromBacktestRun(run);
    }

    private static BacktestSummaryForReview? CreateSegmentedBacktestSummary()
    {
        var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = startDate.AddDays(45);

        var equitySeries = Enumerable.Range(0, 45 * 96)
            .Select(i => new EquitySnapshot(
                startDate.AddMinutes(i * 15).ToUnixTimeMilliseconds(),
                10000m + i))
            .ToList();

        var candleLog = new List<CandleEvaluationEntry>
        {
            new()
            {
                TimestampUtc = startDate.AddHours(1).ToUnixTimeMilliseconds(),
                Open = 105m,
                High = 107m,
                Low = 104m,
                Close = 106m,
                Volume = 1200m,
                IsWarmup = false,
                EmaFast = 104m,
                EmaSlow = 100m,
                EmaTrend = 98m,
                Rsi = 58m,
                Atr = 1m,
                SetupDetected = true,
                GridLifecycleState = "Deploying",
                PositionSize = 0m,
                PositionAvgEntry = 0m,
                SignalsEmitted = ["DeployGrid"],
                GridCycleId = "cycle-asia"
            },
            new()
            {
                TimestampUtc = startDate.AddHours(10).ToUnixTimeMilliseconds(),
                Open = 100m,
                High = 100.5m,
                Low = 99.5m,
                Close = 100m,
                Volume = 1100m,
                IsWarmup = false,
                EmaFast = 100.2m,
                EmaSlow = 100.1m,
                EmaTrend = 100m,
                Rsi = 49m,
                Atr = 2m,
                SetupDetected = true,
                GridLifecycleState = "Deploying",
                PositionSize = 0m,
                PositionAvgEntry = 0m,
                SignalsEmitted = ["DeployGrid"],
                GridCycleId = "cycle-europe"
            },
            new()
            {
                TimestampUtc = startDate.AddHours(18).ToUnixTimeMilliseconds(),
                Open = 109m,
                High = 111m,
                Low = 108m,
                Close = 110m,
                Volume = 1800m,
                IsWarmup = false,
                EmaFast = 108m,
                EmaSlow = 100m,
                EmaTrend = 97m,
                Rsi = 64m,
                Atr = 4m,
                SetupDetected = true,
                GridLifecycleState = "Deploying",
                PositionSize = 0m,
                PositionAvgEntry = 0m,
                SignalsEmitted = ["DeployGrid"],
                GridCycleId = "cycle-us"
            }
        };

        var gridCycles = new List<GridCycleEntry>
        {
            new()
            {
                GridCycleId = "cycle-asia",
                DeployTimestampUtc = candleLog[0].TimestampUtc,
                AnchorPrice = 100m,
                LevelsPlaced = 4,
                LevelPrices = [99.5m, 99m],
                LevelsFilled = 2,
                TakeProfitPrice = 101m,
                StopLossPrice = 97m,
                ExitReason = "TakeProfit",
                CyclePnl = 45m,
                CycleDurationMs = (long)TimeSpan.FromHours(5).TotalMilliseconds,
                CloseTimestampUtc = startDate.AddHours(6).ToUnixTimeMilliseconds(),
            },
            new()
            {
                GridCycleId = "cycle-europe",
                DeployTimestampUtc = candleLog[1].TimestampUtc,
                AnchorPrice = 100m,
                LevelsPlaced = 4,
                LevelPrices = [99.7m, 99.2m],
                LevelsFilled = 3,
                TakeProfitPrice = 100.8m,
                StopLossPrice = 97m,
                ExitReason = "StopLoss",
                CyclePnl = -20m,
                CycleDurationMs = (long)TimeSpan.FromHours(7).TotalMilliseconds,
                CloseTimestampUtc = startDate.AddHours(17).ToUnixTimeMilliseconds(),
            },
            new()
            {
                GridCycleId = "cycle-us",
                DeployTimestampUtc = candleLog[2].TimestampUtc,
                AnchorPrice = 100m,
                LevelsPlaced = 4,
                LevelPrices = [99.4m, 98.8m],
                LevelsFilled = 4,
                TakeProfitPrice = 101.4m,
                StopLossPrice = 96.5m,
                ExitReason = "TakeProfit",
                CyclePnl = 80m,
                CycleDurationMs = (long)TimeSpan.FromHours(4).TotalMilliseconds,
                CloseTimestampUtc = startDate.AddHours(22).ToUnixTimeMilliseconds(),
            }
        };

        var fundingRates = new List<FundingRate>
        {
            FundingRate.Create("ETH", startDate.ToUnixTimeMilliseconds(), -0.0002m, 100m),
            FundingRate.Create("ETH", startDate.AddHours(8).ToUnixTimeMilliseconds(), 0m, 100m),
            FundingRate.Create("ETH", startDate.AddHours(16).ToUnixTimeMilliseconds(), 0.0002m, 100m),
        };

        var run = BacktestRun.Create(
            symbol: "ETH",
            intervalsJson: "[\"15m\"]",
            startDateUtc: startDate.ToUnixTimeMilliseconds(),
            endDateUtc: endDate.ToUnixTimeMilliseconds(),
            strategyConfigJson: "{\"grid\":{}}",
            executionConfigJson: "{\"makerFee\":0.0002}",
            initialCapital: 10000m,
            candlesReplayed: 45 * 96,
            elapsedMs: 1500,
            totalTrades: 24,
            winningTrades: 15,
            losingTrades: 9,
            winRate: 62.5m,
            totalPnl: 105m,
            maxDrawdown: 180m,
            averageTradePnl: 4.38m,
            averageHoldTimeMinutes: 240d,
            hedgesOpened: 1,
            totalFeesPaid: 18m,
            tradesJson: "[]",
            equityTimeSeriesJson: JsonSerializer.Serialize(equitySeries),
            candleLogJson: JsonSerializer.Serialize(candleLog),
            gridCycleLogJson: JsonSerializer.Serialize(gridCycles));

        return BacktestSummaryForReview.FromBacktestRun(run, fundingRates);
    }
}
