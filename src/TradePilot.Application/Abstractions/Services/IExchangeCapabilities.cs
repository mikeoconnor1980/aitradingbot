using TradePilot.Domain.ValueObjects;

namespace TradePilot.Application.Abstractions.Services;

public interface IExchangeCapabilities
{
    Exchange Exchange { get; }

    ExchangeCapabilitySet CapabilitySet { get; }

    bool Supports(TradingPair pair);
}