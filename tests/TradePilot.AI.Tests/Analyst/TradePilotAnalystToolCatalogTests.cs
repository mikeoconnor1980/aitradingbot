using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using TradePilot.AI.Analyst;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketAnalysis.Models;
using TradePilot.Application.MarketAnalysis.Queries;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.MarketData.Queries;
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
    private TradePilotAnalystToolCatalog _sut = null!;

    [TestInitialize]
    public void Initialize()
    {
        _sender = new Mock<ISender>();
        _exchangeResolver = new Mock<IExchangeResolver>();
        _walletRepository = new Mock<IUserWalletAddressRepository>();
        _credentialRepository = new Mock<IUserExchangeCredentialRepository>();
        _exchangeResolver
            .Setup(resolver => resolver.GetCurrentExchangeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Exchange.Hyperliquid);
        _walletRepository
            .Setup(repository => repository.GetActiveByUserIdAndExchangeAsync(
                UserId,
                Exchange.Hyperliquid,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserWalletAddress.Create(UserId, WalletAddress));

        _sut = new TradePilotAnalystToolCatalog(
            _sender.Object,
            _exchangeResolver.Object,
            _walletRepository.Object,
            _credentialRepository.Object,
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
    public async Task GivenUnknownOrExecutionTool_WhenExecuted_ThenNoApplicationCapabilityIsInvoked()
    {
        var unknown = await _sut.ExecuteAsync("not_registered", "{}", new AnalystToolContext(UserId), CancellationToken.None);
        var execution = await _sut.ExecuteAsync("place_order", "{}", new AnalystToolContext(UserId), CancellationToken.None);

        unknown.Error!.Code.Should().Be("unknown_tool");
        execution.Error!.Code.Should().Be("unknown_tool");
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
}
