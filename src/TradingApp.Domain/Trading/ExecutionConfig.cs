namespace TradingApp.Domain.Trading;

public sealed record ExecutionConfig
{
    public FeeModel FeeModel { get; init; } = FeeModel.Default;
}