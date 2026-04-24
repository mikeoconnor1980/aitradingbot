namespace TradePilot.Application.Abstractions.Services;

public interface IBinanceSpotExchangeInfoCache
{
    Task<IReadOnlyDictionary<string, BinanceSpotSymbolMetadata>> GetSupportedSymbolsAsync(CancellationToken cancellationToken = default);

    Task<BinanceSpotSymbolMetadata?> GetSymbolAsync(string asset, CancellationToken cancellationToken = default);
}

public sealed record BinanceSpotSymbolMetadata(
    string Asset,
    string Symbol,
    int SizeDecimals,
    int PriceDecimals,
    decimal MinNotional);
