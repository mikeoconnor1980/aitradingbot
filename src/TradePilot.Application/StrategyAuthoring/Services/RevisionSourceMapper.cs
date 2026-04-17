using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Domain.Enums;

namespace TradePilot.Application.StrategyAuthoring.Services;

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