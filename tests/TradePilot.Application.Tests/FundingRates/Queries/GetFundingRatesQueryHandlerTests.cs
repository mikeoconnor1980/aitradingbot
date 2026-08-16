using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.FundingRates.Models;
using TradePilot.Application.FundingRates.Queries;

namespace TradePilot.Application.Tests.FundingRates.Queries;

[TestClass]
public sealed class GetFundingRatesQueryHandlerTests
{
    [TestMethod]
    public async Task GivenMappedAsset_WhenHandle_ThenReturnsFundingInTimestampOrder()
    {
        using var cancellationSource = new CancellationTokenSource();
        var pair = TradingPair.Create("BTC", "USD", AssetType.Perp);
        var client = new Mock<IExchangeHistoricalDataClient>();
        client.SetupGet(candidate => candidate.Exchange).Returns(Exchange.Binance);
        client.Setup(candidate => candidate.GetFundingRatesAsync(
                pair,
                1_000,
                3_000,
                cancellationSource.Token))
            .ReturnsAsync([
                new FundingRateDto { FundingTime = 3_000, Rate = 0.0002m, MarkPrice = 102m },
                new FundingRateDto { FundingTime = 1_000, Rate = 0.0001m, MarkPrice = 100m },
            ]);
        var mapper = new Mock<IExchangeSymbolMapper>();
        mapper.SetupGet(candidate => candidate.Exchange).Returns(Exchange.Binance);
        mapper.Setup(candidate => candidate.FromExchangeSymbol("BTCUSDT")).Returns(pair);
        var sut = new GetFundingRatesQueryHandler([client.Object], [mapper.Object]);

        var result = await sut.Handle(
            new GetFundingRatesQuery("BTCUSDT", 1_000, 3_000, Exchange.Binance),
            cancellationSource.Token);

        result.Select(rate => rate.FundingTime).Should().ContainInOrder(1_000, 3_000);
        client.Verify(candidate => candidate.GetFundingRatesAsync(
            pair,
            1_000,
            3_000,
            cancellationSource.Token), Times.Once);
    }

    [TestMethod]
    public async Task GivenNoFundingObservations_WhenHandle_ThenReturnsEmptySet()
    {
        var pair = TradingPair.Create("BTC", "USD", AssetType.Perp);
        var client = new Mock<IExchangeHistoricalDataClient>();
        client.SetupGet(candidate => candidate.Exchange).Returns(Exchange.Hyperliquid);
        client.Setup(candidate => candidate.GetFundingRatesAsync(
                pair,
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var mapper = new Mock<IExchangeSymbolMapper>();
        mapper.SetupGet(candidate => candidate.Exchange).Returns(Exchange.Hyperliquid);
        mapper.Setup(candidate => candidate.FromExchangeSymbol("BTC-PERP")).Returns(pair);
        var sut = new GetFundingRatesQueryHandler([client.Object], [mapper.Object]);

        var result = await sut.Handle(
            new GetFundingRatesQuery("BTC-PERP", 1_000, 1_000),
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GivenEndBeforeStart_WhenHandle_ThenRejectsRangeBeforeCallingExchange()
    {
        var client = new Mock<IExchangeHistoricalDataClient>();
        var mapper = new Mock<IExchangeSymbolMapper>();
        var sut = new GetFundingRatesQueryHandler([client.Object], [mapper.Object]);

        var action = () => sut.Handle(
            new GetFundingRatesQuery("BTC-PERP", 2_000, 1_000),
            CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("EndTime must be greater than or equal to StartTime.");
        client.Verify(
            candidate => candidate.GetFundingRatesAsync(
                It.IsAny<TradingPair>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
