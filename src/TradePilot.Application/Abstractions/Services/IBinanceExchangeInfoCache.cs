namespace TradePilot.Application.Abstractions.Services;

public interface IBinanceExchangeInfoCache
{
    Task<IReadOnlyDictionary<string, BinanceExchangeSymbolMetadata>> GetSupportedSymbolsAsync(CancellationToken cancellationToken = default);

    Task<BinanceExchangeSymbolMetadata?> GetSymbolAsync(string asset, CancellationToken cancellationToken = default);
}

public sealed record BinanceExchangeSymbolMetadata(
    string Asset,
    string Symbol,
    int SizeDecimals,
    int PriceDecimals,
    int MaxLeverage);