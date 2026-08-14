using System.ComponentModel;
using System.Diagnostics;
using System.Security.Claims;
using MediatR;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.MarketAnalysis.Models;
using TradePilot.Application.MarketAnalysis.Queries;
using TradePilot.Application.MarketData.Models;
using TradePilot.Application.MarketData.Queries;
using TradePilot.Domain.Enums;

namespace TradePilot.Api.Mcp;

/// <summary>
/// Exposes an explicitly allow-listed, read-only MCP adapter over TradePilot application queries.
/// </summary>
[McpServerToolType]
public sealed class TradePilotMcpTools
{
    private readonly ISender _sender;
    private readonly IExchangeResolver _exchangeResolver;
    private readonly IUserWalletAddressRepository _walletRepository;
    private readonly IUserExchangeCredentialRepository _credentialRepository;
    private readonly ILogger<TradePilotMcpTools> _logger;

    /// <summary>
    /// Initializes the read-only TradePilot MCP tool adapter.
    /// </summary>
    public TradePilotMcpTools(
        ISender sender,
        IExchangeResolver exchangeResolver,
        IUserWalletAddressRepository walletRepository,
        IUserExchangeCredentialRepository credentialRepository,
        ILogger<TradePilotMcpTools> logger)
    {
        _sender = sender;
        _exchangeResolver = exchangeResolver;
        _walletRepository = walletRepository;
        _credentialRepository = credentialRepository;
        _logger = logger;
    }

    /// <summary>
    /// Returns the existing TradePilot market-information snapshot for one exchange-facing symbol.
    /// </summary>
    [McpServerTool(Name = "get_market_snapshot", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns TradePilot's current structured market snapshot for one exchange-facing symbol, including available prices, funding, volume, and open interest. It does not provide trading advice.")]
    public async Task<MarketInfoDto> GetMarketSnapshotAsync(
        [Description("Exchange-facing market symbol understood by TradePilot, such as BTC or BTC-PERP.")] string symbol,
        [Description("Optional exchange selector. When omitted, TradePilot uses the authenticated user's selected exchange.")] Exchange? exchange = null,
        CancellationToken cancellationToken = default)
    {
        RequireValue(symbol, nameof(symbol));
        var resolvedExchange = await ResolveExchangeAsync(exchange, cancellationToken);

        return await ExecuteAsync(
            "get_market_snapshot",
            symbol,
            null,
            () => _sender.Send(new GetMarketInfoQuery(symbol, resolvedExchange), cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Returns the existing Phase 2 deterministic analysis without changing its calculations or policies.
    /// </summary>
    [McpServerTool(Name = "analyse_market", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns TradePilot's deterministic technical analysis for one market and timeframe using completed candle data and existing indicator and classification policies. It does not provide trading advice.")]
    public async Task<MarketAnalysisResult> AnalyseMarketAsync(
        [Description("Exchange-facing market symbol understood by TradePilot, such as BTC or BTC-PERP.")] string symbol,
        [Description("TradePilot-supported analysis timeframe, such as 15m, 1h, 4h, or 1d.")] string timeframe,
        [Description("Optional exchange selector. When omitted, TradePilot uses the authenticated user's selected exchange.")] Exchange? exchange = null,
        [Description("Optional UTC cutoff. Candles closing after this instant are excluded by the existing Phase 2 capability.")] DateTimeOffset? cutoff = null,
        CancellationToken cancellationToken = default)
    {
        RequireValue(symbol, nameof(symbol));
        RequireValue(timeframe, nameof(timeframe));
        var resolvedExchange = await ResolveExchangeAsync(exchange, cancellationToken);

        return await ExecuteAsync(
            "analyse_market",
            symbol,
            timeframe,
            () => _sender.Send(
                new AnalyseMarketQuery(symbol, timeframe, resolvedExchange, cutoff),
                cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Returns the existing Phase 3 multi-timeframe result with its complete Phase 2 evidence.
    /// </summary>
    [McpServerTool(Name = "analyse_market_multi_timeframe", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns TradePilot's deterministic multi-timeframe analysis for two or more requested timeframes, including the complete Phase 2 evidence per timeframe and existing alignment/conflict facts. It does not recalculate or reinterpret those facts.")]
    public async Task<MultiTimeframeMarketAnalysisResult> AnalyseMarketMultiTimeframeAsync(
        [Description("Exchange-facing market symbol understood by TradePilot, such as BTC or BTC-PERP.")] string symbol,
        [Description("Two or more timeframe values. TradePilot Phase 3 owns canonicalization, ordering, duplicate handling, validation, and failure semantics.")] string[] timeframes,
        [Description("Optional exchange selector. When omitted, TradePilot uses the authenticated user's selected exchange.")] Exchange? exchange = null,
        [Description("Optional shared UTC cutoff passed unchanged to the existing Phase 3 capability.")] DateTimeOffset? cutoff = null,
        CancellationToken cancellationToken = default)
    {
        RequireValue(symbol, nameof(symbol));
        if (timeframes is null)
        {
            throw new McpProtocolException("The timeframes parameter is required.", McpErrorCode.InvalidParams);
        }

        var resolvedExchange = await ResolveExchangeAsync(exchange, cancellationToken);

        return await ExecuteAsync(
            "analyse_market_multi_timeframe",
            symbol,
            string.Join(',', timeframes),
            () => _sender.Send(
                new AnalyseMarketMultiTimeframeQuery(symbol, timeframes, resolvedExchange, cutoff),
                cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Returns the authenticated user's existing Phase 1 account-summary result.
    /// </summary>
    [McpServerTool(Name = "get_account_summary", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns TradePilot's current read-only derivatives account summary for the authenticated user's configured exchange account. Account data is sensitive and no orders or account changes are performed.")]
    public async Task<AccountSummaryDto> GetAccountSummaryAsync(
        ClaimsPrincipal user,
        [Description("Optional exchange selector. When omitted, TradePilot uses the authenticated user's selected exchange.")] Exchange? exchange = null,
        CancellationToken cancellationToken = default)
    {
        var context = await ResolveAccountContextAsync(user, exchange, cancellationToken);

        return await ExecuteAsync(
            "get_account_summary",
            null,
            null,
            () => _sender.Send(new GetAccountSummaryQuery(context.Exchange, context.WalletAddress), cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Returns the authenticated user's existing Phase 1 open-position result.
    /// </summary>
    [McpServerTool(Name = "get_positions", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns TradePilot's current open positions for the authenticated user's configured exchange account. This is read-only and does not close or modify positions.")]
    public async Task<IReadOnlyList<PositionDto>> GetPositionsAsync(
        ClaimsPrincipal user,
        [Description("Optional exchange selector. When omitted, TradePilot uses the authenticated user's selected exchange.")] Exchange? exchange = null,
        CancellationToken cancellationToken = default)
    {
        var context = await ResolveAccountContextAsync(user, exchange, cancellationToken);

        return await ExecuteAsync(
            "get_positions",
            null,
            null,
            () => _sender.Send(new GetOpenPositionsQuery(context.Exchange, context.WalletAddress), cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Returns the authenticated user's existing Phase 1 open-order result.
    /// </summary>
    [McpServerTool(Name = "get_open_orders", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns TradePilot's active orders for the authenticated user's configured exchange account. This is read-only and cannot place, cancel, or alter orders.")]
    public async Task<IReadOnlyList<OpenOrderDto>> GetOpenOrdersAsync(
        ClaimsPrincipal user,
        [Description("Optional exchange selector. When omitted, TradePilot uses the authenticated user's selected exchange.")] Exchange? exchange = null,
        CancellationToken cancellationToken = default)
    {
        var context = await ResolveAccountContextAsync(user, exchange, cancellationToken);

        return await ExecuteAsync(
            "get_open_orders",
            null,
            null,
            () => _sender.Send(new GetOpenOrdersQuery(context.Exchange, context.WalletAddress), cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Returns the authenticated user's existing Phase 1 recent-fill result.
    /// </summary>
    [McpServerTool(Name = "get_recent_fills", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Returns TradePilot's recent executed fills for the authenticated user's configured exchange account, optionally filtered by exchange-facing asset symbol. This is read-only.")]
    public async Task<IReadOnlyList<FillEventDto>> GetRecentFillsAsync(
        ClaimsPrincipal user,
        [Description("Optional exchange-facing asset symbol. Omit it to use the existing capability's unfiltered behaviour.")] string? symbol = null,
        [Description("Optional exchange selector. When omitted, TradePilot uses the authenticated user's selected exchange.")] Exchange? exchange = null,
        CancellationToken cancellationToken = default)
    {
        var context = await ResolveAccountContextAsync(user, exchange, cancellationToken);

        return await ExecuteAsync(
            "get_recent_fills",
            symbol,
            null,
            () => _sender.Send(
                new GetRecentFillsQuery(context.Exchange, symbol, context.WalletAddress),
                cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Resolves an optional MCP exchange parameter through the existing per-user exchange selection.
    /// </summary>
    private Task<Exchange> ResolveExchangeAsync(Exchange? exchange, CancellationToken cancellationToken)
    {
        return exchange.HasValue
            ? Task.FromResult(exchange.Value)
            : _exchangeResolver.GetCurrentExchangeAsync(cancellationToken);
    }

    /// <summary>
    /// Maps authenticated HTTP identity to the existing account query inputs without accessing an exchange directly.
    /// </summary>
    private async Task<AccountContext> ResolveAccountContextAsync(
        ClaimsPrincipal user,
        Exchange? requestedExchange,
        CancellationToken cancellationToken)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new McpException("Account configuration is invalid: the authenticated user identifier is unavailable.");
        }

        var exchange = await ResolveExchangeAsync(requestedExchange, cancellationToken);
        if (exchange == Exchange.Hyperliquid)
        {
            var wallet = await _walletRepository.GetActiveByUserIdAndExchangeAsync(
                userId,
                exchange,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(wallet?.WalletAddress))
            {
                throw new McpException("Account configuration is invalid: no active Hyperliquid wallet is configured.");
            }

            return new AccountContext(exchange, wallet.WalletAddress);
        }

        if (exchange == Exchange.Binance)
        {
            var credentials = await _credentialRepository.GetActiveByUserIdAndExchangeAsync(
                userId,
                exchange,
                cancellationToken);
            if (credentials is null)
            {
                throw new McpException("Account configuration is invalid: no active Binance credentials are configured.");
            }

            return new AccountContext(exchange, null);
        }

        throw new McpException($"Account configuration is invalid: exchange '{exchange}' is unsupported.");
    }

    /// <summary>
    /// Runs one application query with safe MCP error mapping and structured operational logging.
    /// </summary>
    private async Task<T> ExecuteAsync<T>(
        string toolName,
        string? symbol,
        string? timeframe,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await action();
            _logger.LogInformation(
                "MCP tool {ToolName} succeeded for {Symbol} {Timeframe} in {DurationMs} ms",
                toolName,
                symbol,
                timeframe,
                stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("MCP tool {ToolName} was cancelled", toolName);
            throw;
        }
        catch (DomainException exception)
        {
            _logger.LogWarning("MCP tool {ToolName} rejected the application request", toolName);
            throw new McpException($"TradePilot rejected the request: {exception.Message}");
        }
        catch (NotFoundException exception)
        {
            _logger.LogWarning("MCP tool {ToolName} could not find the requested data", toolName);
            throw new McpException($"TradePilot data was not found: {exception.Message}");
        }
        catch (ExchangeApiException exception)
        {
            _logger.LogWarning(
                "MCP tool {ToolName} failed in upstream category {FailureCategory}",
                toolName,
                exception.ErrorCategory);
            throw new McpException($"Upstream exchange data is unavailable ({exception.ErrorCategory}).");
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogError(
                "MCP tool {ToolName} encountered invalid application configuration ({ExceptionType})",
                toolName,
                exception.GetType().Name);
            throw new McpException("TradePilot is not configured for the requested capability.");
        }
        catch (Exception exception) when (exception is not McpException)
        {
            _logger.LogError(
                "MCP tool {ToolName} failed unexpectedly ({ExceptionType})",
                toolName,
                exception.GetType().Name);
            throw new McpException("TradePilot could not complete the requested capability.");
        }
    }

    /// <summary>
    /// Rejects missing required string inputs as MCP invalid-parameter protocol errors.
    /// </summary>
    private static void RequireValue(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new McpProtocolException(
                $"The {parameterName} parameter is required.",
                McpErrorCode.InvalidParams);
        }
    }

    private sealed record AccountContext(Exchange Exchange, string? WalletAddress);
}
