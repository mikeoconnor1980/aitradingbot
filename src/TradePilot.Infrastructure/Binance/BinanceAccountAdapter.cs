using System.Globalization;
using System.Text.Json;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Infrastructure.Binance;

public sealed class BinanceAccountAdapter : IExchangeAccountClient
{
    private static readonly HashSet<string> SupportedAssets = new(StringComparer.OrdinalIgnoreCase)
    {
        "BTC",
        "ETH",
    };

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

        var equity = ParseDecimal(account.TotalCrossWalletBalance);
        if (equity == 0m)
        {
            equity = ParseDecimal(account.TotalWalletBalance);
        }

        if (equity == 0m && usdtBalance is not null)
        {
            equity = ParseDecimal(usdtBalance.CrossWalletBalance);
        }

        var unrealizedPnl = ParseDecimal(account.TotalCrossUnrealizedPnl);
        if (unrealizedPnl == 0m)
        {
            unrealizedPnl = ParseDecimal(account.TotalUnrealizedProfit);
        }

        if (unrealizedPnl == 0m && usdtBalance is not null)
        {
            unrealizedPnl = ParseDecimal(usdtBalance.CrossUnrealizedPnl);
        }

        equity += unrealizedPnl;

        var availableMargin = ParseDecimal(account.AvailableBalance);
        if (availableMargin == 0m && usdtBalance is not null)
        {
            availableMargin = ParseDecimal(usdtBalance.AvailableBalance);
        }

        var maintenanceMargin = ParseDecimal(account.TotalMaintenanceMargin);

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
            ? SupportedAssets.Select(BinanceAssetMapper.ToFuturesSymbol).ToArray()
            : [BinanceAssetMapper.ToFuturesSymbol(pair.Base)];

        var tradeTasks = symbols
            .Select(symbol => _authClient.GetUserTradesAsync(symbol, cancellationToken: cancellationToken))
            .ToArray();

        await Task.WhenAll(tradeTasks);

        return tradeTasks
            .SelectMany(task => task.Result)
            .OrderByDescending(trade => trade.Time)
            .Select(MapFill)
            .ToList();
    }

    private static PositionDto? MapPosition(BinancePositionRiskSnapshot position)
    {
        var size = ParseDecimal(position.PositionAmount);
        if (size == 0m)
        {
            return null;
        }

        var leverage = ParseInt(position.Leverage);
        var marginUsed = string.Equals(position.MarginType, "isolated", StringComparison.OrdinalIgnoreCase)
            ? ParseDecimal(position.IsolatedMargin)
            : (leverage > 0 ? Math.Abs(size) * ParseDecimal(position.MarkPrice) / leverage : 0m);

        var unrealizedPnl = ParseDecimal(position.UnrealizedProfit);
        var pnlPercent = marginUsed > 0m ? unrealizedPnl / marginUsed * 100m : 0m;

        return new PositionDto
        {
            Asset = ToAsset(position.Symbol),
            Size = size,
            Side = size >= 0m ? "Long" : "Short",
            EntryPrice = ParseDecimal(position.EntryPrice),
            MarkPrice = ParseDecimal(position.MarkPrice),
            UnrealisedPnl = unrealizedPnl,
            UnrealisedPnlPercent = pnlPercent,
            LiquidationPrice = ParseDecimal(position.LiquidationPrice),
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
            Price = ParseDecimal(order.Price),
            Size = ParseDecimal(order.OriginalQuantity),
            OrderType = orderType,
            Status = order.Status,
            TriggerPrice = IsTriggerOrder(order.Type) ? ParseDecimal(order.StopPrice) : null,
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
            Size = ParseDecimal(trade.Quantity),
            Price = ParseDecimal(trade.Price),
            Fee = ParseDecimal(trade.Commission),
            ClosedPnl = ParseDecimal(trade.RealizedPnl),
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
                var price = ParseDecimal(order.StopPrice);

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
        => SupportedAssets.Contains(ToAsset(symbol));

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

    private static int ParseInt(string value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static decimal ParseDecimal(string value)
        => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;

    private static string ToTitleCase(string value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
}