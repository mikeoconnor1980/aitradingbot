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
            StrategyEntryPoint.UiWizard => RevisionSource.Ui,
            StrategyEntryPoint.NaturalLanguage => RevisionSource.Api,
            StrategyEntryPoint.PineImport => RevisionSource.Import,
            StrategyEntryPoint.Migration => RevisionSource.Import,
            StrategyEntryPoint.Optimizer => RevisionSource.Optimizer,
            _ => throw new ArgumentOutOfRangeException(nameof(entryPoint), entryPoint, "Unmapped entry point"),
        };
    }
}