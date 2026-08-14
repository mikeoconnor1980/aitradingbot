using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using TradePilot.AI.Analyst;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketAnalysis.Models;
using TradePilot.Application.Backtesting.Experiments;
using TradePilot.Application.MarketAnalysis.Queries;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.MarketData.Queries;
using TradePilot.Application.StrategyEvaluations.Queries;
using TradePilot.Application.TradeJournal.Models;
using TradePilot.Application.TradeJournal.Queries;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Enums;

namespace TradePilot.AI.Tests.Analyst;

[TestClass]
public sealed class TradePilotAnalystToolCatalogTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private const string WalletAddress = "0xb63a3948477254cc17e0fb444050b9e161fccfa3";

    private Mock<ISender> _sender = null!;
    private Mock<IExchangeResolver> _exchangeResolver = null!;
    private Mock<IUserWalletAddressRepository> _walletRepository = null!;
    private Mock<IUserExchangeCredentialRepository> _credentialRepository = null!;
    private Mock<IStrategyRepository> _strategyRepository = null!;
    private Mock<IBacktestExperimentService> _backtestExperimentService = null!;
    private Strategy _strategy = null!;
    private TradePilotAnalystToolCatalog _sut = null!;

    [TestInitialize]
    public void Initialize()
    {
        _sender = new Mock<ISender>();
        _exchangeResolver = new Mock<IExchangeResolver>();
        _walletRepository = new Mock<IUserWalletAddressRepository>();
        _credentialRepository = new Mock<IUserExchangeCredentialRepository>();
        _strategyRepository = new Mock<IStrategyRepository>();
        _backtestExperimentService = new Mock<IBacktestExperimentService>();
        _strategy = Strategy.Create(UserId.ToString(), "v10.4", "signal", "{}");
        _exchangeResolver
            .Setup(resolver => resolver.GetCurrentExchangeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Exchange.Hyperliquid);
        _walletRepository
            .Setup(repository => repository.GetActiveByUserIdAndExchangeAsync(
                UserId,
                Exchange.Hyperliquid,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserWalletAddress.Create(UserId, WalletAddress));
        _strategyRepository
            .Setup(repository => repository.SearchIdsByNameAsync("v10.4", It.IsAny<CancellationToken>()))
            .ReturnsAsync([_strategy.Id]);
        _strategyRepository
            .Setup(repository => repository.GetByIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { _strategy.Id })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([_strategy]);
        _strategyRepository
            .Setup(repository => repository.GetByIdAsync(_strategy.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_strategy);

        _sut = new TradePilotAnalystToolCatalog(
            _sender.Object,
            _exchangeResolver.Object,
            _walletRepository.Object,
            _credentialRepository.Object,
            _strategyRepository.Object,
            _backtestExperimentService.Object,
            NullLogger<TradePilotAnalystToolCatalog>.Instance);
    }

    [TestMethod]
    public void GivenToolCatalogue_WhenInspectingDefinitions_ThenOnlyReadOnlyAllowListIsPresent()
    {
        _sut.Definitions.Select(definition => definition.Name).Should().Equal(
        [
            "get_market_snapshot",
            "analyse_market",
            "analyse_market_multi_timeframe",
            "get_account_summary",
            "get_positions",
            "get_open_orders",
            "get_recent_fills",
            "get_strategy_evaluations",
            "get_latest_strategy_evaluation",
            "get_strategy_evaluation_summary",
            "get_recent_trades",
            "get_trade",
            "get_trade_analytics",
            "get_strategy_trade_analytics",
            "run_backtest_experiment",
        ]);
        _sut.Definitions.Select(definition => definition.Name).Should().NotContain(
        ["place_order", "cancel_order", "close_position", "change_risk", "deploy_strategy", "withdraw", "transfer"]);
    }

    [TestMethod]
    public async Task GivenMarketTools_WhenExecuted_ThenExactApplicationQueriesAndStructuredResultsArePreserved()
    {
        var snapshot = new MarketInfoDto { Asset = "BTC", MarkPrice = 60_000.25m };
        var analysis = CreateAnalysis();
        var multiTimeframe = CreateMultiTimeframeAnalysis(analysis);
        GetMarketInfoQuery? snapshotQuery = null;
        AnalyseMarketQuery? analysisQuery = null;
        AnalyseMarketMultiTimeframeQuery? multiTimeframeQuery = null;
        using var cancellation = new CancellationTokenSource();

        _sender.Setup(sender => sender.Send(It.IsAny<GetMarketInfoQuery>(), cancellation.Token))
            .Callback<IRequest<MarketInfoDto>, CancellationToken>((request, _) => snapshotQuery = (GetMarketInfoQuery)request)
            .ReturnsAsync(snapshot);
        _sender.Setup(sender => sender.Send(It.IsAny<AnalyseMarketQuery>(), cancellation.Token))
            .Callback<IRequest<MarketAnalysisResult>, CancellationToken>((request, _) => analysisQuery = (AnalyseMarketQuery)request)
            .ReturnsAsync(analysis);
        _sender.Setup(sender => sender.Send(It.IsAny<AnalyseMarketMultiTimeframeQuery>(), cancellation.Token))
            .Callback<IRequest<MultiTimeframeMarketAnalysisResult>, CancellationToken>(
                (request, _) => multiTimeframeQuery = (AnalyseMarketMultiTimeframeQuery)request)
            .ReturnsAsync(multiTimeframe);

        var snapshotResult = await _sut.ExecuteAsync(
            "get_market_snapshot",
            "{\"symbol\":\"BTC\",\"exchange\":\"Binance\"}",
            new AnalystToolContext(null),
            cancellation.Token);
        var analysisResult = await _sut.ExecuteAsync(
            "analyse_market",
            "{\"symbol\":\"BTC\",\"timeframe\":\"4h\",\"exchange\":\"Binance\",\"cutoff\":\"2026-08-14T10:00:00Z\"}",
            new AnalystToolContext(null),
            cancellation.Token);
        var multiResult = await _sut.ExecuteAsync(
            "analyse_market_multi_timeframe",
            "{\"symbol\":\"BTC\",\"timeframes\":[\"15m\",\"1d\"]}",
            new AnalystToolContext(null),
            cancellation.Token);

        snapshotQuery.Should().Be(new GetMarketInfoQuery("BTC", Exchange.Binance));
        analysisQuery.Should().Be(new AnalyseMarketQuery(
            "BTC",
            "4h",
            Exchange.Binance,
            new DateTimeOffset(2026, 8, 14, 10, 0, 0, TimeSpan.Zero)));
        multiTimeframeQuery.Should().NotBeNull();
        multiTimeframeQuery!.Timeframes.Should().Equal("15m", "1d");
        snapshotResult.Result!.Value.GetProperty("markPrice").GetDecimal().Should().Be(60_000.25m);
        analysisResult.Result!.Value.GetProperty("trend").GetString().Should().Be("bullish");
        multiResult.Result!.Value.GetProperty("conflicts").GetProperty("hasTrendConflict").GetBoolean().Should().BeTrue();
    }

    [TestMethod]
    public async Task GivenAccountTools_WhenExecuted_ThenExactPhase1QueriesAreUsed()
    {
        _sender.Setup(sender => sender.Send(It.IsAny<GetAccountSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountSummaryDto { Equity = 10_000m });
        _sender.Setup(sender => sender.Send(It.IsAny<GetOpenPositionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PositionDto> { new() { Asset = "BTC", Side = "Long", Size = 1m } });
        _sender.Setup(sender => sender.Send(It.IsAny<GetOpenOrdersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OpenOrderDto> { new() { Asset = "BTC", OrderId = "order-1" } });
        _sender.Setup(sender => sender.Send(It.IsAny<GetRecentFillsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FillEventDto> { new() { Asset = "BTC", Price = 59_000m } });
        var context = new AnalystToolContext(UserId);

        var summary = await _sut.ExecuteAsync("get_account_summary", "{}", context, CancellationToken.None);
        var positions = await _sut.ExecuteAsync("get_positions", "{}", context, CancellationToken.None);
        var orders = await _sut.ExecuteAsync("get_open_orders", "{}", context, CancellationToken.None);
        var fills = await _sut.ExecuteAsync("get_recent_fills", "{\"symbol\":\"BTC\"}", context, CancellationToken.None);

        summary.Result!.Value.GetProperty("equity").GetDecimal().Should().Be(10_000m);
        positions.Result!.Value[0].GetProperty("asset").GetString().Should().Be("BTC");
        orders.Result!.Value[0].GetProperty("orderId").GetString().Should().Be("order-1");
        fills.Result!.Value[0].GetProperty("price").GetDecimal().Should().Be(59_000m);
        _sender.Verify(sender => sender.Send(
            It.Is<GetAccountSummaryQuery>(query => query.Exchange == Exchange.Hyperliquid && query.WalletAddress == WalletAddress),
            It.IsAny<CancellationToken>()), Times.Once);
        _sender.Verify(sender => sender.Send(
            It.Is<GetOpenPositionsQuery>(query => query.Exchange == Exchange.Hyperliquid && query.WalletAddress == WalletAddress),
            It.IsAny<CancellationToken>()), Times.Once);
        _sender.Verify(sender => sender.Send(
            It.Is<GetOpenOrdersQuery>(query => query.Exchange == Exchange.Hyperliquid && query.WalletAddress == WalletAddress),
            It.IsAny<CancellationToken>()), Times.Once);
        _sender.Verify(sender => sender.Send(
            It.Is<GetRecentFillsQuery>(query => query.Exchange == Exchange.Hyperliquid && query.WalletAddress == WalletAddress && query.Asset == "BTC"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GivenTradeAnalyticsTools_WhenExecuted_ThenApplicationCalculatesHistoryAndGroups()
    {
        var analytics = new TradeAnalytics(
            2, 1, 1, 0, 20m, 16m, 4m, null, false, 50m, 20m, -4m, 8m,
            5m, false, TimeSpan.FromHours(2), 30m, 10m, -12m, -4m, null, null);
        var grouped = new StrategyTradeAnalytics(
            [new TradeAnalyticsGroup("4", analytics)],
            [new TradeAnalyticsGroup("Bullish", analytics)]);
        _sender.Setup(sender => sender.Send(It.IsAny<GetTradeAnalyticsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(analytics);
        _sender.Setup(sender => sender.Send(It.IsAny<GetStrategyTradeAnalyticsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(grouped);

        var totals = await _sut.ExecuteAsync(
            "get_trade_analytics",
            "{\"symbol\":\"BTC\",\"outcome\":\"loser\"}",
            new AnalystToolContext(UserId),
            CancellationToken.None);
        var versions = await _sut.ExecuteAsync(
            "get_strategy_trade_analytics",
            "{\"strategyName\":\"v10.4\"}",
            new AnalystToolContext(UserId),
            CancellationToken.None);

        totals.Result!.Value.GetProperty("winRate").GetDecimal().Should().Be(50m);
        versions.Result!.Value.GetProperty("byStrategyVersion")[0].GetProperty("key").GetString().Should().Be("4");
        _sender.Verify(sender => sender.Send(
            It.Is<GetTradeAnalyticsQuery>(query =>
                query.UserId == UserId.ToString()
                && query.Symbol == "BTC"
                && query.Outcome == TradeOutcome.Loser),
            It.IsAny<CancellationToken>()), Times.Once);
        _sender.Verify(sender => sender.Send(
            It.Is<GetStrategyTradeAnalyticsQuery>(query =>
                query.UserId == UserId.ToString()
                && query.StrategyId == _strategy.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GivenUnknownOrExecutionTool_WhenExecuted_ThenNoApplicationCapabilityIsInvoked()
    {
        var unknown = await _sut.ExecuteAsync("not_registered", "{}", new AnalystToolContext(UserId), CancellationToken.None);
        var execution = await _sut.ExecuteAsync("place_order", "{}", new AnalystToolContext(UserId), CancellationToken.None);

        unknown.Error!.Code.Should().Be("unknown_tool");
        execution.Error!.Code.Should().Be("unknown_tool");
        _sender.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task GivenRsiExperimentTool_WhenExecuted_ThenBoundedStructuredRequestIsDelegatedWithoutLiveMutation()
    {
        _backtestExperimentService
            .Setup(service => service.RunAsync(It.IsAny<BacktestExperimentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BacktestExperimentResult(
                _strategy.Id, 1, "BTC", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-02-01T00:00:00Z"),
                10_000m, new BacktestExperimentMetrics(100m, 10m, 1m, 4, 50m, 2m, 25m, 1m, 10),
                [new BacktestCandidateExperimentResult("RSI 65", [new StrategyParameterOverride("rsi.value", "rsi-1", 65m)],
                    new BacktestExperimentMetrics(120m, 12m, 1.2m, 5, 60m, 2.5m, 24m, 1m, 10),
                    new BacktestComparison(20m, 2m, 0.2m, 1, 10m, 0.5m, -1m, 0m))]));

        var result = await _sut.ExecuteAsync(
            "run_backtest_experiment",
            "{\"strategyName\":\"v10.4\",\"symbol\":\"BTC\",\"start\":\"2026-01-01T00:00:00Z\",\"end\":\"2026-02-01T00:00:00Z\",\"initialCapital\":10000,\"candidates\":[{\"label\":\"RSI 65\",\"rsiConditionId\":\"rsi-1\",\"rsiValue\":65}]}",
            new AnalystToolContext(UserId),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Result!.Value.GetProperty("candidates")[0].GetProperty("comparison").GetProperty("totalPnlDelta").GetDecimal().Should().Be(20m);
        _backtestExperimentService.Verify(service => service.RunAsync(
            It.Is<BacktestExperimentRequest>(request =>
                request.StrategyId == _strategy.Id
                && request.UserId == UserId.ToString()
                && request.Candidates.Single().ConfigurationOverrides.Single().Value == 65m),
            It.IsAny<CancellationToken>()), Times.Once);
        _sender.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task GivenInvalidArgumentsOrApplicationFailure_WhenExecuted_ThenSafeStructuredErrorsAreReturned()
    {
        _sender.Setup(sender => sender.Send(It.IsAny<AnalyseMarketQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("private-key=do-not-expose"));

        var invalid = await _sut.ExecuteAsync(
            "analyse_market",
            "{not-json",
            new AnalystToolContext(null),
            CancellationToken.None);
        var failure = await _sut.ExecuteAsync(
            "analyse_market",
            "{\"symbol\":\"BTC\",\"timeframe\":\"4h\"}",
            new AnalystToolContext(null),
            CancellationToken.None);

        invalid.Error!.Code.Should().Be("invalid_arguments");
        failure.Error!.Code.Should().Be("tool_failure");
        failure.Error.Message.Should().NotContain("private-key");
    }

    [TestMethod]
    public async Task GivenCancellation_WhenToolRuns_ThenTokenIsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _sender.Setup(sender => sender.Send(It.IsAny<AnalyseMarketQuery>(), cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));

        var action = () => _sut.ExecuteAsync(
            "analyse_market",
            "{\"symbol\":\"BTC\",\"timeframe\":\"4h\"}",
            new AnalystToolContext(null),
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _sender.Verify(sender => sender.Send(It.IsAny<AnalyseMarketQuery>(), cancellation.Token), Times.Once);
    }

    [TestMethod]
    public async Task GivenLatestStrategyEvaluationTool_WhenExecuted_ThenExactApplicationCapabilityAndEvidenceArePreserved()
    {
        var evaluation = CreateStrategyEvaluation();
        _sender
            .Setup(sender => sender.Send(
                It.Is<GetLatestStrategyEvaluationQuery>(query =>
                    query.StrategyId == _strategy.Id
                    && query.StrategyName == null
                    && query.StrategyVersion == 4
                    && query.Symbol == "BTC"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(evaluation);

        var result = await _sut.ExecuteAsync(
            "get_latest_strategy_evaluation",
            "{\"strategyName\":\"v10.4\",\"strategyVersion\":4,\"symbol\":\"BTC\"}",
            new AnalystToolContext(UserId),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Result!.Value.GetProperty("decision").GetString().Should().Be("no_trade");
        result.Result.Value.GetProperty("rules")[0].GetProperty("ruleId").GetString().Should().Be("entry.rsi.max");
        result.Result.Value.GetProperty("rules")[0].GetProperty("actualNumericValue").GetDecimal().Should().Be(67.3m);
    }

    [TestMethod]
    public async Task GivenNoRecordedEvaluation_WhenLatestToolExecuted_ThenMissingEvidenceErrorIsReturned()
    {
        _sender
            .Setup(sender => sender.Send(It.IsAny<GetLatestStrategyEvaluationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StrategyEvaluation?)null);

        var result = await _sut.ExecuteAsync(
            "get_latest_strategy_evaluation",
            "{\"strategyName\":\"v10.4\",\"symbol\":\"BTC\"}",
            new AnalystToolContext(UserId),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error!.Code.Should().Be("no_evaluation_evidence");
        result.Error.Message.Should().Contain("No recorded strategy evaluation");
    }

    private static MarketAnalysisResult CreateAnalysis()
    {
        return new MarketAnalysisResult(
            "BTC",
            "4h",
            new DateTimeOffset(2026, 8, 14, 8, 0, 0, TimeSpan.Zero),
            60_000.25m,
            new MarketIndicatorValues(59_000m, 58_000m, 55_000m, 52m, 1_200m, 2m, 1m, 2m, 9m),
            MarketTrend.Bullish,
            MarketMomentum.Neutral,
            VolatilityRegime.Normal,
            MarketStructure.HigherHighHigherLow,
            61_000m,
            null);
    }

    private static MultiTimeframeMarketAnalysisResult CreateMultiTimeframeAnalysis(MarketAnalysisResult analysis)
    {
        return new MultiTimeframeMarketAnalysisResult(
            "BTC",
            analysis.Timestamp,
            [new TimeframeMarketAnalysis("15m", analysis), new TimeframeMarketAnalysis("1d", analysis)],
            "1d",
            "15m",
            MarketTrend.Bullish,
            MarketTrend.Bearish,
            DirectionalAlignment.Mixed,
            DirectionalAlignment.AlignedNeutral,
            StructureAlignment.AlignedHigherHighHigherLow,
            VolatilityAlignment.AlignedNormal,
            1, 1, 0, 0, 0, 2,
            2, 0, 0, 0, 0, 0,
            2, 0,
            new MultiTimeframeMarketAnalysisConflicts(
                true,
                true,
                [new TimeframeClassificationConflict<MarketTrend>("15m", MarketTrend.Bearish, "1d", MarketTrend.Bullish)],
                [],
                [],
                []));
    }

    private static StrategyEvaluation CreateStrategyEvaluation()
    {
        return StrategyEvaluation.Create(
            Guid.NewGuid(),
            "v10.4",
            4,
            new string('a', 64),
            "BTC",
            "15m",
            1_000,
            StrategyDecision.NoTrade,
            false,
            "RSI 67.3 exceeded 62.",
            1_000,
            60_000m,
            "Normal",
            null,
            null,
            false,
            [
                RuleEvaluation.Create(
                    0,
                    "entry.rsi.max",
                    "Maximum RSI",
                    RuleCategory.Momentum,
                    false,
                    "RSI 67.3 exceeded 62.",
                    true,
                    RuleEvaluationKind.Blocking,
                    "67.3",
                    67.3m,
                    "<= 62",
                    62m)
            ]);
    }
}
