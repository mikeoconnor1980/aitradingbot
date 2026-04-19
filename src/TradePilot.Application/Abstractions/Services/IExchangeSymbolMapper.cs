using TradePilot.Domain.ValueObjects;

namespace TradePilot.Application.Abstractions.Services;

public interface IExchangeSymbolMapper
{
    Exchange Exchange { get; }

    string ToExchangeSymbol(TradingPair pair);

    TradingPair FromExchangeSymbol(string exchangeSymbol);

    bool CanMap(TradingPair pair);
}