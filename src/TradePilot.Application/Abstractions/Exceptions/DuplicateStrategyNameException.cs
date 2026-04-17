namespace TradePilot.Application.Abstractions.Exceptions;

public sealed class DuplicateStrategyNameException : Exception
{
    public DuplicateStrategyNameException(string name)
        : base($"A strategy named '{name}' already exists.")
    {
    }
}