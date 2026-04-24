using System.Globalization;
using System.Text.Json;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Infrastructure.Binance;

public sealed class BinanceAccountAdapter : IExchangeAccountClient
{
    private readonly IBinanceFuturesAuthClient _authClient;

    public BinanceAccountAdapter(IBinanceFuturesAuthClient authClient)
    {
        _authClient = authClient;
    }

    public Exchange Exchange => Exchange.Binance;

    public async Task<AccountSummaryDto> GetAccountSummaryAsync(string? walletAddress = null, CancellationToken cancellationToken = default)
    {
        var accountTask = _authClient.GetAccountAsync(cancellationToken);
        var balancesTask = _authClient.GetBalancesAsync(cancellationToken);

        await Task.WhenAll(accountTask, balancesTask);

        var account = await accountTask;
        var usdtBalance = (await balancesTask)
            .FirstOrDefault(balance => string.Equals(balance.Asset, "USDT", StringComparison.OrdinalIgnoreCase));

        if (!BinanceParsing.TryParseDecimal(account.TotalCrossWalletBalance, out var equity) || equity == 0m)
        {
            equity = BinanceParsing.ParseDecimal(account.TotalWalletBalance);
        }

        if (equity == 0m && usdtBalance is not null && BinanceParsing.TryParseDecimal(usdtBalance.CrossWalletBalance, out var crossWalletBalance))
        {
            equity = crossWalletBalance;
        }

        if (!BinanceParsing.TryParseDecimal(account.TotalCrossUnrealizedPnl, out var unrealizedPnl) || unrealizedPnl == 0m)
        {
            unrealizedPnl = BinanceParsing.ParseDecimal(account.TotalUnrealizedProfit);
        }

        if (unrealizedPnl == 0m && usdtBalance is not null && BinanceParsing.TryParseDecimal(usdtBalance.CrossUnrealizedPnl, out var crossUnrealizedPnl))
        {
            unrealizedPnl = crossUnrealizedPnl;
        }

        equity += unrealizedPnl;

        if (!BinanceParsing.TryParseDecimal(account.AvailableBalance, out var availableMargin) || availableMargin == 0m)
        {
            if (usdtBalance is not null)
            {
                availableMargin = BinanceParsing.ParseDecimal(usdtBalance.AvailableBalance);
            }
        }

        var maintenanceMargin = BinanceParsing.ParseDecimal(account.TotalMaintenanceMargin);

        return new AccountSummaryDto
        {
            Equity = equity,
            AvailableMargin = availableMargin,
            CrossMarginRatio = equity > 0m ? maintenanceMargin / equity : 0m,
            MaintenanceMargin = maintenanceMargin,
            UnrealisedPnl = unrealizedPnl,
        };
    }

    public async Task<IReadOnlyList<PositionDto>> GetPositionsAsync(string? walletAddress = null, CancellationToken cancellationToken = default)
    {
        var positionsTask = _authClient.GetPositionRiskAsync(cancellationToken: cancellationToken);
        var openOrdersTask = _authClient.GetOpenOrdersAsync(cancellationToken: cancellationToken);

        await Task.WhenAll(positionsTask, openOrdersTask);

        var positions = (await positionsTask)
            .Where(position => IsSupportedSymbol(position.Symbol))
            .Select(MapPosition)
            .Where(position => position is not null)
            .Cast<PositionDto>()
            .ToList();

        EnrichPositionsWithTriggerOrders(positions, await openOrdersTask);
        return positions;
    }

    public async Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(string? walletAddress = null, CancellationToken cancellationToken = default)
    {
        var orders = await _authClient.GetOpenOrdersAsync(cancellationToken: cancellationToken);
        return orders
            .Where(order => IsSupportedSymbol(order.Symbol))
            .Select(MapOpenOrder)
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
                .Select(BinanceAssetMapper.ToFuturesSymbol)
                .ToArray()
            : [BinanceAssetMapper.ToFuturesSymbol(pair.Base)];

        List<BinanceUserTradeSnapshot> trades = [];

        foreach (var symbol in symbols)
        {
            var symbolTrades = await _authClient.GetUserTradesAsync(symbol, cancellationToken: cancellationToken);
            trades.AddRange(symbolTrades);
        }

        return trades
            .OrderByDescending(trade => trade.Time)
            .Select(MapFill)
            .ToList();
    }

    private static PositionDto? MapPosition(BinancePositionRiskSnapshot position)
    {
        var size = BinanceParsing.ParseDecimal(position.PositionAmount);
        if (size == 0m)
        {
            return null;
        }

        var leverage = BinanceParsing.ParseInt(position.Leverage);
        var marginUsed = string.Equals(position.MarginType, "isolated", StringComparison.OrdinalIgnoreCase)
            ? BinanceParsing.ParseDecimal(position.IsolatedMargin)
            : (leverage > 0 ? Math.Abs(size) * BinanceParsing.ParseDecimal(position.MarkPrice) / leverage : 0m);

        var unrealizedPnl = BinanceParsing.ParseDecimal(position.UnrealizedProfit);
        var pnlPercent = marginUsed > 0m ? unrealizedPnl / marginUsed * 100m : 0m;

        return new PositionDto
        {
            Asset = ToAsset(position.Symbol),
            Size = size,
            Side = size >= 0m ? "Long" : "Short",
            EntryPrice = BinanceParsing.ParseDecimal(position.EntryPrice),
            MarkPrice = BinanceParsing.ParseDecimal(position.MarkPrice),
            UnrealisedPnl = unrealizedPnl,
            UnrealisedPnlPercent = pnlPercent,
            LiquidationPrice = BinanceParsing.ParseDecimal(position.LiquidationPrice),
            Leverage = leverage,
            MarginMode = position.MarginType,
            MarginUsed = marginUsed,
        };
    }

    private static OpenOrderDto MapOpenOrder(BinanceOpenOrderSnapshot order)
    {
        var orderType = IsTriggerOrder(order.Type) ? "trigger" : order.Type.ToLowerInvariant();
        var tpslType = order.Type switch
        {
            "STOP_MARKET" => "sl",
            "TAKE_PROFIT_MARKET" => "tp",
            _ => null,
        };

        return new OpenOrderDto
        {
            OrderId = order.OrderId.ToString(CultureInfo.InvariantCulture),
            Asset = ToAsset(order.Symbol),
            Side = ToTitleCase(order.Side),
            Price = BinanceParsing.ParseDecimal(order.Price),
            Size = BinanceParsing.ParseDecimal(order.OriginalQuantity),
            OrderType = orderType,
            Status = order.Status,
            TriggerPrice = IsTriggerOrder(order.Type) ? BinanceParsing.ParseDecimal(order.StopPrice) : null,
            TpslType = tpslType,
            IsReduceOnly = order.ReduceOnly || order.ClosePosition,
        };
    }

    private static FillEventDto MapFill(BinanceUserTradeSnapshot trade)
    {
        var side = trade.Buyer ? "Buy" : "Sell";

        return new FillEventDto
        {
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(trade.Time).UtcDateTime,
            Asset = ToAsset(trade.Symbol),
            Side = side,
            Direction = side,
            Size = BinanceParsing.ParseDecimal(trade.Quantity),
            Price = BinanceParsing.ParseDecimal(trade.Price),
            Fee = BinanceParsing.ParseDecimal(trade.Commission),
            ClosedPnl = BinanceParsing.ParseDecimal(trade.RealizedPnl),
            OrderId = trade.OrderId.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static void EnrichPositionsWithTriggerOrders(
        IReadOnlyList<PositionDto> positions,
        IReadOnlyList<BinanceOpenOrderSnapshot> openOrders)
    {
        var triggerOrdersByAsset = openOrders
            .Where(order => IsTriggerOrder(order.Type))
            .GroupBy(order => ToAsset(order.Symbol), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var position in positions)
        {
            if (!triggerOrdersByAsset.TryGetValue(position.Asset, out var triggerOrders))
            {
                continue;
            }

            foreach (var order in triggerOrders)
            {
                var price = BinanceParsing.ParseDecimal(order.StopPrice);

                switch (order.Type)
                {
                    case "STOP_MARKET":
                        position.StopLossPrice = price;
                        position.StopLossOrderId = order.OrderId.ToString(CultureInfo.InvariantCulture);
                        break;
                    case "TAKE_PROFIT_MARKET":
                        position.TakeProfitPrice = price;
                        position.TakeProfitOrderId = order.OrderId.ToString(CultureInfo.InvariantCulture);
                        break;
                }
            }
        }
    }

    private static bool IsTriggerOrder(string orderType)
        => string.Equals(orderType, "STOP_MARKET", StringComparison.OrdinalIgnoreCase)
            || string.Equals(orderType, "TAKE_PROFIT_MARKET", StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedSymbol(string symbol)
        => BinanceAssetMapper.SupportedAssets.Contains(ToAsset(symbol));

    private static string ToAsset(string symbol)
    {
        if (symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
        {
            return symbol[..^4];
        }

        if (symbol.EndsWith("USD", StringComparison.OrdinalIgnoreCase))
        {
            return symbol[..^3];
        }

        return symbol;
    }

    private static string ToTitleCase(string value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
}