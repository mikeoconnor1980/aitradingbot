using TradingApp.Application.StrategyAuthoring.Models;
using TradingApp.Domain.Enums;

namespace TradingApp.Application.StrategyAuthoring.Services;

public static class RevisionSourceMapper
{
    public static RevisionSource MapFrom(StrategyEntryPoint? entryPoint)
    {
        return entryPoint switch
        {
            null => RevisionSource.Ui,
            StrategyEntryPoint.UiBuilder => RevisionSource.Ui,
            StrategyEntryPoint.NaturalLanguage => RevisionSource.Api,
            StrategyEntryPoint.PineImport => RevisionSource.Import,
            StrategyEntryPoint.Migration => RevisionSource.Import,
            _ => throw new ArgumentOutOfRangeException(nameof(entryPoint), entryPoint, "Unmapped entry point"),
        };
    }
}