namespace TradingApp.Application.Abstractions.Exceptions;

public sealed class BacktestUnavailableException : Exception
{
    public BacktestUnavailableException(string message)
        : base(message)
    {
    }
}