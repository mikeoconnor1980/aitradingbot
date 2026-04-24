using TradePilot.Application.Abstractions.Services;

namespace TradePilot.Infrastructure.Binance;

public sealed class BinanceSymbolMetadataProvider : IExchangeSymbolMetadataProvider
{
    private readonly IBinanceExchangeInfoCache _cache;

    public BinanceSymbolMetadataProvider(IBinanceExchangeInfoCache cache)
    {
        _cache = cache;
    }

    public async Task<IReadOnlyList<ExchangeSymbolMetadata>> GetSupportedSymbolsAsync(CancellationToken cancellationToken = default)
    {
        var symbols = await _cache.GetSupportedSymbolsAsync(cancellationToken);
        return symbols.Values
            .Select(Map)
            .ToList();
    }

    public async Task<ExchangeSymbolMetadata?> GetSymbolAsync(string asset, CancellationToken cancellationToken = default)
    {
        var symbol = await _cache.GetSymbolAsync(asset, cancellationToken);
        return symbol is null ? null : Map(symbol);
    }

    private static ExchangeSymbolMetadata Map(BinanceExchangeSymbolMetadata metadata)
        => new(
            metadata.Asset,
            metadata.Symbol,
            metadata.SizeDecimals,
            metadata.PriceDecimals,
            metadata.MaxLeverage);
}