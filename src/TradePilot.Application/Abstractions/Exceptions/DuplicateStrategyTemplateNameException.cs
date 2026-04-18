namespace TradePilot.Application.Abstractions.Exceptions;

public sealed class DuplicateStrategyTemplateNameException : Exception
{
    public DuplicateStrategyTemplateNameException(string name)
        : base($"A library strategy named '{name}' already exists.")
    {
    }
}