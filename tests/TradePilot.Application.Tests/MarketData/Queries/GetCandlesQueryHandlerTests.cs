using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.MarketData.Queries;

namespace TradePilot.Application.Tests.MarketData.Queries;

[TestClass]
public sealed class GetCandlesQueryHandlerTests
{
    [TestMethod]
    public async Task GivenMappedAsset_WhenHandle_ThenRequestsSnapshotsThroughExchangeClient()
    {
        var historicalDataClient = new Mock<IExchangeHistoricalDataClient>();
        historicalDataClient.SetupGet(client => client.Exchange).Returns(Exchange.Hyperliquid);
        historicalDataClient.Setup(client => client.GetCandleSnapshotsAsync(
                It.Is<TradingPair>(pair => pair.Canonical == "BTC/USD:PERP"),
                "15m",
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new CandleSnapshotDto { Timestamp = 2, Open = 2m, High = 3m, Low = 1m, Close = 2.5m, Volume = 20m },
                new CandleSnapshotDto { Timestamp = 1, Open = 1m, High = 2m, Low = 0.5m, Close = 1.5m, Volume = 10m },
            ]);

        var mapper = new Mock<IExchangeSymbolMapper>();
        mapper.SetupGet(m => m.Exchange).Returns(Exchange.Hyperliquid);
        mapper.Setup(m => m.FromExchangeSymbol("BTC-PERP"))
            .Returns(TradingPair.Create("BTC", "USD", AssetType.Perp));

        var sut = new GetCandlesQueryHandler([historicalDataClient.Object], [mapper.Object]);

        var result = await sut.Handle(new GetCandlesQuery("BTC-PERP", "15m"), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Timestamp.Should().Be(2);
        result[1].Timestamp.Should().Be(1);
    }

    [TestMethod]
    public async Task GivenUnsupportedTimeframe_WhenHandle_ThenThrowsDomainException()
    {
        var historicalDataClient = new Mock<IExchangeHistoricalDataClient>();
        historicalDataClient.SetupGet(client => client.Exchange).Returns(Exchange.Hyperliquid);

        var mapper = new Mock<IExchangeSymbolMapper>();
        mapper.SetupGet(m => m.Exchange).Returns(Exchange.Hyperliquid);
        mapper.Setup(m => m.FromExchangeSymbol("BTC-PERP"))
            .Returns(TradingPair.Create("BTC", "USD", AssetType.Perp));

        var sut = new GetCandlesQueryHandler([historicalDataClient.Object], [mapper.Object]);

        var act = () => sut.Handle(new GetCandlesQuery("BTC-PERP", "2m"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("Invalid timeframe '2m'.*");
    }
}