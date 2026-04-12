using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TradingApp.Api.Models;
using TradingApp.Api.Services;
using TradingApp.Application.Abstractions.Models;
using TradingApp.Api.Tests.Infrastructure;
using TradingApp.Application.Abstractions.Exceptions;
using TradingApp.Application.Abstractions.Repositories;
using TradingApp.Application.Abstractions.Services;
using TradingApp.Application.Backtesting;
using TradingApp.Application.Backtesting.Models;
using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Application.StrategyAuthoring.Serialization;
using TradingApp.Application.Trading.Models;
using TradingApp.Domain.Entities;
using TradingApp.Domain.Enums;
using TradingApp.Domain.Trading;

namespace TradingApp.Api.Tests.Controllers;

[TestClass]
public sealed class BacktestsControllerTests : BaseControllerTests
{
    private const string BaseUrl = "api/backtests";
    private const string TestPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f2a4c5890a0c1f9b2e";

    private readonly Mock<IBacktestRunner> _backtestRunnerMock = new();
    private readonly Mock<IBacktestRunRepository> _backtestRunRepositoryMock = new();
    private readonly Mock<ICandleRepository> _candleRepositoryMock = new();
    private readonly Mock<IStrategyRepository> _strategyRepositoryMock = new();
    private readonly Mock<IStrategyRevisionRepository> _strategyRevisionRepositoryMock = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Hyperliquid:PrivateKey", TestPrivateKey);
        builder.UseSetting("Hyperliquid:BaseUrl", "https://api.hyperliquid-testnet.xyz");
        builder.UseSetting("Hyperliquid:Network", "testnet");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        _strategyRepositoryMock
            .Setup(repository => repository.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        services.RemoveAll<IBacktestRunner>();
        services.AddSingleton(_backtestRunnerMock.Object);

        services.RemoveAll<IBacktestRunRepository>();
        services.AddSingleton(_backtestRunRepositoryMock.Object);

        services.RemoveAll<ICandleRepository>();
        services.AddSingleton(_candleRepositoryMock.Object);

        services.RemoveAll<IStrategyRepository>();
        services.AddSingleton(_strategyRepositoryMock.Object);

        services.RemoveAll<IStrategyRevisionRepository>();
        services.AddSingleton(_strategyRevisionRepositoryMock.Object);

        // Suppress the background processor so it doesn't interfere with tests
        services.RemoveAll<IHostedService>();
    }

    [TestMethod]
    public async Task GivenValidRequest_WhenPostBacktest_ThenReturnsAcceptedWithQueuedStatus()
    {
        _backtestRunRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<BacktestRun>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var client = GetTestClient();
        var request = CreateValidRequest();

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<BacktestRunResponse>(BaseControllerTestsJson.Options);
        result.Should().NotBeNull();

        result!.Id.Should().NotBeEmpty();
        result.Symbol.Should().Be("BTC");
        result.Intervals.Should().Equal("15m", "1h", "4h");
        result.InitialCapital.Should().Be(10000m);
        result.Status.Should().Be("Queued");
        result.Progress.Should().Be(0);
        result.CandlesReplayed.Should().Be(0);
        result.TotalTrades.Should().Be(0);
        result.StartDate.Should().Be(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        result.EndDate.Should().Be(new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc));
        result.CreatedAt.Should().BeAfter(DateTime.MinValue);
        result.StrategyConfig.StrategyName.Should().Be("BTC Grid");
        result.StrategyConfig.Market.Should().Be("BTC");
        result.StrategyConfig.Grid.Should().NotBeNull();
        result.StrategyConfig.Grid!.Levels.Should().Be(10);
        result.StrategyConfig.Grid.EntryMode.Should().Be(EntryModes.WaitForLimitPrice);
        result.StrategyConfig.Grid.AnchorPrice.Should().Be(42000m);
        result.StrategyConfig.Grid.Spacing.Should().Be(0.5m);
        result.StrategyConfig.Exit.TakeProfit.Enabled.Should().BeTrue();
        result.StrategyConfig.Exit.TakeProfit.Value.Should().Be(1.0m);
        result.StrategyConfig.Exit.StopLoss.Enabled.Should().BeTrue();
        result.StrategyConfig.Exit.StopLoss.Value.Should().Be(5m);
        result.StrategyConfig.Risk.PositionSizeValue.Should().Be(100m);
        result.StrategyConfig.Risk.Leverage.Should().Be(3m);
        result.ExecutionConfig.FeeModel.MakerFeeRate.Should().Be(0.0001m);
        result.ExecutionConfig.FeeModel.TakerFeeRate.Should().Be(0.00035m);
        result.ExecutionConfig.FeeModel.SlippageRate.Should().Be(0m);

        _backtestRunRepositoryMock.Verify(
            repository => repository.AddAsync(
                It.Is<BacktestRun>(run =>
                    run.Symbol == "BTC" &&
                    run.InitialCapital == 10000m &&
                    run.Status == Domain.Enums.BacktestStatus.Queued),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenInitialMarketThenGridRequest_WhenPostBacktest_ThenAcceptsWithoutManualAnchorPrice()
    {
        _backtestRunRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<BacktestRun>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var client = GetTestClient();
        var request = CreateValidRequest();
        var grid = request.StrategyConfig?.Grid;
        grid.Should().NotBeNull();
        grid!.EntryMode = EntryModes.InitialMarketThenGrid;
        grid.AnchorPrice = null;

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<BacktestRunResponse>(BaseControllerTestsJson.Options);
        result.Should().NotBeNull();
        result!.StrategyConfig.Grid.Should().NotBeNull();
        result.StrategyConfig.Grid!.EntryMode.Should().Be(EntryModes.InitialMarketThenGrid);
        result.StrategyConfig.Grid.AnchorPrice.Should().BeNull();
    }

    [TestMethod]
    public async Task GivenRiskBasedRequest_WhenPostBacktest_ThenMapsRiskPerTradePercent()
    {
        BacktestRun? savedRun = null;

        _backtestRunRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<BacktestRun>(), It.IsAny<CancellationToken>()))
            .Callback<BacktestRun, CancellationToken>((run, _) => savedRun = run)
            .Returns(Task.CompletedTask);

        var client = GetTestClient();
        var request = CreateValidRequest();
        request.StrategyConfig!.Risk.PositionSizeType = "risk_based";
        request.StrategyConfig.Risk.PositionSizeValue = 1m;
        request.StrategyConfig.Risk.RiskPerTradePercent = 1.5m;

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<BacktestRunResponse>(BaseControllerTestsJson.Options);

        result.Should().NotBeNull();
        result!.StrategyConfig.Risk.PositionSizeType.Should().Be(PositionSizeType.RiskBased);
        result.StrategyConfig.Risk.RiskPerTradePercent.Should().Be(1.5m);
        savedRun.Should().NotBeNull();

        var mappedConfig = JsonSerializer.Deserialize<StrategyConfig>(savedRun!.StrategyConfigJson, StrategyJsonOptions.Default);
        mappedConfig.Should().NotBeNull();
        mappedConfig!.Risk.PositionSizeType.Should().Be(PositionSizeType.RiskBased);
        mappedConfig.Risk.RiskPerTradePercent.Should().Be(1.5m);
    }

    [TestMethod]
    public async Task GivenAutoLeverageRequest_WhenPostBacktest_ThenMapsAutoLeverage()
    {
        BacktestRun? savedRun = null;

        _backtestRunRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<BacktestRun>(), It.IsAny<CancellationToken>()))
            .Callback<BacktestRun, CancellationToken>((run, _) => savedRun = run)
            .Returns(Task.CompletedTask);

        var client = GetTestClient();
        var request = CreateValidRequest();
        request.StrategyConfig!.Risk.AutoLeverage = true;
        request.StrategyConfig.Risk.RiskPerTradePercent = 1.5m;

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<BacktestRunResponse>(BaseControllerTestsJson.Options);

        result.Should().NotBeNull();
        result!.StrategyConfig.Risk.AutoLeverage.Should().BeTrue();
        savedRun.Should().NotBeNull();

        var mappedConfig = JsonSerializer.Deserialize<StrategyConfig>(savedRun!.StrategyConfigJson, StrategyJsonOptions.Default);
        mappedConfig.Should().NotBeNull();
        mappedConfig!.Risk.AutoLeverage.Should().BeTrue();
    }

    [TestMethod]
    public async Task GivenValidStrategyId_WhenPostBacktest_ThenReturnsAcceptedWithStrategyFields()
    {
        BacktestRun? savedRun = null;
        var strategy = CreateStrategy();

        _strategyRepositoryMock
            .Setup(repository => repository.GetByIdAsync(strategy.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategy);
        _strategyRevisionRepositoryMock
            .Setup(repository => repository.GetLatestRevisionNumberAsync(strategy.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _backtestRunRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<BacktestRun>(), It.IsAny<CancellationToken>()))
            .Callback<BacktestRun, CancellationToken>((run, _) => savedRun = run)
            .Returns(Task.CompletedTask);

        var client = GetTestClient();
        var request = new RunBacktestRequest
        {
            StrategyId = strategy.Id,
            StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            InitialCapital = 10000m,
            ExecutionConfig = new ExecutionConfigRequest
            {
                MakerFee = 0.0001m,
                TakerFee = 0.00035m,
                Slippage = 0m,
            },
        };

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<BacktestRunResponse>(BaseControllerTestsJson.Options);

        result.Should().NotBeNull();
        result!.Symbol.Should().Be("BTC");
        result.Intervals.Should().Equal("15m", "1h", "4h");
        result.StrategyId.Should().Be(strategy.Id);
        result.StrategyRevisionId.Should().Be(3);
        savedRun.Should().NotBeNull();
        savedRun!.StrategyId.Should().Be(strategy.Id);
        savedRun.StrategyRevisionId.Should().Be(3);

        _strategyRevisionRepositoryMock.Verify(
            repository => repository.GetLatestRevisionNumberAsync(strategy.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenStrategyUsingDisplayMarket_WhenPostBacktest_ThenNormalizesSymbolForBacktest()
    {
        BacktestRun? savedRun = null;
        var strategy = CreateStrategy();
        var strategyConfigNode = JsonNode.Parse(strategy.ConfigJson)?.AsObject()
            ?? throw new InvalidOperationException("Strategy config JSON was invalid.");
        strategyConfigNode["market"] = "BTC-USD";
        strategy.Update(strategy.Name, strategyConfigNode.ToJsonString());

        _strategyRepositoryMock
            .Setup(repository => repository.GetByIdAsync(strategy.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategy);
        _strategyRevisionRepositoryMock
            .Setup(repository => repository.GetLatestRevisionNumberAsync(strategy.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _backtestRunRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<BacktestRun>(), It.IsAny<CancellationToken>()))
            .Callback<BacktestRun, CancellationToken>((run, _) => savedRun = run)
            .Returns(Task.CompletedTask);

        var client = GetTestClient();
        var request = new RunBacktestRequest
        {
            StrategyId = strategy.Id,
            StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            InitialCapital = 10000m,
            ExecutionConfig = new ExecutionConfigRequest
            {
                MakerFee = 0.0001m,
                TakerFee = 0.00035m,
                Slippage = 0m,
            },
        };

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<BacktestRunResponse>(BaseControllerTestsJson.Options);

        result.Should().NotBeNull();
        result!.Symbol.Should().Be("BTC");
        savedRun.Should().NotBeNull();
        savedRun!.Symbol.Should().Be("BTC");
    }

    [TestMethod]
    public async Task GivenNoStrategyIdAndNoConfig_WhenPostBacktest_ThenReturnsBadRequest()
    {
        var client = GetTestClient();
        var request = new RunBacktestRequest
        {
            Symbol = "BTC",
            Intervals = ["15m"],
            StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            InitialCapital = 10000m,
            ExecutionConfig = new ExecutionConfigRequest
            {
                MakerFee = 0.0001m,
                TakerFee = 0.00035m,
                Slippage = 0m,
            },
        };

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Contain("Either strategyId or strategyConfig must be provided");
        body.GetProperty("errorCode").GetString().Should().Be("validation_error");
    }

    [TestMethod]
    public async Task GivenNonExistentStrategyId_WhenPostBacktest_ThenReturnsNotFound()
    {
        var strategyId = Guid.NewGuid();

        _strategyRepositoryMock
            .Setup(repository => repository.GetByIdAsync(strategyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Strategy?)null);

        var client = GetTestClient();
        var request = new RunBacktestRequest
        {
            StrategyId = strategyId,
            StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            InitialCapital = 10000m,
            ExecutionConfig = new ExecutionConfigRequest
            {
                MakerFee = 0.0001m,
                TakerFee = 0.00035m,
                Slippage = 0m,
            },
        };

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.NotFound);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("not_found");
        body.GetProperty("errorMessage").GetString().Should().Contain(strategyId.ToString());
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
        result.StrategyConfig.Grid.Should().NotBeNull();
        result.StrategyConfig.Grid!.Levels.Should().Be(10);
        result.Intervals.Should().Equal("15m", "1h", "4h");
        result.Trades.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenBacktestWithRTrackedTrades_WhenGetById_ThenReturnsAggregateAndTradeLevelRMetrics()
    {
        var backtestRun = CreateBacktestRunWithRTrackedTrades();

        _backtestRunRepositoryMock
            .Setup(repository => repository.GetByIdAsync(backtestRun.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backtestRun);

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/{backtestRun.Id}");

        var result = await response.ReadAndAssertSuccessAsync<BacktestRunResponse>();

        result.Expectancy.Should().Be(0.56m);
        result.ProfitFactor.Should().BeApproximately(2.1667m, 0.0001m);
        result.Sqn.Should().NotBeNull();
        result.AvgWinR.Should().BeApproximately(2.08m, 0.01m);
        result.AvgLossR.Should().BeApproximately(-0.96m, 0.01m);
        result.RWinRate.Should().Be(50m);
        result.RDistribution.Should().Equal([2.1m, -1.0m, 1.5m, -1.0m, 3.0m, -0.8m, 2.0m, -1.0m, 1.8m, -1.0m]);
        result.Trades.Should().HaveCount(10);
        result.Trades[0].InitialRDollars.Should().Be(100m);
        result.Trades[0].RMultipleResult.Should().Be(2.1m);
        result.Trades[0].Mfe.Should().Be(3m);
        result.Trades[0].Mae.Should().Be(-0.5m);
    }

    [TestMethod]
    public async Task GivenBacktestWithAuditData_WhenGetDebug_ThenReturns200WithFilteredData()
    {
        var backtestRun = CreateBacktestRunWithAuditData();

        _backtestRunRepositoryMock
            .Setup(repository => repository.GetByIdAsync(backtestRun.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backtestRun);

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/{backtestRun.Id}/debug?cycleId=cycle-1");

        var result = await response.ReadAndAssertSuccessAsync<BacktestDebugResponse>();

        result.CycleId.Should().Be("cycle-1");
        result.CandleEvaluations.Should().HaveCount(1);
        result.CandleEvaluations[0].GridCycleId.Should().Be("cycle-1");
        result.OrderEvents.Should().HaveCount(1);
        result.OrderEvents[0].GridCycleId.Should().Be("cycle-1");
        result.GridCycleSummary.Should().NotBeNull();
        result.GridCycleSummary!.GridCycleId.Should().Be("cycle-1");
    }

    [TestMethod]
    public async Task GivenBacktestWithoutAuditData_WhenGetDebug_ThenReturns204()
    {
        var backtestRun = CreateBacktestRun();

        _backtestRunRepositoryMock
            .Setup(repository => repository.GetByIdAsync(backtestRun.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(backtestRun);

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/{backtestRun.Id}/debug?cycleId=cycle-1");

        response.AssertStatusCode(HttpStatusCode.NoContent);
    }

    [TestMethod]
    public async Task GivenNonExistentBacktest_WhenGetDebug_ThenReturnsNotFound()
    {
        var backtestId = Guid.NewGuid();

        _backtestRunRepositoryMock
            .Setup(repository => repository.GetByIdAsync(backtestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BacktestRun?)null);

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/{backtestId}/debug?cycleId=cycle-1");

        response.AssertStatusCode(HttpStatusCode.NotFound);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("not_found");
        body.GetProperty("errorMessage").GetString().Should().Contain(backtestId.ToString());
    }

    [TestMethod]
    public async Task GivenNoBacktests_WhenGetList_ThenReturnsEmptyPagedResult()
    {
        _backtestRunRepositoryMock
            .Setup(repository => repository.GetPagedSummariesAsync(1, 20, null, null, It.IsAny<CancellationToken>()))
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
            .Setup(repository => repository.GetPagedSummariesAsync(2, 1, null, null, It.IsAny<CancellationToken>()))
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
        result.Items[0].StrategyId.Should().BeNull();
        result.Items[0].StrategyRevisionId.Should().BeNull();
        result.Items[0].StrategyName.Should().BeNull();
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(1);
        result.TotalCount.Should().Be(2);
        result.TotalPages.Should().Be(2);

        _backtestRunRepositoryMock.Verify(
            repository => repository.GetPagedSummariesAsync(2, 1, null, null, It.IsAny<CancellationToken>()),
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
            repository => repository.GetPagedSummariesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()),
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
    public async Task GivenDisplayStyleSymbol_WhenValidate_ThenNormalizesSymbolForCoverage()
    {
        _candleRepositoryMock
            .Setup(repository => repository.GetCoverageAsync("BTC", "15m", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1704067200000L, 1704152700000L, 96));

        var client = GetTestClient();

        var response = await client.GetAsync($"{BaseUrl}/validate?symbol=BTC-USD&intervals=15m");

        var result = await response.ReadAndAssertSuccessAsync<CandleCoverageResponse>();

        result.Coverage.Should().ContainKey("BTC/15m");
        result.Coverage["BTC/15m"].CandleCount.Should().Be(96);
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
    public async Task GivenFutureEndDate_WhenPostBacktest_ThenReturnsBadRequest()
    {
        var client = GetTestClient();
        var request = CreateValidRequest();
        request.EndDate = DateTime.UtcNow.AddDays(1);

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Contain("endDate cannot be in the future");
        body.GetProperty("errorCode").GetString().Should().Be("validation_error");
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
        var grid = request.StrategyConfig?.Grid;
        grid.Should().NotBeNull();
        grid!.Levels = 0;

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.TryGetProperty("StrategyConfig.Grid.Levels", out var gridLevelErrors).Should().BeTrue();
        gridLevelErrors[0].GetString().Should().Contain("gridLevels must be > 0");
    }

    [TestMethod]
    public async Task GivenLimitEntryModeWithoutLimitPrice_WhenPostBacktest_ThenReturnsBadRequest()
    {
        var client = GetTestClient();
        var request = CreateValidRequest();
        var grid = request.StrategyConfig?.Grid;
        grid.Should().NotBeNull();
        grid!.AnchorPrice = null;

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorMessage").GetString().Should().Contain("manualAnchorPrice is required");
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
                    strategyName = "BTC Grid",
                    market = "BTC",
                    timeframe = "15m",
                    direction = "long",
                    enabled = true,
                    grid = new
                    {
                        levels = 10,
                        entryMode = EntryModes.WaitForLimitPrice,
                        anchorPrice = 42000m,
                        spacing = 0.5m,
                        breakdownThreshold = -3.0m,
                    },
                    exit = new
                    {
                        takeProfit = new
                        {
                            enabled = true,
                            type = "fixed_percent",
                            value = 1.0m,
                        },
                        stopLoss = new
                        {
                            enabled = true,
                            type = "fixed_percent",
                            value = 5m,
                        },
                        exitOnOppositeSignal = false,
                    },
                    risk = new
                    {
                        positionSizeType = "fixed_notional",
                        positionSizeValue = 100m,
                        leverage = 3m,
                        maxOpenTrades = 1,
                        cooldownValue = 0,
                        cooldownUnit = "candles",
                        allowSameCandleReentry = false,
                    },
                },
                executionConfig = new
                {
                    makerFee = 0.0001m,
                    takerFee = 0.00035m,
                    slippage = 0m,
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
    public async Task GivenValidRequest_WhenPostBacktest_ThenEnqueuesJobAndSavesQueuedRun()
    {
        BacktestRun? savedRun = null;

        _backtestRunRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<BacktestRun>(), It.IsAny<CancellationToken>()))
            .Callback<BacktestRun, CancellationToken>((run, _) => savedRun = run)
            .Returns(Task.CompletedTask);

        var client = GetTestClient();
        var request = CreateValidRequest();

        var response = await client.PostAsJsonAsync(BaseUrl, request);

        response.AssertStatusCode(HttpStatusCode.Accepted);

        savedRun.Should().NotBeNull();
        savedRun!.Status.Should().Be(Domain.Enums.BacktestStatus.Queued);
        savedRun.Progress.Should().Be(0);
        savedRun.CandlesReplayed.Should().Be(0);
        savedRun.TotalTrades.Should().Be(0);
        savedRun.AuditLogEnabled.Should().BeTrue();

        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"api/backtests/{savedRun.Id}");
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
            StrategyConfig = new StrategyConfigRequest
            {
                StrategyName = "BTC Grid",
                Market = "BTC",
                Timeframe = "15m",
                Direction = "long",
                Enabled = true,
                Grid = new GridConfigRequest
                {
                    Levels = 10,
                    EntryMode = EntryModes.WaitForLimitPrice,
                    AnchorPrice = 42000m,
                    Spacing = 0.5m,
                    BreakdownThreshold = -3.0m,
                },
                Exit = new ExitConfigRequest
                {
                    TakeProfit = new ExitRuleRequest
                    {
                        Enabled = true,
                        Type = "fixed_percent",
                        Value = 1.0m,
                    },
                    StopLoss = new ExitRuleRequest
                    {
                        Enabled = true,
                        Type = "fixed_percent",
                        Value = 5m,
                    },
                    ExitOnOppositeSignal = false,
                },
                Risk = new RiskConfigRequest
                {
                    PositionSizeType = "fixed_notional",
                    PositionSizeValue = 100m,
                    RiskPerTradePercent = null,
                    Leverage = 3m,
                    MaxOpenTrades = 1,
                    CooldownValue = 0,
                    CooldownUnit = "candles",
                    AllowSameCandleReentry = false,
                },
            },
            ExecutionConfig = new ExecutionConfigRequest
            {
                MakerFee = 0.0001m,
                TakerFee = 0.00035m,
                Slippage = 0m,
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
            strategyConfigJson: CreateTestStrategyConfigJson(),
            executionConfigJson: CreateTestExecutionConfigJson(),
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

    private static BacktestRun CreateBacktestRunWithAuditData()
    {
        return BacktestRun.Create(
            symbol: "BTC",
            intervalsJson: "[\"15m\",\"1h\",\"4h\"]",
            startDateUtc: 1704067200000,
            endDateUtc: 1735689599000,
            strategyConfigJson: CreateTestStrategyConfigJson(),
            executionConfigJson: CreateTestExecutionConfigJson(),
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
            tradesJson: "[]",
            candleLogJson: BacktestRunResponseMapper.SerializeCandleLog(
            [
                new CandleEvaluationEntry
                {
                    TimestampUtc = 1704067200000,
                    Open = 42000m,
                    High = 42100m,
                    Low = 41950m,
                    Close = 42080m,
                    Volume = 1234m,
                    IsWarmup = false,
                    EmaFast = 42010m,
                    EmaSlow = 41990m,
                    EmaTrend = 41850m,
                    Rsi = 58m,
                    Atr = 125m,
                    SetupDetected = true,
                    GridLifecycleState = "Active",
                    PositionSize = 0.01m,
                    PositionAvgEntry = 42000m,
                    SignalsEmitted = ["DeployGrid"],
                    GridCycleId = "cycle-1",
                },
                new CandleEvaluationEntry
                {
                    TimestampUtc = 1704068100000,
                    Open = 42100m,
                    High = 42200m,
                    Low = 42050m,
                    Close = 42150m,
                    Volume = 1200m,
                    IsWarmup = false,
                    EmaFast = 42050m,
                    EmaSlow = 42000m,
                    EmaTrend = 41890m,
                    Rsi = 61m,
                    Atr = 130m,
                    SetupDetected = true,
                    GridLifecycleState = "Active",
                    PositionSize = 0.02m,
                    PositionAvgEntry = 42050m,
                    SignalsEmitted = ["TakeProfit"],
                    GridCycleId = "cycle-2",
                },
            ]),
            orderEventLogJson: BacktestRunResponseMapper.SerializeOrderEventLog(
            [
                new OrderEventEntry
                {
                    TimestampUtc = 1704067200000,
                    EventType = OrderEventType.Placed,
                    OrderId = "order-1",
                    Side = "Buy",
                    OrderType = "Limit",
                    Price = 42000m,
                    Size = 0.01m,
                    GridCycleId = "cycle-1",
                },
                new OrderEventEntry
                {
                    TimestampUtc = 1704068100000,
                    EventType = OrderEventType.Cancelled,
                    OrderId = "order-2",
                    Side = "Sell",
                    OrderType = "Limit",
                    Price = 42150m,
                    Size = 0.01m,
                    CancellationReason = CancellationReason.TakeProfitTriggered,
                    GridCycleId = "cycle-2",
                },
            ]),
            gridCycleLogJson: BacktestRunResponseMapper.SerializeGridCycleLog(
            [
                new GridCycleEntry
                {
                    GridCycleId = "cycle-1",
                    DeployTimestampUtc = 1704067200000,
                    AnchorPrice = 42000m,
                    LevelsPlaced = 3,
                    LevelPrices = [42000m, 41900m, 41800m],
                    LevelsFilled = 1,
                    TakeProfitPrice = 42300m,
                    StopLossPrice = 41000m,
                    ExitReason = "TakeProfit",
                    CyclePnl = 45.5m,
                    CycleDurationMs = 900000,
                    CloseTimestampUtc = 1704068100000,
                },
                new GridCycleEntry
                {
                    GridCycleId = "cycle-2",
                    DeployTimestampUtc = 1704068100000,
                    AnchorPrice = 42100m,
                    LevelsPlaced = 2,
                    LevelPrices = [42100m, 42000m],
                    LevelsFilled = 0,
                    TakeProfitPrice = 42400m,
                    StopLossPrice = 41100m,
                    ExitReason = "Cancelled",
                    CyclePnl = 0m,
                    CycleDurationMs = 600000,
                    CloseTimestampUtc = 1704068700000,
                },
            ]));
    }

    private static BacktestRun CreateBacktestRunWithRTrackedTrades()
    {
        var trades = new decimal[] { 2.1m, -1.0m, 1.5m, -1.0m, 3.0m, -0.8m, 2.0m, -1.0m, 1.8m, -1.0m }
            .Select((rMultiple, index) => new BacktestTrade
            {
                TradeId = $"trade-{index + 1}",
                GridCycleId = "cycle-1",
                EntryTimeUtc = 1704067200000 + (index * 60_000L),
                EntryPrice = 50_000m,
                ExitTimeUtc = 1704067230000 + (index * 60_000L),
                ExitPrice = 50_000m + (rMultiple * 100m),
                Side = OrderSide.Buy,
                Size = 0.1m,
                PnL = rMultiple * 100m,
                Fees = 1m,
                TradeType = TradeType.GridFill,
                ExitReason = "TakeProfitTriggered",
                InitialRDollars = 100m,
                RMultipleResult = rMultiple,
                MFE = 3m,
                MAE = -0.5m,
            })
            .ToList();

        return BacktestRun.Create(
            symbol: "BTC",
            intervalsJson: "[\"15m\",\"1h\",\"4h\"]",
            startDateUtc: 1704067200000,
            endDateUtc: 1735689599000,
            strategyConfigJson: CreateTestStrategyConfigJson(),
            executionConfigJson: CreateTestExecutionConfigJson(),
            initialCapital: 10000m,
            candlesReplayed: 35040,
            elapsedMs: 12500,
            totalTrades: trades.Count,
            winningTrades: trades.Count(trade => trade.PnL > 0m),
            losingTrades: trades.Count(trade => trade.PnL < 0m),
            winRate: 50m,
            totalPnl: trades.Sum(trade => trade.PnL) ?? 0m,
            maxDrawdown: -1234.56m,
            averageTradePnl: trades.Average(trade => trade.PnL ?? 0m),
            averageHoldTimeMinutes: 1.0,
            hedgesOpened: 0,
            totalFeesPaid: trades.Sum(trade => trade.Fees),
            tradesJson: BacktestRunResponseMapper.SerializeTrades(trades));
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

    private static Strategy CreateStrategy()
    {
        return Strategy.Create(
            userId: "dev-user",
            name: "Saved BTC Grid",
            strategyType: "GridStrategy",
            configJson: JsonSerializer.Serialize(new StrategyConfig
            {
                SchemaVersion = 1,
                StrategyMode = StrategyMode.Grid,
                StrategyName = "Saved BTC Grid",
                Exchange = "Hyperliquid",
                Market = "BTC",
                Timeframe = "15m",
                Direction = Direction.Long,
                Enabled = true,
                Grid = new GridConfig
                {
                    Levels = 8,
                    EntryMode = EntryModes.AutoFromSignalCandle,
                    Spacing = 0.4m,
                    BreakdownThreshold = -2.5m,
                },
                Exit = new ExitConfig
                {
                    TakeProfit = new ExitRuleConfig
                    {
                        Enabled = true,
                        Type = ExitRuleType.FixedPercent,
                        Value = 1m,
                    },
                    StopLoss = new ExitRuleConfig
                    {
                        Enabled = true,
                        Type = ExitRuleType.FixedPercent,
                        Value = 5m,
                    },
                },
                Risk = new RiskConfig
                {
                    PositionSizeType = PositionSizeType.FixedNotional,
                    PositionSizeValue = 100m,
                    Leverage = 3m,
                    MaxOpenTrades = 1,
                    CooldownValue = 0,
                    CooldownUnit = CooldownUnit.Candles,
                },
                Source = new SourceMetadata
                {
                    EntryPoint = StrategyEntryPoint.UiBuilder,
                    Summary = "Backtest: Saved BTC Grid",
                },
            }, StrategyJsonOptions.Default));
    }

    private static string CreateTestStrategyConfigJson()
    {
        return JsonSerializer.Serialize(new StrategyConfig
        {
            SchemaVersion = 1,
            StrategyMode = StrategyMode.Grid,
            StrategyName = "BTC Grid",
            Exchange = "Hyperliquid",
            Market = "BTC",
            Timeframe = "15m",
            Direction = Direction.Long,
            Enabled = true,
            Grid = new GridConfig
            {
                Levels = 10,
                EntryMode = EntryModes.WaitForLimitPrice,
                AnchorPrice = 42000m,
                Spacing = 0.5m,
                BreakdownThreshold = -3m,
            },
            Exit = new ExitConfig
            {
                TakeProfit = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.FixedPercent,
                    Value = 1m,
                },
                StopLoss = new ExitRuleConfig
                {
                    Enabled = true,
                    Type = ExitRuleType.FixedPercent,
                    Value = 5m,
                },
            },
            Risk = new RiskConfig
            {
                PositionSizeType = PositionSizeType.FixedNotional,
                PositionSizeValue = 100m,
                Leverage = 3m,
                MaxOpenTrades = 1,
                CooldownValue = 0,
                CooldownUnit = CooldownUnit.Candles,
            },
            Source = new SourceMetadata
            {
                EntryPoint = StrategyEntryPoint.UiBuilder,
                Summary = "Backtest: BTC Grid",
            },
        }, StrategyJsonOptions.Default);
    }

    private static string CreateTestExecutionConfigJson()
    {
        return JsonSerializer.Serialize(new ExecutionConfig
        {
            FeeModel = new FeeModel
            {
                MakerFeeRate = 0.0001m,
                TakerFeeRate = 0.00035m,
                SlippageRate = 0m,
            },
        }, StrategyJsonOptions.Default);
    }
}