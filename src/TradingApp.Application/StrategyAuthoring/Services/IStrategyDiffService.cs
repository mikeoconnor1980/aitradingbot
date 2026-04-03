using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Application.StrategyAuthoring.Services;

public interface IStrategyDiffService
{
    IReadOnlyList<FieldChangeDto> ComputeDiff(string fromConfigJson, string toConfigJson);
}