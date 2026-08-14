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

    public AnalystController(ITradingAnalyst tradingAnalyst, IdentityService identityService)
    {
        _tradingAnalyst = tradingAnalyst;
        _identityService = identityService;
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
        if (!TryCreateContext(request.Context, out var context))
        {
            return BadRequest("The Analyst context is not valid.");
        }

        var result = await _tradingAnalyst.AnalyseAsync(
            new TradingAnalystRequest(CreateQuestion(request.Question, context), userId, Context: context),
            cancellationToken);

        return Ok(result);
    }

    private static bool TryCreateContext(AnalystRequestContext? requestContext, out TradingAnalystContext? context)
    {
        context = null;
        if (requestContext is null)
        {
            return true;
        }

        if (!Enum.TryParse<TradingAnalystIntent>(requestContext.Intent, true, out var intent))
        {
            return false;
        }

        if (intent is TradingAnalystIntent.ExplainStrategyEntry or TradingAnalystIntent.SummariseStrategyBlockingRules)
        {
            if (!requestContext.StrategyId.HasValue || requestContext.BacktestRunId.HasValue)
            {
                return false;
            }
        }

        if (intent is TradingAnalystIntent.AnalyseBacktestRun or TradingAnalystIntent.CompareBacktestRuns)
        {
            if (!requestContext.BacktestRunId.HasValue || requestContext.StrategyId.HasValue)
            {
                return false;
            }
        }

        context = new TradingAnalystContext(intent, requestContext.StrategyId, requestContext.StrategyVersion, requestContext.BacktestRunId);
        return true;
    }

    private static string CreateQuestion(string question, TradingAnalystContext? context)
    {
        if (context is null)
        {
            return question;
        }

        return context.Intent switch
        {
            TradingAnalystIntent.ExplainStrategyEntry =>
                $"Explain why strategy {context.StrategyId} did or did not enter. Use its latest evaluation evidence.",
            TradingAnalystIntent.SummariseStrategyBlockingRules =>
                $"Identify which rules most often block setups for strategy {context.StrategyId}.",
            TradingAnalystIntent.AnalyseBacktestRun =>
                $"Analyse persisted backtest run {context.BacktestRunId} using its available evidence.",
            TradingAnalystIntent.CompareBacktestRuns =>
                $"Compare persisted backtest run {context.BacktestRunId} with its available evidence.",
            _ => question
        };
    }
}

public sealed record AnalystQuestionRequest(string Question, AnalystRequestContext? Context = null);

public sealed record AnalystRequestContext(
    string Intent,
    Guid? StrategyId = null,
    int? StrategyVersion = null,
    Guid? BacktestRunId = null);