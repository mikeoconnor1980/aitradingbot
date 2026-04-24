using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketData.Models;
using TradePilot.Domain.ValueObjects;

namespace TradePilot.Infrastructure.Binance;

public sealed class BinanceMarketMetadataProvider : IExchangeMarketMetadataProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IBinanceExchangeInfoCache _exchangeInfoCache;
    private readonly ILogger<BinanceMarketMetadataProvider> _logger;

    public BinanceMarketMetadataProvider(
        IHttpClientFactory httpClientFactory,
        IBinanceExchangeInfoCache exchangeInfoCache,
        ILogger<BinanceMarketMetadataProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _exchangeInfoCache = exchangeInfoCache;
        _logger = logger;
    }

    public Exchange Exchange => Exchange.Binance;

    public async Task<MarketInfoDto?> GetMarketInfoAsync(TradingPair pair, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pair);

        var symbolMetadata = await _exchangeInfoCache.GetSymbolAsync(pair.Base, cancellationToken);
        if (symbolMetadata is null)
        {
            return null;
        }

        var symbol = symbolMetadata.Symbol;
        var client = _httpClientFactory.CreateClient("binance-public");

        try
        {
            var premiumTask = client.GetAsync($"/fapi/v1/premiumIndex?symbol={Uri.EscapeDataString(symbol)}", cancellationToken);
            var tickerTask = client.GetAsync($"/fapi/v1/ticker/24hr?symbol={Uri.EscapeDataString(symbol)}", cancellationToken);
            var openInterestTask = FetchOpenInterestAsync(client, symbol, cancellationToken);

            await Task.WhenAll(premiumTask, tickerTask, openInterestTask);

            using var premiumResponse = await premiumTask;
            using var tickerResponse = await tickerTask;

            premiumResponse.EnsureSuccessStatusCode();
            tickerResponse.EnsureSuccessStatusCode();

            await using var premiumStream = await premiumResponse.Content.ReadAsStreamAsync(cancellationToken);
            await using var tickerStream = await tickerResponse.Content.ReadAsStreamAsync(cancellationToken);

            using var premiumDocument = await JsonDocument.ParseAsync(premiumStream, cancellationToken: cancellationToken);
            using var tickerDocument = await JsonDocument.ParseAsync(tickerStream, cancellationToken: cancellationToken);

            var premium = premiumDocument.RootElement;
            var ticker = tickerDocument.RootElement;

            var markPrice = BinanceParsing.ParseDecimal(GetString(premium, "markPrice"));
            var indexPrice = BinanceParsing.ParseDecimal(GetString(premium, "indexPrice"));

            return new MarketInfoDto
            {
                Asset = pair.Base,
                MidPrice = markPrice,
                MarkPrice = markPrice,
                IndexPrice = indexPrice,
                FundingRate = BinanceParsing.ParseDecimal(GetString(premium, "lastFundingRate")),
                Volume24h = BinanceParsing.ParseDecimal(GetString(ticker, "quoteVolume")),
                OpenInterest = await openInterestTask,
                PriceChange24hPercent = BinanceParsing.ParseDecimal(GetString(ticker, "priceChangePercent")),
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException)
        {
            _logger.LogWarning(ex, "Failed to fetch Binance market info for {Asset}", pair.Base);
            return null;
        }
    }

    public Task<int?> GetMaxLeverageAsync(TradingPair pair, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pair);
        return GetMaxLeverageCoreAsync(pair, cancellationToken);
    }

    private async Task<int?> GetMaxLeverageCoreAsync(TradingPair pair, CancellationToken cancellationToken)
    {
        var symbolMetadata = await _exchangeInfoCache.GetSymbolAsync(pair.Base, cancellationToken);
        return symbolMetadata?.MaxLeverage;
    }

    private async Task<decimal> FetchOpenInterestAsync(HttpClient client, string futuresSymbol, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(
                $"/fapi/v1/openInterest?symbol={Uri.EscapeDataString(futuresSymbol)}",
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            return BinanceParsing.ParseDecimal(GetString(document.RootElement, "openInterest"));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException)
        {
            _logger.LogWarning(ex, "Failed to fetch Binance open interest for {Symbol}. Defaulting to 0.", futuresSymbol);
            return 0m;
        }
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }
}