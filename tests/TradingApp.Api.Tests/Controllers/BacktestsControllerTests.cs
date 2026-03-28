using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradingApp.Api.Models;
using TradingApp.Application.Abstractions.Models;
using TradingApp.Api.Tests.Infrastructure;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;

namespace TradingApp.Api.Tests.Controllers;

[TestClass]
public sealed class BacktestsControllerTests : BaseControllerTests
{
    private const string BaseUrl = "api/backtests";
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";

    private readonly Mock<IBacktestRunner> _backtestRunnerMock = new();
    private readonly Mock<IBacktestRunRepository> _backtestRunRepositoryMock = new();
    private readonly Mock<ICandleRepository> _candleRepositoryMock = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
        builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
        builder.UseSetting("Hyperliquid:Network", "testnet");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IBacktestRunner>();
        services.AddSingleton(_backtestRunnerMock.Object);

        services.RemoveAll<IBacktestRunRepository>();
        services.AddSingleton(_backtestRunRepositoryMock.Object);

        services.RemoveAll<ICandleRepository>();
        services.AddSingleton(_candleRepositoryMock.Object);
    }

    [TestMethod]
    public async Task GivenValidRequest_WhenPostBacktest_ThenReturnsOkWithResult()
    {
        var backtestResult = CreateMockBacktestResult();

        _backtestRunnerMock
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(backtestResult);

        _backtestRunRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<BacktestRun>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var client = GetTestClient();
        var request = CreateValidRequest();

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<BacktestRunResponse>();
        result.Should().NotBeNull();

        result!.Id.Should().NotBeEmpty();
        result.Symbol.Should().Be("BTC");
        result.Intervals.Should().Equal("15m", "1h", "4h");
        result.InitialCapital.Should().Be(10000m);
        result.CandlesReplayed.Should().Be(35040);
        result.TotalTrades.Should().Be(10);
        result.WinningTrades.Should().Be(7);
        result.LosingTrades.Should().Be(3);
        result.WinRate.Should().Be(70m);
        result.TotalPnl.Should().Be(500m);
        result.MaxDrawdown.Should().Be(-100m);
        result.AverageTradePnl.Should().Be(50m);
        result.HedgesOpened.Should().Be(1);
        result.TotalFeesPaid.Should().Be(5m);
        result.ElapsedMs.Should().BeGreaterThan(0);
        result.StartDate.Should().Be(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        result.EndDate.Should().Be(new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc));
        result.CreatedAt.Should().BeAfter(DateTime.MinValue);
        result.Trades.Should().HaveCount(1);
        var trade = result.Trades[0];
        trade.EntryPrice.Should().Be(42150.50m);
        trade.ExitPrice.Should().Be(42361.25m);
        trade.Side.Should().Be("Buy");
        trade.Size.Should().Be(0.001m);
        trade.Pnl.Should().Be(0.21m);
        trade.Fees.Should().Be(0.015m);
        trade.TradeType.Should().Be("GridFill");
        result.StrategyConfig.GridLevels.Should().Be(10);
        result.StrategyConfig.GridSpacing.Should().Be(0.5m);

        _backtestRunnerMock.Verify(
            runner => runner.RunAsync(
                It.Is<BacktestConfig>(config =>
                    config.Symbol == "BTC" &&
                    config.Intervals.SequenceEqual(new[] { "15m", "1h", "4h" }) &&
                    config.InitialCapital == 10000m &&
                    config.StartDateUtc == new DateTimeOffset(request.StartDate!.Value).ToUnixTimeMilliseconds() &&
                    config.EndDateUtc == new DateTimeOffset(request.EndDate!.Value).ToUnixTimeMilliseconds()),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _backtestRunRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.Is<BacktestRun>(run =>
                    run.Symbol == "BTC" &&
                    run.InitialCapital == 10000m &&
                    run.CandlesReplayed == 35040 &&
                    run.TotalTrades == 10),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenExistingBacktestId_WhenGetById_ThenReturnsOkWithResult()
    {
        var backtestRun = CreateBacktestRun();

        _backtestRunRepositoryMock
            .Setup(repository => repository.GetByIdAsync(backtestRun.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backtestRun);

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/{backtestRun.Id}");

        var result = await response.ReadAndAssertSuccessAsync<BacktestRunResponse>();

        result.Id.Should().Be(backtestRun.Id);
        result.Symbol.Should().Be("BTC");
        result.TotalTrades.Should().Be(847);
        result.WinRate.Should().Be(72.3m);
        result.StrategyConfig.GridLevels.Should().Be(10);
        result.Intervals.Should().Equal("15m", "1h", "4h");
        result.Trades.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenNoBacktests_WhenGetList_ThenReturnsEmptyPagedResult()
    {
        _backtestRunRepositoryMock
            .Setup(repository => repository.GetPagedSummariesAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<BacktestRunSummary>
            {
                Items = [],
                Page = 1,
                PageSize = 20,
                TotalCount = 0,
            });

        var client = GetTestClient();

        var response = await client.GetAsync(BaseUrl);

        var result = await response.ReadAndAssertSuccessAsync<PagedResult<BacktestSummaryDto>>();

        result.Items.Should().BeEmpty();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    [TestMethod]
    public async Task GivenExistingBacktests_WhenGetListWithPaging_ThenReturnsPagedSummaries()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        _backtestRunRepositoryMock
            .Setup(repository => repository.GetPagedSummariesAsync(2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<BacktestRunSummary>
            {
                Items =
                [
                    CreateBacktestRunSummary(
                        secondId,
                        "ETH",
                        ["15m", "1h", "4h"],
                        new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                        new DateTime(2024, 2, 29, 23, 59, 59, DateTimeKind.Utc),
                        42,
                        61.5m,
                        1250.75m,
                        -210.4m,
                        new DateTime(2024, 3, 1, 10, 30, 0, DateTimeKind.Utc)),
                ],
                Page = 2,
                PageSize = 1,
                TotalCount = 2,
            });

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}?page=2&pageSize=1");

        var result = await response.ReadAndAssertSuccessAsync<PagedResult<BacktestSummaryDto>>();

        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(secondId);
        result.Items[0].Symbol.Should().Be("ETH");
        result.Items[0].Intervals.Should().Equal("15m", "1h", "4h");
        result.Items[0].TotalTrades.Should().Be(42);
        result.Items[0].WinRate.Should().Be(61.5m);
        result.Items[0].TotalPnl.Should().Be(1250.75m);
        result.Items[0].MaxDrawdown.Should().Be(-210.4m);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(1);
        result.TotalCount.Should().Be(2);
        result.TotalPages.Should().Be(2);

        _backtestRunRepositoryMock.Verify(
            repository => repository.GetPagedSummariesAsync(2, 1, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [DataTestMethod]
    [DataRow(0, 20, "page must be greater than or equal to 1")]
    [DataRow(1, 0, "pageSize must be between 1 and 100")]
    [DataRow(1, 101, "pageSize must be between 1 and 100")]
    public async Task GivenInvalidPaging_WhenGetList_ThenReturnsBadRequest(int page, int pageSize, string errorMessage)
    {
        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}?page={page}&pageSize={pageSize}");

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Contain(errorMessage);
        body.GetProperty("errorCode").GetString().Should().Be("validation_error");

        _backtestRunRepositoryMock.Verify(
            repository => repository.GetPagedSummariesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenValidSymbolAndIntervals_WhenValidate_ThenReturnsOkWithCoverage()
    {
        _candleRepositoryMock
            .Setup(repository => repository.GetCoverageAsync("BTC", "15m", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1704067200000L, 1704152700000L, 96));

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/validate?symbol=BTC&intervals=15m");

        var result = await response.ReadAndAssertSuccessAsync<CandleCoverageResponse>();

        result.Coverage.Should().ContainKey("BTC/15m");
        result.Coverage["BTC/15m"].CandleCount.Should().Be(96);
        result.Coverage["BTC/15m"].From.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1704067200000L).UtcDateTime);
        result.Coverage["BTC/15m"].To.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1704152700000L).UtcDateTime);
    }

    [TestMethod]
    public async Task GivenEndDateBeforeStartDate_WhenPostBacktest_ThenReturnsBadRequest()
    {
        var client = GetTestClient();
        var request = CreateValidRequest();
        request.StartDate = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        request.EndDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Contain("endDate must be after startDate");
        body.GetProperty("errorCode").GetString().Should().Be("validation_error");
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task GivenUnknownSymbol_WhenPostBacktest_ThenReturnsBadRequestWithSupportedSymbols()
    {
        var client = GetTestClient();
        var request = CreateValidRequest();
        request.Symbol = "INVALID";

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Contain("Unknown symbol 'INVALID'");
        body.GetProperty("errorMessage").GetString().Should().Contain("Supported:");
        body.GetProperty("errorCode").GetString().Should().Be("validation_error");
    }

    [TestMethod]
    public async Task GivenInvalidInterval_WhenPostBacktest_ThenReturnsBadRequestWithValidIntervals()
    {
        var client = GetTestClient();
        var request = CreateValidRequest();
        request.Intervals = ["2m"];

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Contain("Invalid interval '2m'");
        body.GetProperty("errorCode").GetString().Should().Be("validation_error");
    }

    [TestMethod]
    public async Task GivenGridLevelsZero_WhenPostBacktest_ThenReturnsBadRequest()
    {
        var client = GetTestClient();
        var request = CreateValidRequest();
        request.StrategyConfig.GridLevels = 0;

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.TryGetProperty("StrategyConfig.GridLevels", out var gridLevelErrors).Should().BeTrue();
        gridLevelErrors[0].GetString().Should().Contain("gridLevels must be > 0");
    }

    [TestMethod]
    public async Task GivenMissingRequiredFields_WhenPostBacktest_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.PostAsync(
            BaseUrl,
            GetStringContent(new
            {
                intervals = new[] { "15m", "1h", "4h" },
                startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                endDate = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                initialCapital = 10000m,
                strategyConfig = new
                {
                    gridLevels = 10,
                    gridSpacing = 0.5m,
                    takeProfitPercent = 1.0m,
                    breakdownThreshold = -3.0m,
                    makerFee = 0.0001m,
                    takerFee = 0.00035m,
                    slippage = 0m,
                    positionSize = 100m,
                    leverage = 3m,
                    stopLossPercent = 5m,
                },
            }));

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.TryGetProperty("Symbol", out _).Should().BeTrue();
    }

    [TestMethod]
    public async Task GivenNonExistentId_WhenGetById_ThenReturnsNotFound()
    {
        _backtestRunRepositoryMock
            .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BacktestRun?)null);

        var client = GetTestClient();
        var nonExistentId = Guid.NewGuid();

        var response = await client.GetAsync($"{BaseUrl}/{nonExistentId}");

        response.AssertStatusCode(HttpStatusCode.NotFound);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("not_found");
        body.GetProperty("errorMessage").GetString().Should().Contain(nonExistentId.ToString());
    }

    [TestMethod]
    public async Task GivenUnknownSymbol_WhenValidate_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/validate?symbol=INVALID&intervals=15m");

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Contain("Unknown symbol 'INVALID'");
        body.GetProperty("errorCode").GetString().Should().Be("validation_error");
    }

    [TestMethod]
    public async Task GivenMissingSymbol_WhenValidate_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/validate?intervals=15m");

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Contain("symbol is required");
        body.GetProperty("errorCode").GetString().Should().Be("validation_error");
    }

    [TestMethod]
    public async Task GivenInvalidInterval_WhenValidate_ThenReturnsBadRequest()
    {
        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/validate?symbol=BTC&intervals=2m");

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Contain("Invalid interval '2m'");
        body.GetProperty("errorCode").GetString().Should().Be("validation_error");
    }

    [TestMethod]
    public async Task GivenNoCandleData_WhenPostBacktest_ThenReturnsNotFound()
    {
        _backtestRunnerMock
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("No candle data found for the requested range"));

        var client = GetTestClient();
        var request = CreateValidRequest();

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.NotFound);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("not_found");
        body.GetProperty("errorMessage").GetString().Should().Contain("No candle data found");
    }

    [TestMethod]
    public async Task GivenBacktestTimeout_WhenPostBacktest_ThenReturnsRequestTimeout()
    {
        _backtestRunnerMock
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var client = GetTestClient();
        var request = CreateValidRequest();

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.RequestTimeout);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("request_timeout");
        body.GetProperty("errorMessage").GetString().Should().Contain("Request was cancelled or exceeded maximum timeout");

        _backtestRunRepositoryMock.Verify(
            repository => repository.AddAsync(It.IsAny<BacktestRun>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenBacktestRunnerUnavailable_WhenPostBacktest_ThenReturnsServiceUnavailable()
    {
        _backtestRunnerMock
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BacktestUnavailableException(
                "Backtest execution is not available in the API host yet. The concrete strategy pipeline services are not registered, so the endpoint can validate and read saved results but cannot run new backtests."));

        var client = GetTestClient();
        var request = CreateValidRequest();

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.ServiceUnavailable);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("backtest_unavailable");
        body.GetProperty("errorMessage").GetString().Should().Contain("Backtest execution is not available in the API host yet");

        _backtestRunRepositoryMock.Verify(
            repository => repository.AddAsync(It.IsAny<BacktestRun>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenBacktestWithNoTrades_WhenPostBacktest_ThenReturnsEmptyTradesAndZeroMetrics()
    {
        var backtestResult = new BacktestResult
        {
            TotalTrades = 0,
            WinningTrades = 0,
            LosingTrades = 0,
            WinRate = 0m,
            TotalPnL = 0m,
            MaxDrawdownAbsolute = 0m,
            MaxDrawdownPercent = 0m,
            AverageTradePnL = 0m,
            AverageHoldTime = TimeSpan.Zero,
            HedgesOpened = 0,
            TotalFeesPaid = 0m,
            GridCycles = 0,
            CandlesReplayed = 35040,
            FinalEquity = 10000m,
            EquityTimeSeries = [],
            TradeLog = [],
        };

        _backtestRunnerMock
            .Setup(runner => runner.RunAsync(It.IsAny<BacktestConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(backtestResult);

        _backtestRunRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<BacktestRun>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var client = GetTestClient();
        var request = CreateValidRequest();

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<BacktestRunResponse>();
        result.Should().NotBeNull();

        result!.Trades.Should().BeEmpty();
        result.TotalTrades.Should().Be(0);
        result.WinRate.Should().Be(0m);
        result.TotalPnl.Should().Be(0m);
        result.MaxDrawdown.Should().Be(0m);
        result.AverageTradePnl.Should().Be(0m);
        result.HedgesOpened.Should().Be(0);
        result.TotalFeesPaid.Should().Be(0m);
    }

    [TestMethod]
    public async Task GivenNoCandleDataForInterval_WhenValidate_ThenReturnsNullDatesAndZeroCount()
    {
        _candleRepositoryMock
            .Setup(repository => repository.GetCoverageAsync("BTC", "15m", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((long?)null, (long?)null, 0));

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/validate?symbol=BTC&intervals=15m");

        var result = await response.ReadAndAssertSuccessAsync<CandleCoverageResponse>();

        result.Coverage.Should().ContainKey("BTC/15m");
        result.Coverage["BTC/15m"].CandleCount.Should().Be(0);
        result.Coverage["BTC/15m"].From.Should().BeNull();
        result.Coverage["BTC/15m"].To.Should().BeNull();
    }

    private static RunBacktestRequest CreateValidRequest()
    {
        return new RunBacktestRequest
        {
            Symbol = "BTC",
            Intervals = ["15m", "1h", "4h"],
            StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            InitialCapital = 10000m,
            StrategyConfig = new GridStrategyConfigRequest
            {
                GridLevels = 10,
                GridSpacing = 0.5m,
                TakeProfitPercent = 1.0m,
                BreakdownThreshold = -3.0m,
                MakerFee = 0.0001m,
                TakerFee = 0.00035m,
                Slippage = 0m,
                PositionSize = 100m,
                Leverage = 3m,
                StopLossPercent = 5m,
            },
        };
    }

    private static BacktestResult CreateMockBacktestResult()
    {
        return new BacktestResult
        {
            TotalTrades = 10,
            WinningTrades = 7,
            LosingTrades = 3,
            WinRate = 70m,
            TotalPnL = 500m,
            MaxDrawdownAbsolute = -100m,
            MaxDrawdownPercent = -1m,
            AverageTradePnL = 50m,
            AverageHoldTime = TimeSpan.FromMinutes(120),
            HedgesOpened = 1,
            TotalFeesPaid = 5m,
            GridCycles = 2,
            CandlesReplayed = 35040,
            FinalEquity = 10500m,
            EquityTimeSeries = [],
            TradeLog =
            [
                new BacktestTrade
                {
                    TradeId = Guid.NewGuid().ToString(),
                    GridCycleId = Guid.NewGuid().ToString(),
                    EntryTimeUtc = 1704067200000,
                    EntryPrice = 42150.50m,
                    ExitTimeUtc = 1704082800000,
                    ExitPrice = 42361.25m,
                    Side = OrderSide.Buy,
                    Size = 0.001m,
                    PnL = 0.21m,
                    Fees = 0.015m,
                    TradeType = TradeType.GridFill,
                },
            ],
        };
    }

    private static BacktestRun CreateBacktestRun()
    {
        return BacktestRun.Create(
            symbol: "BTC",
            intervalsJson: "[\"15m\",\"1h\",\"4h\"]",
            startDateUtc: 1704067200000,
            endDateUtc: 1735689599000,
            strategyConfigJson: JsonSerializer.Serialize(new GridStrategyConfig
            {
                GridLevels = 10,
                GridSpacing = 0.5m,
                TakeProfitPercent = 1m,
                BreakdownThreshold = -3m,
                MakerFee = 0.0001m,
                TakerFee = 0.00035m,
                Slippage = 0m,
                PositionSize = 100m,
                Leverage = 3m,
                StopLossPercent = 5m,
            }),
            initialCapital: 10000m,
            candlesReplayed: 35040,
            elapsedMs: 12500,
            totalTrades: 847,
            winningTrades: 612,
            losingTrades: 235,
            winRate: 72.3m,
            totalPnl: 4521.87m,
            maxDrawdown: -1234.56m,
            averageTradePnl: 5.34m,
            averageHoldTimeMinutes: 245.0,
            hedgesOpened: 12,
            totalFeesPaid: 89.23m,
            tradesJson: "[]");
    }

    private static BacktestRunSummary CreateBacktestRunSummary(
        Guid id,
        string symbol,
        IReadOnlyList<string> intervals,
        DateTime startDate,
        DateTime endDate,
        int totalTrades,
        decimal winRate,
        decimal totalPnl,
        decimal maxDrawdown,
        DateTime createdAt)
    {
        return new BacktestRunSummary
        {
            Id = id,
            Symbol = symbol,
            Intervals = intervals,
            StartDate = startDate,
            EndDate = endDate,
            TotalTrades = totalTrades,
            WinRate = winRate,
            TotalPnl = totalPnl,
            MaxDrawdown = maxDrawdown,
            CreatedAt = createdAt,
        };
    }
}