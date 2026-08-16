using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Api.Infrastructure;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Analyst.Models;

namespace TradePilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/analyst")]
[Produces("application/json")]
public sealed class AnalystController : ControllerBase
{
    private readonly ITradingAnalyst _tradingAnalyst;
    private readonly IdentityService _identityService;
    private readonly IExchangeResolver _exchangeResolver;

    public AnalystController(
        ITradingAnalyst tradingAnalyst,
        IdentityService identityService,
        IExchangeResolver exchangeResolver)
    {
        _tradingAnalyst = tradingAnalyst;
        _identityService = identityService;
        _exchangeResolver = exchangeResolver;
    }

    [HttpPost("analyse")]
    [ProducesResponseType(typeof(TradingAnalystResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TradingAnalystResult>> AnalyseAsync(
        [FromBody] AnalystQuestionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required.");
        }

        Guid? userId = Guid.TryParse(_identityService.Identity.UserId, out var parsedUserId)
            ? parsedUserId
            : null;
        var context = await TryCreateContextAsync(request.Context, cancellationToken);
        if (context is null && request.Context is not null)
        {
            return BadRequest("The Analyst context is not valid.");
        }

        var result = await _tradingAnalyst.AnalyseAsync(
            new TradingAnalystRequest(request.Question.Trim(), userId, Context: context),
            cancellationToken);

        return Ok(result);
    }

    private async Task<TradingAnalystContext?> TryCreateContextAsync(
        AnalystRequestContext? requestContext,
        CancellationToken cancellationToken)
    {
        if (requestContext is null)
        {
            return null;
        }

        if (!Enum.TryParse<TradingAnalystIntent>(requestContext.Intent, true, out var intent))
        {
            return null;
        }

        if (intent is TradingAnalystIntent.ExplainStrategyEntry or TradingAnalystIntent.SummariseStrategyBlockingRules)
        {
            if (!requestContext.StrategyId.HasValue || requestContext.BacktestRunId.HasValue)
            {
                return null;
            }
        }

        if (intent is TradingAnalystIntent.AnalyseBacktestRun or TradingAnalystIntent.CompareBacktestRuns)
        {
            if (!requestContext.BacktestRunId.HasValue || requestContext.StrategyId.HasValue)
            {
                return null;
            }
        }

        if (intent == TradingAnalystIntent.AnalyseChart)
        {
            if (requestContext.Chart is null || requestContext.StrategyId.HasValue || requestContext.BacktestRunId.HasValue)
            {
                return null;
            }

            var chart = requestContext.Chart;
            if (string.IsNullOrWhiteSpace(chart.Symbol) || chart.Symbol.Trim().Length > 32 ||
                !TryParseUtc(chart.VisibleFromOpenTimeUtc, out var visibleFrom) ||
                !TryParseUtc(chart.VisibleToOpenTimeUtc, out var visibleTo) ||
                !TryParseOptionalUtc(chart.SelectedCandleOpenTimeUtc, out var selected) ||
                !TryParseUtc(chart.CapturedAtUtc, out var capturedAt) ||
                visibleFrom > visibleTo ||
                (selected.HasValue && (selected < visibleFrom || selected > visibleTo)) ||
                !TryParseIndicators(chart.ActiveIndicators, out var indicators) ||
                !TryParseOverlays(chart.VisibleOverlays, out var overlays))
            {
                return null;
            }

            try
            {
                var exchange = await _exchangeResolver.GetCurrentExchangeAsync(cancellationToken);
                return new TradingAnalystContext(
                    intent,
                    Chart: new TradingAnalystChartContext(
                        chart.Symbol.Trim().ToUpperInvariant(),
                        chart.Timeframe.Trim(),
                        exchange,
                        visibleFrom,
                        visibleTo,
                        selected,
                        indicators,
                        overlays,
                        capturedAt,
                        DateTimeOffset.UtcNow));
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        if (requestContext.Chart is not null)
        {
            return null;
        }

        return new TradingAnalystContext(intent, requestContext.StrategyId, requestContext.StrategyVersion, requestContext.BacktestRunId);
    }

    private static bool TryParseUtc(string? value, out DateTimeOffset result)
    {
        return DateTimeOffset.TryParse(value, out result) && result.Offset == TimeSpan.Zero;
    }

    private static bool TryParseOptionalUtc(string? value, out DateTimeOffset? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!TryParseUtc(value, out var parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }

    private static bool TryParseIndicators(IReadOnlyList<string>? values, out IReadOnlyList<ChartIndicatorId> indicators)
    {
        indicators = [];
        if (values is null || values.Count > 6 || !values.All(value => Enum.TryParse<ChartIndicatorId>(value, true, out _)))
        {
            return false;
        }

        indicators = values.Select(value => Enum.Parse<ChartIndicatorId>(value, true)).Distinct().ToArray();
        return true;
    }

    private static bool TryParseOverlays(IReadOnlyList<string>? values, out IReadOnlyList<ChartOverlayId> overlays)
    {
        overlays = [];
        if (values is null || values.Count > 1 || !values.All(value => Enum.TryParse<ChartOverlayId>(value, true, out _)))
        {
            return false;
        }

        overlays = values.Select(value => Enum.Parse<ChartOverlayId>(value, true)).Distinct().ToArray();
        return true;
    }
}

public sealed record AnalystQuestionRequest(string Question, AnalystRequestContext? Context = null);

public sealed record AnalystRequestContext(
    string Intent,
    Guid? StrategyId = null,
    int? StrategyVersion = null,
    Guid? BacktestRunId = null,
    AnalystChartContext? Chart = null);

public sealed record AnalystChartContext(
    string Symbol,
    string Timeframe,
    string VisibleFromOpenTimeUtc,
    string VisibleToOpenTimeUtc,
    string? SelectedCandleOpenTimeUtc,
    IReadOnlyList<string>? ActiveIndicators,
    IReadOnlyList<string>? VisibleOverlays,
    string CapturedAtUtc);