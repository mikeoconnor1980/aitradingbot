using System;
using System.Collections.Generic;
using TradePilot.Strategies.Signals.Abstractions;

namespace TradePilot.Strategies.Signals.Registry;

public sealed class DerivedSignalRegistry : IDerivedSignalRegistry
{
    private readonly Dictionary<string, IDerivedSignal> _signals = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IDerivedSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        _signals[signal.Name] = signal;
    }

    public IDerivedSignal Get(string name)
    {
        if (!_signals.TryGetValue(name, out var signal))
            throw new KeyNotFoundException($"Derived signal '{name}' is not registered.");

        return signal;
    }

    public bool TryGet(string name, out IDerivedSignal? signal)
        => _signals.TryGetValue(name, out signal);

    public IReadOnlyCollection<string> ListNames()
        => _signals.Keys;
}