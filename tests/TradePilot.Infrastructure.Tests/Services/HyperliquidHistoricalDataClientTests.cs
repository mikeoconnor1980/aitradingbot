using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.FundingRates.Models;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.ValueObjects;
using TradePilot.Infrastructure.Hyperliquid;

namespace TradePilot.Infrastructure.Tests.Services;

[TestClass]
public sealed class HyperliquidHistoricalDataClientTests
{
    private Mock<IHyperliquidRestClient> _restClientMock = null!;
    private HyperliquidHistoricalDataClient _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _restClientMock = new Mock<IHyperliquidRestClient>();
        _sut = new HyperliquidHistoricalDataClient(_restClientMock.Object);
    }

    [TestMethod]
    public async Task GivenCandleSnapshots_WhenGetCandleSnapshotsAsync_ThenReturnsRestClientResults()
    {
        var pair = TradingPair.Create("BTC", "USD", AssetType.Perp);
        IReadOnlyList<CandleSnapshotDto> expected =
        [
            new CandleSnapshotDto { Timestamp = 1700000000000L, Open = 1m, High = 2m, Low = 0.5m, Close = 1.5m, Volume = 10m, NumTrades = 5 },
            new CandleSnapshotDto { Timestamp = 1700000060000L, Open = 1.5m, High = 2.5m, Low = 1m, Close = 2m, Volume = 12m, NumTrades = 6 },
        ];

        _restClientMock
            .Setup(client => client.GetCandleSnapshotsAsync(pair.Base, "1m", 10L, 20L, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected.ToList());

        var result = await _sut.GetCandleSnapshotsAsync(pair, "1m", 10L, 20L);

        result.Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    public async Task GivenRestClientFailure_WhenGetCandleSnapshotsAsync_ThenPropagatesOriginalException()
    {
        var pair = TradingPair.Create("BTC", "USD", AssetType.Perp);

        _restClientMock
            .Setup(client => client.GetCandleSnapshotsAsync(pair.Base, "1m", 10L, 20L, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        Func<Task> act = () => _sut.GetCandleSnapshotsAsync(pair, "1m", 10L, 20L);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("boom");
    }

    [TestMethod]
    public async Task GivenFundingRateRequest_WhenGetFundingRatesAsync_ThenReturnsEmptyList()
    {
        var pair = TradingPair.Create("BTC", "USD", AssetType.Perp);

        var result = await _sut.GetFundingRatesAsync(pair, 10L, 20L);

        result.Should().BeEquivalentTo(Array.Empty<FundingRateDto>());
    }
}