namespace TradePilot.Application.StrategyAuthoring.Validation;

public sealed record ValidationError
{
    public required ValidationSeverity Severity { get; init; }
    public required string FieldPath { get; init; }
    public required string Code { get; init; }
    public required string Message { get; init; }
}