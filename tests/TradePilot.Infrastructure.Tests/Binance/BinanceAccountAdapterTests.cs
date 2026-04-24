using System.Collections.Concurrent;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Domain.ValueObjects;
using TradePilot.Infrastructure.Binance;

namespace TradePilot.Infrastructure.Tests.Binance;

[TestClass]
public sealed class BinanceAccountAdapterTests
{
    private Mock<IBinanceFuturesAuthClient> _authClientMock = null!;
    private BinanceAccountAdapter _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _authClientMock = new Mock<IBinanceFuturesAuthClient>(MockBehavior.Strict);
        _sut = new BinanceAccountAdapter(_authClientMock.Object);
    }

    [TestMethod]
    public void GivenBinanceCapabilities_WhenReadingSupportedAssets_ThenMatchesMapperSingleSourceOfTruth()
    {
        var capabilities = new BinanceCapabilities();

        capabilities.SupportedAssets.Should().BeEquivalentTo(BinanceAssetMapper.SupportedAssets);
        capabilities.SupportedAssets.Should().HaveCount(8);
        capabilities.Supports(TradingPair.Create("SOL", "USD", AssetType.Perp)).Should().BeTrue();
        capabilities.Supports(TradingPair.Create("XRP", "USD", AssetType.Perp)).Should().BeFalse();
    }

    [TestMethod]
    public async Task GivenFallbackAccountFields_WhenGetAccountSummaryAsync_ThenParsesScientificNotationAndFallbackValues()
    {
        _authClientMock
            .Setup(client => client.GetAccountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BinanceAccountSnapshot
            {
                TotalCrossWalletBalance = "0",
                TotalWalletBalance = "1.25E+3",
                TotalCrossUnrealizedPnl = "0",
                TotalUnrealizedProfit = "1.25E+1",
                AvailableBalance = "0",
                TotalMaintenanceMargin = "25",
            });

        _authClientMock
            .Setup(client => client.GetBalancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new BinanceBalanceSnapshot
                {
                    Asset = "USDT",
                    AvailableBalance = "800.25",
                    CrossWalletBalance = "0",
                    CrossUnrealizedPnl = "0",
                },
            ]);

        var result = await _sut.GetAccountSummaryAsync(cancellationToken: CancellationToken.None);

        result.Equity.Should().Be(1262.5m);
        result.UnrealisedPnl.Should().Be(12.5m);
        result.AvailableMargin.Should().Be(800.25m);
        result.MaintenanceMargin.Should().Be(25m);
        result.CrossMarginRatio.Should().BeApproximately(25m / 1262.5m, 0.0000001m);
    }

    [TestMethod]
    public async Task GivenExpandedMappedAssets_WhenGetPositionsAsync_ThenReturnsAllSupportedBinanceAssets()
    {
        _authClientMock
            .Setup(client => client.GetPositionRiskAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreatePosition("SOLUSDT", "10", "150", "155", "5", "cross"),
                CreatePosition("LINKUSDT", "-3", "18", "17.5", "4", "isolated", isolatedMargin: "13.125"),
                CreatePosition("XRPUSDT", "25", "0.5", "0.55", "3", "cross"),
            ]);

        _authClientMock
            .Setup(client => client.GetOpenOrdersAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BinanceOpenOrderSnapshot>());

        var result = await _sut.GetPositionsAsync(cancellationToken: CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(position => position.Asset).Should().BeEquivalentTo(["SOL", "LINK"]);
    }

    [TestMethod]
    public async Task GivenNoPairFilter_WhenGetRecentFillsAsync_ThenQueriesMappedSymbolsSequentially()
    {
        var requestedSymbols = new ConcurrentQueue<string>();
        var inFlight = 0;
        var maxConcurrency = 0;
        long nextTimestamp = 1_000;

        _authClientMock
            .Setup(client => client.GetUserTradesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<string, int, CancellationToken>(async (symbol, _, cancellationToken) =>
            {
                requestedSymbols.Enqueue(symbol);

                var currentConcurrency = Interlocked.Increment(ref inFlight);
                maxConcurrency = Math.Max(maxConcurrency, currentConcurrency);

                try
                {
                    await Task.Delay(5, cancellationToken);
                    var timestamp = Interlocked.Increment(ref nextTimestamp);

                    return
                    [
                        new BinanceUserTradeSnapshot
                        {
                            Symbol = symbol,
                            OrderId = timestamp,
                            Price = "10",
                            Quantity = "1",
                            Commission = "0.1",
                            RealizedPnl = "0.2",
                            Time = timestamp,
                            Buyer = true,
                        },
                    ];
                }
                finally
                {
                    Interlocked.Decrement(ref inFlight);
                }
            });

        var result = await _sut.GetRecentFillsAsync(cancellationToken: CancellationToken.None);

        result.Should().HaveCount(BinanceAssetMapper.SupportedAssets.Count);
        result.Select(fill => fill.Asset).Should().BeEquivalentTo(BinanceAssetMapper.SupportedAssets);
        requestedSymbols.Should().BeEquivalentTo(BinanceAssetMapper.SupportedAssets.Select(BinanceAssetMapper.ToFuturesSymbol));
        maxConcurrency.Should().Be(1);
        result.Select(fill => fill.Timestamp).Should().BeInDescendingOrder();
    }

    private static BinancePositionRiskSnapshot CreatePosition(
        string symbol,
        string positionAmount,
        string entryPrice,
        string markPrice,
        string leverage,
        string marginType,
        string isolatedMargin = "0")
    {
        return new BinancePositionRiskSnapshot
        {
            Symbol = symbol,
            PositionAmount = positionAmount,
            EntryPrice = entryPrice,
            MarkPrice = markPrice,
            UnrealizedProfit = "12.5",
            LiquidationPrice = "100",
            Leverage = leverage,
            MarginType = marginType,
            IsolatedMargin = isolatedMargin,
        };
    }
}