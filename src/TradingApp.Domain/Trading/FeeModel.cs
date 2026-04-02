using TradingApp.Domain.Enums;

namespace TradingApp.Domain.Trading;

public sealed record FeeModel
{
    public decimal MakerFeeRate { get; init; } = 0.0001m;
    public decimal TakerFeeRate { get; init; } = 0.00035m;
    public decimal SlippageRate { get; init; } = 0m;

    public static FeeModel Default { get; } = new();

    public decimal CalculateFee(decimal fillSize, decimal fillPrice, bool isMaker)
    {
        var rate = isMaker ? MakerFeeRate : TakerFeeRate;
        return fillSize * fillPrice * rate;
    }

    public decimal ApplySlippage(decimal price, OrderSide side)
    {
        return side == OrderSide.Buy
            ? price * (1 + SlippageRate)
            : price * (1 - SlippageRate);
    }
}