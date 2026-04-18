using TradePilot.Strategies.Signals.Implementations;
using TradePilot.Strategies.Signals.Registry;

namespace TradePilot.Strategies.Signals.Examples;

public static class DerivedSignalBootstrap
{
    public static DerivedSignalRegistry CreateDefaultRegistry()
    {
        var registry = new DerivedSignalRegistry();

        registry.Register(new CandlePatternSignal());
        registry.Register(new LiquiditySweepSignal());
        registry.Register(new StructureShiftSignal());
        registry.Register(new RangeStateSignal());
        registry.Register(new RegimeStateSignal());

        return registry;
    }
}