using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading.Models;
using TradePilot.Domain.Trading;

namespace TradePilot.Application.Trading.Services;

public sealed class DcaStrategyEngine : IStrategyEngine
{
    public Task<StrategyEvaluation> EvaluateAsync(MarketContext context, IStrategyConfig strategyConfig, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(strategyConfig);

        if (strategyConfig is not StrategyConfig config)
        {
            throw new ArgumentException(
                $"Expected {nameof(StrategyConfig)} but received {strategyConfig.GetType().Name}.",
                nameof(strategyConfig));
        }

        if (config.Dca is null
            || config.Dca.BaseAmountUsd <= 0m
            || config.Dca.Allocations.Count == 0)
        {
            return Task.FromResult(Fail("DCA configuration is incomplete."));
        }

        if (config.Direction != Direction.Long)
        {
            return Task.FromResult(Fail("DCA currently supports long accumulation only."));
        }

        var gates = config.Dca.GateConditions;
        if (gates is null)
        {
            return Task.FromResult(Pass("DCA buy window open."));
        }

        if (gates.MaxPriceUsd is > 0m && context.CurrentCandle.Close > gates.MaxPriceUsd.Value)
        {
            return Task.FromResult(Fail($"Price gate blocked DCA buy. Current price {context.CurrentCandle.Close} is above {gates.MaxPriceUsd.Value}."));
        }

        if (gates.MinFearGreedIndex.HasValue || gates.MaxFearGreedIndex.HasValue)
        {
            if (context.FearGreed is null)
            {
                return Task.FromResult(Fail("Fear & Greed gate is enabled but no Fear & Greed reading is available."));
            }

            if (gates.MinFearGreedIndex.HasValue && context.FearGreed.Value < gates.MinFearGreedIndex.Value)
            {
                return Task.FromResult(Fail($"Fear & Greed gate blocked DCA buy. Current value {context.FearGreed.Value} is below {gates.MinFearGreedIndex.Value}."));
            }

            if (gates.MaxFearGreedIndex.HasValue && context.FearGreed.Value > gates.MaxFearGreedIndex.Value)
            {
                return Task.FromResult(Fail($"Fear & Greed gate blocked DCA buy. Current value {context.FearGreed.Value} is above {gates.MaxFearGreedIndex.Value}."));
            }
        }

        return Task.FromResult(Pass("DCA buy window open."));
    }

    private static StrategyEvaluation Fail(string reason)
    {
        return new StrategyEvaluation
        {
            SetupDetected = false,
            Reason = reason,
        };
    }

    private static StrategyEvaluation Pass(string reason)
    {
        return new StrategyEvaluation
        {
            SetupDetected = true,
            Reason = reason,
        };
    }
}