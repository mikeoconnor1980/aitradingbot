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
using TradePilot.Application.StrategyEvaluations.Queries;
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
    private readonly IStrategyRepository _strategyRepository;
    private readonly ILogger<TradePilotAnalystToolCatalog> _logger;

    /// <summary>Initializes the native TradePilot Analyst tool catalogue.</summary>
    public TradePilotAnalystToolCatalog(
        ISender sender,
        IExchangeResolver exchangeResolver,
        IUserWalletAddressRepository walletRepository,
        IUserExchangeCredentialRepository credentialRepository,
        IStrategyRepository strategyRepository,
        ILogger<TradePilotAnalystToolCatalog> logger)
    {
        _sender = sender;
        _exchangeResolver = exchangeResolver;
        _walletRepository = walletRepository;
        _credentialRepository = credentialRepository;
        _strategyRepository = strategyRepository;
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
                "get_strategy_evaluations" => await GetStrategyEvaluationsAsync(argumentsJson, context, cancellationToken),
                "get_latest_strategy_evaluation" => await GetLatestStrategyEvaluationAsync(argumentsJson, context, cancellationToken),
                "get_strategy_evaluation_summary" => await GetStrategyEvaluationSummaryAsync(argumentsJson, context, cancellationToken),
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

    private async Task<AnalystToolResult> GetStrategyEvaluationsAsync(
        string argumentsJson,
        AnalystToolContext context,
        CancellationToken cancellationToken)
    {
        var arguments = Parse<StrategyEvaluationArguments>(argumentsJson);
        var strategyId = await ResolveOwnedStrategyIdAsync(
            context,
            arguments.StrategyId,
            arguments.StrategyName,
            cancellationToken);
        var result = await _sender.Send(
            new GetStrategyEvaluationsQuery(
                StrategyId: strategyId,
                StrategyVersion: arguments.StrategyVersion,
                Symbol: arguments.Symbol,
                From: arguments.From,
                To: arguments.To,
                Limit: arguments.Limit ?? 100),
            cancellationToken);
        return AnalystToolResult.Success(SerializeResult(result));
    }

    private async Task<AnalystToolResult> GetLatestStrategyEvaluationAsync(
        string argumentsJson,
        AnalystToolContext context,
        CancellationToken cancellationToken)
    {
        var arguments = Parse<LatestStrategyEvaluationArguments>(argumentsJson);
        var strategyId = await ResolveOwnedStrategyIdAsync(
            context,
            arguments.StrategyId,
            arguments.StrategyName,
            cancellationToken);
        var result = await _sender.Send(
            new GetLatestStrategyEvaluationQuery(
                StrategyId: strategyId,
                StrategyVersion: arguments.StrategyVersion,
                Symbol: arguments.Symbol,
                AtOrBefore: arguments.AtOrBefore),
            cancellationToken);
        return result is null
            ? AnalystToolResult.Failure(
                "no_evaluation_evidence",
                "No recorded strategy evaluation was available for the requested strategy and period.")
            : AnalystToolResult.Success(SerializeResult(result));
    }

    private async Task<AnalystToolResult> GetStrategyEvaluationSummaryAsync(
        string argumentsJson,
        AnalystToolContext context,
        CancellationToken cancellationToken)
    {
        var arguments = Parse<StrategyEvaluationArguments>(argumentsJson);
        var strategyId = await ResolveOwnedStrategyIdAsync(
            context,
            arguments.StrategyId,
            arguments.StrategyName,
            cancellationToken);
        var result = await _sender.Send(
            new GetStrategyEvaluationSummaryQuery(
                StrategyId: strategyId,
                StrategyVersion: arguments.StrategyVersion,
                Symbol: arguments.Symbol,
                From: arguments.From,
                To: arguments.To),
            cancellationToken);
        return AnalystToolResult.Success(SerializeResult(result));
    }

    private Task<Exchange> ResolveExchangeAsync(Exchange? exchange, CancellationToken cancellationToken)
    {
        return exchange.HasValue
            ? Task.FromResult(exchange.Value)
            : _exchangeResolver.GetCurrentExchangeAsync(cancellationToken);
    }

    private async Task<Guid> ResolveOwnedStrategyIdAsync(
        AnalystToolContext context,
        Guid? strategyId,
        string? strategyName,
        CancellationToken cancellationToken)
    {
        if (!context.UserId.HasValue)
        {
            throw new InvalidOperationException("An authenticated TradePilot user is required for strategy tools.");
        }

        var userId = context.UserId.Value.ToString();
        if (strategyId.HasValue)
        {
            var strategy = await _strategyRepository.GetByIdAsync(strategyId.Value, cancellationToken);
            if (strategy is null || strategy.UserId != userId)
            {
                throw new NotFoundException(nameof(TradePilot.Domain.Entities.Strategy), strategyId.Value);
            }

            return strategy.Id;
        }

        RequireValue(strategyName, nameof(strategyName));
        var candidateIds = await _strategyRepository.SearchIdsByNameAsync(strategyName.Trim(), cancellationToken);
        var strategies = await _strategyRepository.GetByIdsAsync(candidateIds, cancellationToken);
        var matchedStrategy = strategies.FirstOrDefault(strategy =>
            strategy.UserId == userId
            && string.Equals(strategy.Name, strategyName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (matchedStrategy is null)
        {
            throw new NotFoundException(nameof(TradePilot.Domain.Entities.Strategy), strategyName!);
        }

        return matchedStrategy.Id;
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
            Define(
                "get_strategy_evaluations",
                "Get bounded historical deterministic strategy-evaluation evidence. Use strategyId or strategyName; never infer decisions from market analysis.",
                StrategyEvaluationProperties(includeRange: true, includeLimit: true),
                []),
            Define(
                "get_latest_strategy_evaluation",
                "Get the latest recorded deterministic evaluation for why a strategy did or did not act. This is authoritative for strategy-decision explanations.",
                new
                {
                    strategyId = StringProperty("Optional TradePilot strategy GUID; provide this or strategyName."),
                    strategyName = StringProperty("Optional exact strategy name, such as v10.4; provide this or strategyId."),
                    strategyVersion = new { type = "integer", description = "Optional persisted strategy version." },
                    symbol = StringProperty("Optional exchange-facing symbol such as BTC."),
                    atOrBefore = StringProperty("Optional ISO-8601 historical cutoff."),
                },
                []),
            Define(
                "get_strategy_evaluation_summary",
                "Get deterministic counts and blocking-rule frequencies calculated by TradePilot for a bounded period.",
                StrategyEvaluationProperties(includeRange: true, includeLimit: false),
                []),
        ];
    }

    private static object StrategyEvaluationProperties(bool includeRange, bool includeLimit)
    {
        var properties = new Dictionary<string, object>
        {
            ["strategyId"] = StringProperty("Optional TradePilot strategy GUID; provide this or strategyName."),
            ["strategyName"] = StringProperty("Optional exact strategy name, such as v10.4; provide this or strategyId."),
            ["strategyVersion"] = new { type = "integer", description = "Optional persisted strategy version." },
            ["symbol"] = StringProperty("Optional exchange-facing symbol such as BTC."),
        };

        if (includeRange)
        {
            properties["from"] = StringProperty("Optional inclusive ISO-8601 range start.");
            properties["to"] = StringProperty("Optional inclusive ISO-8601 range end.");
        }

        if (includeLimit)
        {
            properties["limit"] = new { type = "integer", minimum = 1, maximum = 500, description = "Maximum records; defaults to 100." };
        }

        return properties;
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

    private sealed record StrategyEvaluationArguments(
        Guid? StrategyId = null,
        string? StrategyName = null,
        int? StrategyVersion = null,
        string? Symbol = null,
        DateTimeOffset? From = null,
        DateTimeOffset? To = null,
        int? Limit = null);

    private sealed record LatestStrategyEvaluationArguments(
        Guid? StrategyId = null,
        string? StrategyName = null,
        int? StrategyVersion = null,
        string? Symbol = null,
        DateTimeOffset? AtOrBefore = null);

    private sealed record AccountContext(Exchange Exchange, string? WalletAddress);
}
