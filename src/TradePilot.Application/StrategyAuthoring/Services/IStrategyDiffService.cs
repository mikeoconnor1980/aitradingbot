using TradePilot.Application.StrategyAuthoring.Models;

namespace TradePilot.Application.StrategyAuthoring.Services;

public interface IStrategyDiffService
{
    IReadOnlyList<FieldChangeDto> ComputeDiff(string fromConfigJson, string toConfigJson);
}