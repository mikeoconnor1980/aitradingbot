using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.Extensions.Logging;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Analyst.Models;
using TradePilot.Application.MarketAnalysis.Queries;
using TradePilot.Application.MarketData.Queries;
using TradePilot.Domain.Enums;

namespace TradePilot.AI.Analyst;

/// <summary>
/// Maps the Analyst's fixed read-only tool set directly to existing application queries.
/// No MCP or exchange client is reachable from this catalogue.
/// </summary>
public sealed class TradePilotAnalystToolCatalog : IAnalystToolCatalog
{
    private static readonly string[] DefaultTimeframes = ["15m", "1h", "4h", "1d"];
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly ISender _sender;
    private readonly IExchangeResolver _exchangeResolver;
    private readonly IUserWalletAddressRepository _walletRepository;
    private readonly IUserExchangeCredentialRepository _credentialRepository;
    private readonly ILogger<TradePilotAnalystToolCatalog> _logger;

    /// <summary>Initializes the native TradePilot Analyst tool catalogue.</summary>
    public TradePilotAnalystToolCatalog(
        ISender sender,
        IExchangeResolver exchangeResolver,
        IUserWalletAddressRepository walletRepository,
        IUserExchangeCredentialRepository credentialRepository,
        ILogger<TradePilotAnalystToolCatalog> logger)
    {
        _sender = sender;
        _exchangeResolver = exchangeResolver;
        _walletRepository = walletRepository;
        _credentialRepository = credentialRepository;
        _logger = logger;
        Definitions = CreateDefinitions();
    }

    /// <inheritdoc />
    public IReadOnlyList<AnalystToolDefinition> Definitions { get; }

    /// <inheritdoc />
    public async Task<AnalystToolResult> ExecuteAsync(
        string toolName,
        string argumentsJson,
        AnalystToolContext context,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            return toolName switch
            {
                "get_market_snapshot" => await GetMarketSnapshotAsync(argumentsJson, cancellationToken),
                "analyse_market" => await AnalyseMarketAsync(argumentsJson, cancellationToken),
                "analyse_market_multi_timeframe" => await AnalyseMarketMultiTimeframeAsync(argumentsJson, cancellationToken),
                "get_account_summary" => await GetAccountSummaryAsync(argumentsJson, context, cancellationToken),
                "get_positions" => await GetPositionsAsync(argumentsJson, context, cancellationToken),
                "get_open_orders" => await GetOpenOrdersAsync(argumentsJson, context, cancellationToken),
                "get_recent_fills" => await GetRecentFillsAsync(argumentsJson, context, cancellationToken),
                _ => AnalystToolResult.Failure("unknown_tool", "The requested tool is not available."),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException)
        {
            return AnalystToolResult.Failure("invalid_arguments", "The tool arguments were not valid JSON for this tool.");
        }
        catch (ArgumentException)
        {
            return AnalystToolResult.Failure("invalid_arguments", "One or more required tool arguments were missing or invalid.");
        }
        catch (DomainException exception)
        {
            _logger.LogWarning("Analyst tool {ToolName} was rejected by TradePilot", toolName);
            return AnalystToolResult.Failure("request_rejected", exception.Message);
        }
        catch (NotFoundException)
        {
            _logger.LogWarning("Analyst tool {ToolName} could not find the requested data", toolName);
            return AnalystToolResult.Failure("not_found", "TradePilot could not find the requested data.");
        }
        catch (ExchangeApiException exception)
        {
            _logger.LogWarning(
                "Analyst tool {ToolName} failed in upstream category {FailureCategory}",
                toolName,
                exception.ErrorCategory);
            return AnalystToolResult.Failure("data_unavailable", "Current TradePilot data is temporarily unavailable.");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogError(
                "Analyst tool {ToolName} is unavailable due to configuration ({ExceptionType})",
                toolName,
                exception.GetType().Name);
            return AnalystToolResult.Failure("capability_unavailable", "TradePilot is not configured for this capability.");
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Analyst tool {ToolName} failed unexpectedly ({ExceptionType})",
                toolName,
                exception.GetType().Name);
            return AnalystToolResult.Failure("tool_failure", "TradePilot could not complete the requested capability.");
        }
    }

    /// <summary>Serializes structured results for the provider without flattening evidence.</summary>
    public static JsonElement SerializeResult<T>(T result)
    {
        return JsonSerializer.SerializeToElement(result, JsonOptions);
    }

    private async Task<AnalystToolResult> GetMarketSnapshotAsync(
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        var arguments = Parse<MarketSnapshotArguments>(argumentsJson);
        RequireValue(arguments.Symbol, nameof(arguments.Symbol));
        var exchange = await ResolveExchangeAsync(arguments.Exchange, cancellationToken);
        var result = await _sender.Send(new GetMarketInfoQuery(arguments.Symbol, exchange), cancellationToken);
        return AnalystToolResult.Success(SerializeResult(result));
    }

    private async Task<AnalystToolResult> AnalyseMarketAsync(
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        var arguments = Parse<AnalyseMarketArguments>(argumentsJson);
        RequireValue(arguments.Symbol, nameof(arguments.Symbol));
        RequireValue(arguments.Timeframe, nameof(arguments.Timeframe));
        var exchange = await ResolveExchangeAsync(arguments.Exchange, cancellationToken);
        var result = await _sender.Send(
            new AnalyseMarketQuery(arguments.Symbol, arguments.Timeframe, exchange, arguments.Cutoff),
            cancellationToken);
        return AnalystToolResult.Success(SerializeResult(result));
    }

    private async Task<AnalystToolResult> AnalyseMarketMultiTimeframeAsync(
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        var arguments = Parse<AnalyseMarketMultiTimeframeArguments>(argumentsJson);
        RequireValue(arguments.Symbol, nameof(arguments.Symbol));
        var timeframes = arguments.Timeframes ?? DefaultTimeframes;
        var exchange = await ResolveExchangeAsync(arguments.Exchange, cancellationToken);
        var result = await _sender.Send(
            new AnalyseMarketMultiTimeframeQuery(arguments.Symbol, timeframes, exchange, arguments.Cutoff),
            cancellationToken);
        return AnalystToolResult.Success(SerializeResult(result));
    }

    private async Task<AnalystToolResult> GetAccountSummaryAsync(
        string argumentsJson,
        AnalystToolContext context,
        CancellationToken cancellationToken)
    {
        var arguments = Parse<AccountArguments>(argumentsJson);
        var account = await ResolveAccountContextAsync(context, arguments.Exchange, cancellationToken);
        var result = await _sender.Send(
            new GetAccountSummaryQuery(account.Exchange, account.WalletAddress),
            cancellationToken);
        return AnalystToolResult.Success(SerializeResult(result));
    }

    private async Task<AnalystToolResult> GetPositionsAsync(
        string argumentsJson,
        AnalystToolContext context,
        CancellationToken cancellationToken)
    {
        var arguments = Parse<AccountArguments>(argumentsJson);
        var account = await ResolveAccountContextAsync(context, arguments.Exchange, cancellationToken);
        var result = await _sender.Send(
            new GetOpenPositionsQuery(account.Exchange, account.WalletAddress),
            cancellationToken);
        return AnalystToolResult.Success(SerializeResult(result));
    }

    private async Task<AnalystToolResult> GetOpenOrdersAsync(
        string argumentsJson,
        AnalystToolContext context,
        CancellationToken cancellationToken)
    {
        var arguments = Parse<AccountArguments>(argumentsJson);
        var account = await ResolveAccountContextAsync(context, arguments.Exchange, cancellationToken);
        var result = await _sender.Send(
            new GetOpenOrdersQuery(account.Exchange, account.WalletAddress),
            cancellationToken);
        return AnalystToolResult.Success(SerializeResult(result));
    }

    private async Task<AnalystToolResult> GetRecentFillsAsync(
        string argumentsJson,
        AnalystToolContext context,
        CancellationToken cancellationToken)
    {
        var arguments = Parse<RecentFillsArguments>(argumentsJson);
        var account = await ResolveAccountContextAsync(context, arguments.Exchange, cancellationToken);
        var result = await _sender.Send(
            new GetRecentFillsQuery(account.Exchange, arguments.Symbol, account.WalletAddress),
            cancellationToken);
        return AnalystToolResult.Success(SerializeResult(result));
    }

    private Task<Exchange> ResolveExchangeAsync(Exchange? exchange, CancellationToken cancellationToken)
    {
        return exchange.HasValue
            ? Task.FromResult(exchange.Value)
            : _exchangeResolver.GetCurrentExchangeAsync(cancellationToken);
    }

    private async Task<AccountContext> ResolveAccountContextAsync(
        AnalystToolContext context,
        Exchange? requestedExchange,
        CancellationToken cancellationToken)
    {
        if (!context.UserId.HasValue)
        {
            throw new InvalidOperationException("An authenticated TradePilot user is required for account tools.");
        }

        var exchange = await ResolveExchangeAsync(requestedExchange, cancellationToken);
        if (exchange == Exchange.Hyperliquid)
        {
            var wallet = await _walletRepository.GetActiveByUserIdAndExchangeAsync(
                context.UserId.Value,
                exchange,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(wallet?.WalletAddress))
            {
                throw new InvalidOperationException("No active Hyperliquid wallet is configured.");
            }

            return new AccountContext(exchange, wallet.WalletAddress);
        }

        if (exchange == Exchange.Binance)
        {
            var credentials = await _credentialRepository.GetActiveByUserIdAndExchangeAsync(
                context.UserId.Value,
                exchange,
                cancellationToken);
            if (credentials is null)
            {
                throw new InvalidOperationException("No active Binance credentials are configured.");
            }

            return new AccountContext(exchange, null);
        }

        throw new InvalidOperationException($"Exchange '{exchange}' is unsupported.");
    }

    private static T Parse<T>(string argumentsJson)
    {
        var json = string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson;
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new JsonException("Tool arguments deserialized to null.");
    }

    private static void RequireValue(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A required tool argument was missing.", parameterName);
        }
    }

    private static IReadOnlyList<AnalystToolDefinition> CreateDefinitions()
    {
        return
        [
            Define(
                "get_market_snapshot",
                "Get TradePilot's current structured market snapshot for one symbol.",
                new { symbol = StringProperty("Exchange-facing symbol such as BTC."), exchange = ExchangeProperty() },
                ["symbol"]),
            Define(
                "analyse_market",
                "Get TradePilot's deterministic Phase 2 market analysis. Never recalculate its classifications.",
                new
                {
                    symbol = StringProperty("Exchange-facing symbol such as BTC."),
                    timeframe = StringProperty("TradePilot-supported timeframe such as 15m, 1h, 4h, or 1d."),
                    exchange = ExchangeProperty(),
                    cutoff = StringProperty("Optional ISO-8601 UTC cutoff."),
                },
                ["symbol", "timeframe"]),
            Define(
                "analyse_market_multi_timeframe",
                "Get TradePilot's deterministic Phase 3 multi-timeframe evidence, alignment, and conflict facts.",
                new
                {
                    symbol = StringProperty("Exchange-facing symbol such as BTC."),
                    timeframes = new { type = "array", items = new { type = "string" }, description = "Optional timeframes; defaults to 15m, 1h, 4h, and 1d." },
                    exchange = ExchangeProperty(),
                    cutoff = StringProperty("Optional ISO-8601 UTC cutoff."),
                },
                ["symbol"]),
            Define(
                "get_account_summary",
                "Get the authenticated user's current TradePilot account summary. Read-only.",
                new { exchange = ExchangeProperty() },
                []),
            Define(
                "get_positions",
                "Get the authenticated user's current open positions. Read-only.",
                new { exchange = ExchangeProperty() },
                []),
            Define(
                "get_open_orders",
                "Get the authenticated user's current open orders. Read-only; cannot place or cancel orders.",
                new { exchange = ExchangeProperty() },
                []),
            Define(
                "get_recent_fills",
                "Get the authenticated user's recent fills, optionally filtered by symbol. Read-only.",
                new { symbol = StringProperty("Optional exchange-facing symbol."), exchange = ExchangeProperty() },
                []),
        ];
    }

    private static AnalystToolDefinition Define(
        string name,
        string description,
        object properties,
        string[] required)
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties,
            required,
            additionalProperties = false,
        });
        return new AnalystToolDefinition(name, description, schema);
    }

    private static object StringProperty(string description) => new { type = "string", description };

    private static object ExchangeProperty() => new
    {
        type = "string",
        @enum = new[] { "Hyperliquid", "Binance" },
        description = "Optional exchange; omit to use the current TradePilot selection.",
    };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
        return options;
    }

    private sealed record MarketSnapshotArguments(string Symbol = "", Exchange? Exchange = null);

    private sealed record AnalyseMarketArguments(
        string Symbol = "",
        string Timeframe = "",
        Exchange? Exchange = null,
        DateTimeOffset? Cutoff = null);

    private sealed record AnalyseMarketMultiTimeframeArguments(
        string Symbol = "",
        string[]? Timeframes = null,
        Exchange? Exchange = null,
        DateTimeOffset? Cutoff = null);

    private sealed record AccountArguments(Exchange? Exchange = null);

    private sealed record RecentFillsArguments(string? Symbol = null, Exchange? Exchange = null);

    private sealed record AccountContext(Exchange Exchange, string? WalletAddress);
}
