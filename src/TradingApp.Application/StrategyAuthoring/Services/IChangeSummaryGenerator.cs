namespace TradingApp.Application.StrategyAuthoring.Services;

public interface IChangeSummaryGenerator
{
    string Generate(string? previousConfigJson, string currentConfigJson);
}