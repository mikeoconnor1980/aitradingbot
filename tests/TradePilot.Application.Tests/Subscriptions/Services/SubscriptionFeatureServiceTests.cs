using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Subscriptions.Services;

namespace TradePilot.Application.Tests.Subscriptions.Services;

[TestClass]
public sealed class SubscriptionFeatureServiceTests
{
    private SubscriptionFeatureService _sut = default!;

    [TestInitialize]
    public void SetUp()
    {
        _sut = new SubscriptionFeatureService(Mock.Of<ISubscriptionRepository>());
    }

    [DataTestMethod]
    [DataRow("BTC")]
    [DataRow("BTC-USD")]
    [DataRow("BTC/USD")]
    [DataRow("BTC-PERP")]
    [DataRow("BTCUSDT")]
    [DataRow("BTCUSD")]
    [DataRow("BTCUSD.P")]
    public void GivenBtcMarketFormats_WhenAssetAllowedChecked_ThenMatchesAllowedAsset(string market)
    {
        var result = _sut.IsAssetAllowed(["BTC", "ETH"], market);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void GivenUnsupportedMarket_WhenAssetAllowedChecked_ThenReturnsFalse()
    {
        var result = _sut.IsAssetAllowed(["BTC", "ETH"], "SOLUSDT");

        result.Should().BeFalse();
    }
}