using System.Text.Json;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Backtesting;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.StrategyAuthoring.Serialization;
using TradingApp.Domain.Entities;

namespace TradingApp.Application.Tests.Backtesting;

[TestClass]
public sealed class GetBacktestDebugQueryHandlerTests
{
    private readonly Mock<IBacktestRunRepository> _repositoryMock = new();

    [TestMethod]
    public async Task GivenCycleAuditCandles_WhenHandle_ThenEnrichesCandleEvaluationsWithIndicatorSeries()
    {
        var candleEntries = Enumerable.Range(0, 220)
            .Select(index => new CandleEvaluationEntry
            {
                TimestampUtc = 1_700_000_000_000 + (index * 900_000L),
                Open = 100m + index,
                High = 101m + index,
                Low = 99m + index,
                Close = 100.5m + index,
                Volume = 50m + index,
                IsWarmup = false,
                EmaFast = 0m,
                EmaSlow = 0m,
                EmaTrend = 0m,
                Rsi = 0m,
                Atr = 0m,
                SetupDetected = false,
                GridLifecycleState = "Active",
                PositionSize = 0m,
                PositionAvgEntry = 0m,
                SignalsEmitted = [],
                GridCycleId = "cycle-1",
            })
            .ToList();

        var backtestRun = BacktestRun.Create(
            symbol: "BTC",
            intervalsJson: "[\"15m\",\"1h\",\"4h\"]",
            startDateUtc: 1_700_000_000_000,
            endDateUtc: 1_700_197_100_000,
            strategyConfigJson: "{}",
            executionConfigJson: "{}",
            initialCapital: 10000m,
            candlesReplayed: 220,
            elapsedMs: 100,
            totalTrades: 0,
            winningTrades: 0,
            losingTrades: 0,
            winRate: 0m,
            totalPnl: 0m,
            maxDrawdown: 0m,
            averageTradePnl: 0m,
            averageHoldTimeMinutes: 0d,
            hedgesOpened: 0,
            totalFeesPaid: 0m,
            tradesJson: "[]",
            candleLogJson: JsonSerializer.Serialize(candleEntries, StrategyJsonOptions.Default),
            orderEventLogJson: "[]",
            gridCycleLogJson: "[]");

        _repositoryMock
            .Setup(repository => repository.GetByIdAsync(backtestRun.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backtestRun);

        var sut = new GetBacktestDebugQueryHandler(_repositoryMock.Object);

        var result = await sut.Handle(new GetBacktestDebugQuery(backtestRun.Id, "cycle-1"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.CandleEvaluations.Should().HaveCount(220);
        var last = result.CandleEvaluations[^1];
        last.Indicators.Should().NotBeNull();
        last.Indicators!.EmaFast.Should().NotBeNull();
        last.Indicators!.EmaSlow.Should().NotBeNull();
        last.Indicators!.EmaTrend.Should().NotBeNull();
        last.Indicators!.Rsi.Should().NotBeNull();
        last.Indicators!.Atr.Should().NotBeNull();
        last.Indicators!.MacdLine.Should().NotBeNull();
        last.Indicators!.MacdSignal.Should().NotBeNull();
        last.Indicators!.MacdHistogram.Should().NotBeNull();
        last.Indicators!.BollingerUpper.Should().NotBeNull();
        last.Indicators!.BollingerMiddle.Should().NotBeNull();
        last.Indicators!.BollingerLower.Should().NotBeNull();
    }
}