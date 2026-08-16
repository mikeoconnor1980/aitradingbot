using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using TradePilot.Api.Mcp;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketAnalysis.Models;
using TradePilot.Application.MarketAnalysis.Queries;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.MarketData.Queries;
using TradePilot.Domain.Entities;

namespace TradePilot.Api.Tests.Mcp;

[TestClass]
public sealed class TradePilotMcpToolsTests
{
    private static readonly DateTimeOffset Cutoff = new(2026, 8, 14, 10, 0, 0, TimeSpan.Zero);

    private Mock<ISender> _sender = null!;
    private Mock<IExchangeResolver> _exchangeResolver = null!;
    private Mock<IUserWalletAddressRepository> _walletRepository = null!;
    private Mock<IUserExchangeCredentialRepository> _credentialRepository = null!;
    private TradePilotMcpTools _sut = null!;

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

        _sut = new TradePilotMcpTools(
            _sender.Object,
            _exchangeResolver.Object,
            _walletRepository.Object,
            _credentialRepository.Object,
            NullLogger<TradePilotMcpTools>.Instance);
    }

    [TestMethod]
    public void GivenRegisteredToolType_WhenInspectingToolNames_ThenOnlyReadOnlyAllowListIsPresent()
    {
        var names = typeof(TradePilotMcpTools)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        names.Should().BeEquivalentTo(
        [
            "get_market_snapshot",
            "analyse_market",
            "analyse_market_multi_timeframe",
            "get_account_summary",
            "get_positions",
            "get_open_orders",
            "get_recent_fills",
        ]);

        names.Should().NotContain(
        [
            "place_order",
            "cancel_order",
            "close_position",
            "withdraw",
            "transfer",
            "deploy_strategy",
            "change_risk",
        ]);

        typeof(TradePilotMcpTools)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
            .Where(attribute => attribute is not null)
            .Should()
            .OnlyContain(attribute => attribute!.ReadOnly && !attribute.Destructive);
    }

    [TestMethod]
    public async Task GivenPhase2Inputs_WhenAnalyseMarket_ThenExactQueryAndResultArePreserved()
    {
        var expected = CreateAnalysis("BTC", "4h", MarketTrend.Bullish);
        AnalyseMarketQuery? captured = null;
        using var cancellation = new CancellationTokenSource();
        _sender
            .Setup(sender => sender.Send(It.IsAny<AnalyseMarketQuery>(), cancellation.Token))
            .Callback<IRequest<MarketAnalysisResult>, CancellationToken>((request, _) => captured = (AnalyseMarketQuery)request)
            .ReturnsAsync(expected);

        var result = await _sut.AnalyseMarketAsync(
            "BTC",
            "4h",
            Exchange.Binance,
            Cutoff,
            cancellation.Token);

        result.Should().BeSameAs(expected);
        captured.Should().Be(new AnalyseMarketQuery("BTC", "4h", Exchange.Binance, Cutoff));
    }

    [TestMethod]
    public async Task GivenPhase3Inputs_WhenAnalyseMultiTimeframe_ThenExactQueryAndEvidenceArePreserved()
    {
        string[] timeframes = ["15m", "1h", "4h", "1d"];
        var expected = CreateMultiTimeframeAnalysis();
        AnalyseMarketMultiTimeframeQuery? captured = null;
        _sender
            .Setup(sender => sender.Send(It.IsAny<AnalyseMarketMultiTimeframeQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<MultiTimeframeMarketAnalysisResult>, CancellationToken>(
                (request, _) => captured = (AnalyseMarketMultiTimeframeQuery)request)
            .ReturnsAsync(expected);

        var result = await _sut.AnalyseMarketMultiTimeframeAsync(
            "BTC",
            timeframes,
            Exchange.Hyperliquid,
            Cutoff);

        result.Should().BeSameAs(expected);
        result.Timeframes[0].Analysis.Should().BeSameAs(expected.Timeframes[0].Analysis);
        captured.Should().NotBeNull();
        captured!.Symbol.Should().Be("BTC");
        captured.Timeframes.Should().BeSameAs(timeframes);
        captured.Exchange.Should().Be(Exchange.Hyperliquid);
        captured.AsOf.Should().Be(Cutoff);
    }

    [TestMethod]
    public async Task GivenAuthenticatedWallet_WhenGetAccountSummary_ThenPhase1QueryAndResultArePreserved()
    {
        var userId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        const string walletAddress = "0xb63a3948477254cc17e0fb444050b9e161fccfa3";
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            "test"));
        var expected = new AccountSummaryDto { Equity = 10_000m, AvailableMargin = 8_000m };
        GetAccountSummaryQuery? captured = null;
        _walletRepository
            .Setup(repository => repository.GetActiveByUserIdAndExchangeAsync(
                userId,
                Exchange.Hyperliquid,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserWalletAddress.Create(userId, walletAddress));
        _sender
            .Setup(sender => sender.Send(It.IsAny<GetAccountSummaryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<AccountSummaryDto>, CancellationToken>(
                (request, _) => captured = (GetAccountSummaryQuery)request)
            .ReturnsAsync(expected);

        var result = await _sut.GetAccountSummaryAsync(user, Exchange.Hyperliquid);

        result.Should().BeSameAs(expected);
        captured.Should().Be(new GetAccountSummaryQuery(Exchange.Hyperliquid, walletAddress));
    }

    [TestMethod]
    public async Task GivenDuplicateOrTooFewTimeframes_WhenAnalyseMultiTimeframe_ThenInputIsDelegatedUnchanged()
    {
        string[] timeframes = ["1H", "1h"];
        _sender
            .Setup(sender => sender.Send(
                It.Is<AnalyseMarketMultiTimeframeQuery>(query => ReferenceEquals(query.Timeframes, timeframes)),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("At least two distinct timeframes are required for multi-timeframe analysis."));

        var action = () => _sut.AnalyseMarketMultiTimeframeAsync("BTC", timeframes);

        var exception = await action.Should().ThrowAsync<McpException>();
        exception.Which.Message.Should().Contain("At least two distinct timeframes");
        _sender.VerifyAll();
    }

    [TestMethod]
    public async Task GivenUnsupportedTimeframe_WhenAnalyseMarket_ThenApplicationValidationIsPreserved()
    {
        _sender
            .Setup(sender => sender.Send(
                It.Is<AnalyseMarketQuery>(query => query.Timeframe == "6h"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException(
                "Invalid timeframe '6h'. Supported: 1m, 3m, 5m, 15m, 30m, 1h, 2h, 4h, 8h, 12h, 1d, 1w, 1M"));

        var action = () => _sut.AnalyseMarketAsync("BTC", "6h");

        var exception = await action.Should().ThrowAsync<McpException>();
        exception.Which.Message.Should().Contain("Invalid timeframe '6h'");
        _sender.VerifyAll();
    }

    [TestMethod]
    public async Task GivenMissingSymbol_WhenAnalyseMarket_ThenInvalidParamsIsReturnedWithoutDelegation()
    {
        var action = () => _sut.AnalyseMarketAsync(" ", "4h");

        await action.Should().ThrowAsync<McpProtocolException>();
        _sender.Verify(
            sender => sender.Send(It.IsAny<AnalyseMarketQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GivenUnexpectedApplicationFailure_WhenAnalyseMarket_ThenInternalDetailsAreNotExposed()
    {
        _sender
            .Setup(sender => sender.Send(It.IsAny<AnalyseMarketQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("private-key=do-not-expose"));

        var action = () => _sut.AnalyseMarketAsync("BTC", "4h");

        var exception = await action.Should().ThrowAsync<McpException>();
        exception.Which.Message.Should().Be("TradePilot could not complete the requested capability.");
        exception.Which.Message.Should().NotContain("private-key");
    }

    [TestMethod]
    public async Task GivenCancellation_WhenAnalyseMarket_ThenRequestTokenIsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _sender
            .Setup(sender => sender.Send(It.IsAny<AnalyseMarketQuery>(), cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));

        var action = () => _sut.AnalyseMarketAsync(
            "BTC",
            "4h",
            cancellationToken: cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _sender.Verify(
            sender => sender.Send(It.IsAny<AnalyseMarketQuery>(), cancellation.Token),
            Times.Once);
    }

    [TestMethod]
    public void GivenAnalysisResult_WhenSerializedForMcp_ThenEnumsDatesDecimalsAndNullsAreStable()
    {
        var result = CreateAnalysis("BTC", "4h", MarketTrend.Bullish) with
        {
            RecentSwingLow = null,
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result, TradePilotMcpJson.CreateOptions()));
        var root = document.RootElement;

        root.GetProperty("trend").GetString().Should().Be("bullish");
        root.GetProperty("momentum").GetString().Should().Be("neutral");
        root.GetProperty("marketStructure").GetString().Should().Be("higher_high_higher_low");
        root.GetProperty("timestamp").GetString().Should().Be("2026-08-14T08:00:00+00:00");
        root.GetProperty("price").GetDecimal().Should().Be(60_000.25m);
        root.GetProperty("recentSwingLow").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private static MarketAnalysisResult CreateAnalysis(string symbol, string timeframe, MarketTrend trend)
    {
        return new MarketAnalysisResult(
            symbol,
            timeframe,
            new DateTimeOffset(2026, 8, 14, 8, 0, 0, TimeSpan.Zero),
            60_000.25m,
            new MarketIndicatorValues(59_000m, 58_000m, 55_000m, 52m, 1_200m, 2m, 1m, 2m, 9m),
            trend,
            MarketMomentum.Neutral,
            VolatilityRegime.Normal,
            MarketStructure.HigherHighHigherLow,
            61_000m,
            57_000m);
    }

    private static MultiTimeframeMarketAnalysisResult CreateMultiTimeframeAnalysis()
    {
        var shortTerm = CreateAnalysis("BTC", "15m", MarketTrend.Bearish);
        var primary = CreateAnalysis("BTC", "1d", MarketTrend.Bullish);

        return new MultiTimeframeMarketAnalysisResult(
            "BTC",
            Cutoff,
            [new TimeframeMarketAnalysis("15m", shortTerm), new TimeframeMarketAnalysis("1d", primary)],
            "1d",
            "15m",
            MarketTrend.Bullish,
            MarketTrend.Bearish,
            DirectionalAlignment.Mixed,
            DirectionalAlignment.AlignedNeutral,
            StructureAlignment.AlignedHigherHighHigherLow,
            VolatilityAlignment.AlignedNormal,
            1,
            1,
            0,
            0,
            0,
            2,
            2,
            0,
            0,
            0,
            0,
            0,
            2,
            0,
            new MultiTimeframeMarketAnalysisConflicts(
                true,
                true,
                [new TimeframeClassificationConflict<MarketTrend>("15m", MarketTrend.Bearish, "1d", MarketTrend.Bullish)],
                [],
                [],
                []));
    }
}
