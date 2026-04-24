using System.Globalization;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Infrastructure.Binance;

public sealed class BinanceSpotAccountAdapter : IExchangeAccountClient
{
    private readonly IBinanceSpotAuthClient _authClient;

    public BinanceSpotAccountAdapter(IBinanceSpotAuthClient authClient)
    {
        _authClient = authClient;
    }

    public Exchange Exchange => Exchange.Binance;

    public async Task<AccountSummaryDto> GetAccountSummaryAsync(string? walletAddress = null, CancellationToken cancellationToken = default)
    {
        var account = await _authClient.GetAccountAsync(cancellationToken);

        var usdtBalance = account.Balances
            .FirstOrDefault(balance => string.Equals(balance.Asset, "USDT", StringComparison.OrdinalIgnoreCase));

        decimal equity = 0m;
        if (usdtBalance is not null)
        {
            BinanceParsing.TryParseDecimal(usdtBalance.Free, out var free);
            BinanceParsing.TryParseDecimal(usdtBalance.Locked, out var locked);
            equity = free + locked;
        }

        // Add estimated value of held assets (only supported assets)
        foreach (var balance in account.Balances)
        {
            if (string.Equals(balance.Asset, "USDT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!BinanceAssetMapper.IsValidSymbol(balance.Asset))
            {
                continue;
            }

            if (!BinanceParsing.TryParseDecimal(balance.Free, out var free))
            {
                continue;
            }

            if (!BinanceParsing.TryParseDecimal(balance.Locked, out var locked))
            {
                locked = 0m;
            }

            var total = free + locked;
            if (total > 0m)
            {
                // Note: without a price feed, we report the raw quantity.
                // A full implementation would multiply by current price.
                // For DCA tracking, the position list is more useful.
                equity += total;
            }
        }

        decimal availableMargin = 0m;
        if (usdtBalance is not null)
        {
            BinanceParsing.TryParseDecimal(usdtBalance.Free, out availableMargin);
        }

        return new AccountSummaryDto
        {
            Equity = equity,
            AvailableMargin = availableMargin,
            CrossMarginRatio = 0m,
            MaintenanceMargin = 0m,
            UnrealisedPnl = 0m,
        };
    }

    public async Task<IReadOnlyList<PositionDto>> GetPositionsAsync(string? walletAddress = null, CancellationToken cancellationToken = default)
    {
        var account = await _authClient.GetAccountAsync(cancellationToken);

        return account.Balances
            .Where(balance =>
                BinanceAssetMapper.IsValidSymbol(balance.Asset) &&
                BinanceParsing.TryParseDecimal(balance.Free, out var free) &&
                BinanceParsing.TryParseDecimal(balance.Locked, out var locked) &&
                (free + locked) > 0m)
            .Select(balance =>
            {
                BinanceParsing.TryParseDecimal(balance.Free, out var free);
                BinanceParsing.TryParseDecimal(balance.Locked, out var locked);
                var total = free + locked;

                return new PositionDto
                {
                    Asset = BinanceAssetMapper.NormalizeSymbol(balance.Asset),
                    Size = total,
                    EntryPrice = 0m, // Spot balances don't track entry price
                    MarkPrice = 0m,
                    UnrealisedPnl = 0m,
                    Leverage = 1,
                    LiquidationPrice = 0m,
                };
            })
            .ToList();
    }

    public async Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(string? walletAddress = null, CancellationToken cancellationToken = default)
    {
        var orders = await _authClient.GetOpenOrdersAsync(cancellationToken: cancellationToken);
        return orders
            .Where(order => BinanceAssetMapper.IsValidSymbol(
                order.Symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)
                    ? order.Symbol[..^4]
                    : order.Symbol))
            .Select(order =>
            {
                var asset = order.Symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)
                    ? order.Symbol[..^4]
                    : order.Symbol;

                return new OpenOrderDto
                {
                    OrderId = order.OrderId.ToString(CultureInfo.InvariantCulture),
                    Asset = BinanceAssetMapper.NormalizeSymbol(asset),
                    Side = order.Side,
                    OrderType = order.Type,
                    Price = BinanceParsing.TryParseDecimal(order.Price, out var price) ? price : 0m,
                    Size = BinanceParsing.TryParseDecimal(order.OrigQty, out var size) ? size : 0m,
                };
            })
            .ToList();
    }

    public async Task<IReadOnlyList<FillEventDto>> GetRecentFillsAsync(
        TradingPair? pair = null,
        string? walletAddress = null,
        CancellationToken cancellationToken = default)
    {
        var symbols = pair is null
            ? BinanceAssetMapper.SupportedAssets
                .OrderBy(asset => asset, StringComparer.OrdinalIgnoreCase)
                .Select(BinanceAssetMapper.ToSpotSymbol)
                .ToArray()
            : [BinanceAssetMapper.ToSpotSymbol(pair.Base)];

        List<BinanceSpotUserTrade> trades = [];

        foreach (var symbol in symbols)
        {
            var symbolTrades = await _authClient.GetUserTradesAsync(symbol, cancellationToken: cancellationToken);
            trades.AddRange(symbolTrades);
        }

        return trades
            .OrderByDescending(trade => trade.Time)
            .Select(trade =>
            {
                var asset = trade.Symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)
                    ? trade.Symbol[..^4]
                    : trade.Symbol;

                return new FillEventDto
                {
                    OrderId = trade.OrderId.ToString(CultureInfo.InvariantCulture),
                    Asset = BinanceAssetMapper.NormalizeSymbol(asset),
                    Side = trade.IsBuyer ? "Buy" : "Sell",
                    Price = BinanceParsing.TryParseDecimal(trade.Price, out var price) ? price : 0m,
                    Size = BinanceParsing.TryParseDecimal(trade.Qty, out var qty) ? qty : 0m,
                    Fee = BinanceParsing.TryParseDecimal(trade.Commission, out var fee) ? fee : 0m,
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(trade.Time).UtcDateTime,
                };
            })
            .ToList();
    }
}
