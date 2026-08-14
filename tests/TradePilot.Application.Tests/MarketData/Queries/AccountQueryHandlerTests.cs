using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.MarketData.Queries;

namespace TradePilot.Application.Tests.MarketData.Queries;

[TestClass]
public sealed class AccountQueryHandlerTests
{
    [TestMethod]
    public async Task GivenExchangeAccount_WhenGetAccountSummary_ThenDelegatesWalletAndCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        var expected = new AccountSummaryDto
        {
            Equity = 12_500m,
            AvailableMargin = 9_000m,
            CrossMarginRatio = 0.08m,
            MaintenanceMargin = 1_000m,
            UnrealisedPnl = 250m,
        };
        var client = CreateAccountClient(Exchange.Hyperliquid);
        client.Setup(candidate => candidate.GetAccountSummaryAsync("wallet", cancellationSource.Token))
            .ReturnsAsync(expected);
        var sut = new GetAccountSummaryQueryHandler([client.Object]);

        var result = await sut.Handle(
            new GetAccountSummaryQuery(Exchange.Hyperliquid, "wallet"),
            cancellationSource.Token);

        result.Should().BeSameAs(expected);
        client.Verify(
            candidate => candidate.GetAccountSummaryAsync("wallet", cancellationSource.Token),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenNoOpenPositions_WhenGetOpenPositions_ThenReturnsEmptySet()
    {
        var client = CreateAccountClient(Exchange.Binance);
        client.Setup(candidate => candidate.GetPositionsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var sut = new GetOpenPositionsQueryHandler([client.Object]);

        var result = await sut.Handle(new GetOpenPositionsQuery(Exchange.Binance), CancellationToken.None);

        result.Should().BeEmpty();
        client.Verify(
            candidate => candidate.GetPositionsAsync(null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenNoOpenOrders_WhenGetOpenOrders_ThenReturnsEmptySet()
    {
        var client = CreateAccountClient(Exchange.Hyperliquid);
        client.Setup(candidate => candidate.GetOpenOrdersAsync("wallet", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var sut = new GetOpenOrdersQueryHandler([client.Object]);

        var result = await sut.Handle(
            new GetOpenOrdersQuery(Exchange.Hyperliquid, "wallet"),
            CancellationToken.None);

        result.Should().BeEmpty();
        client.Verify(
            candidate => candidate.GetOpenOrdersAsync("wallet", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenAssetFilter_WhenGetRecentFills_ThenMapsAssetBeforeDelegating()
    {
        var pair = TradingPair.Create("BTC", "USD", AssetType.Perp);
        var client = CreateAccountClient(Exchange.Hyperliquid);
        client.Setup(candidate => candidate.GetRecentFillsAsync(pair, "wallet", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var mapper = new Mock<IExchangeSymbolMapper>();
        mapper.SetupGet(candidate => candidate.Exchange).Returns(Exchange.Hyperliquid);
        mapper.Setup(candidate => candidate.FromExchangeSymbol("BTC-PERP")).Returns(pair);
        var sut = new GetRecentFillsQueryHandler([client.Object], [mapper.Object]);

        var result = await sut.Handle(
            new GetRecentFillsQuery(Exchange.Hyperliquid, "BTC-PERP", "wallet"),
            CancellationToken.None);

        result.Should().BeEmpty();
        mapper.Verify(candidate => candidate.FromExchangeSymbol("BTC-PERP"), Times.Once);
        client.Verify(
            candidate => candidate.GetRecentFillsAsync(pair, "wallet", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GivenNoAssetFilter_WhenGetRecentFills_ThenDoesNotRequireSymbolMapper()
    {
        var expected = new List<FillEventDto>
        {
            new() { Asset = "ETH", Price = 3_500m, Size = 0.25m },
        };
        var client = CreateAccountClient(Exchange.Binance);
        client.Setup(candidate => candidate.GetRecentFillsAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var sut = new GetRecentFillsQueryHandler([client.Object], []);

        var result = await sut.Handle(new GetRecentFillsQuery(Exchange.Binance), CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    private static Mock<IExchangeAccountClient> CreateAccountClient(Exchange exchange)
    {
        var client = new Mock<IExchangeAccountClient>();
        client.SetupGet(candidate => candidate.Exchange).Returns(exchange);
        return client;
    }
}
