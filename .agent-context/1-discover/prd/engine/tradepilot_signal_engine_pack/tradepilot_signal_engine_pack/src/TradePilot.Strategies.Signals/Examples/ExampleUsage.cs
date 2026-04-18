using System;
using System.Collections.Generic;
using TradePilot.Strategies.Signals.Examples;
using TradePilot.Strategies.Signals.Models;

namespace TradePilot.Strategies.Signals.Examples;

public static class ExampleUsage
{
    public static void Run()
    {
        var candles5m = new List<Candle>
        {
            new(DateTimeOffset.UtcNow.AddMinutes(-20), 100, 102, 99, 101, 10),
            new(DateTimeOffset.UtcNow.AddMinutes(-15), 101, 103, 100, 102, 11),
            new(DateTimeOffset.UtcNow.AddMinutes(-10), 102, 104, 101, 103, 12),
            new(DateTimeOffset.UtcNow.AddMinutes(-5), 103, 106, 102, 104, 14),
            new(DateTimeOffset.UtcNow, 104, 107, 103, 106, 18)
        };

        var context = new InMemorySignalContext(
            "ETHUSDT",
            new Dictionary<string, IReadOnlyList<Candle>>
            {
                ["5m"] = candles5m
            });

        var registry = DerivedSignalBootstrap.CreateDefaultRegistry();

        var request = new SignalRequest(
            "candle_pattern",
            "5m",
            new Dictionary<string, object?>
            {
                ["pattern"] = "bullish_continuation"
            });

        var signal = registry.Get(request.Name);
        var result = signal.Evaluate(context, request);

        Console.WriteLine($"Matched: {result.IsMatch}, Score: {result.Score}");
    }
}