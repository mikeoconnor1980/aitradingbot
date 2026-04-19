using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TradePilot.Api.Infrastructure;
using TradePilot.Api.Models;
using TradePilot.Application.Abstractions.Exceptions;
using TradePilot.Application.Abstractions.Models;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.Backtesting;
using TradePilot.Application.StrategyAuthoring.Commands;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.StrategyAuthoring.Queries;
using TradePilot.Application.Subscriptions.Services;
using TradePilot.Application.StrategyAuthoring.Validation;
using TradePilot.Domain.Subscriptions;

namespace TradePilot.Api.Controllers;

[Route("api/strategies")]
public sealed class StrategiesController : ApiController
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly IStrategyValidator _validator;
    private readonly IAdminAuthorizationService _adminAuthorizationService;
    private readonly ISubscriptionFeatureService _subscriptionFeatureService;
    private readonly IStrategyTemplateRepository _strategyTemplateRepository;
    private readonly StrategyTierConstraintValidator _strategyTierConstraintValidator;

    public StrategiesController(
        IMediator mediator,
        IdentityService identityService,
        IStrategyRepository strategyRepository,
        IStrategyValidator validator,
        IAdminAuthorizationService adminAuthorizationService,
        ISubscriptionFeatureService subscriptionFeatureService,
        IStrategyTemplateRepository strategyTemplateRepository,
        StrategyTierConstraintValidator strategyTierConstraintValidator)
        : base(mediator, identityService)
    {
        _strategyRepository = strategyRepository;
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _adminAuthorizationService = adminAuthorizationService;
        _subscriptionFeatureService = subscriptionFeatureService;
        _strategyTemplateRepository = strategyTemplateRepository;
        _strategyTierConstraintValidator = strategyTierConstraintValidator;
    }

    [HttpPost("validate")]
    [ProducesResponseType(typeof(ValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Validate([FromBody] StrategyConfig config, CancellationToken cancellationToken)
    {
        var result = _validator.Validate(config);
        if (result.IsValid)
        {
            try
            {
                await _strategyTierConstraintValidator.ValidateAsync(
                    IdentityService.Identity,
                    config,
                    templateIsBeginnerVisible: true,
                    cancellationToken);
            }
            catch (DomainException ex)
            {
                result.Add(new ValidationError
                {
                    Severity = ValidationSeverity.Error,
                    FieldPath = "subscription",
                    Code = "subscription_restricted",
                    Message = ex.Message
                });
            }
        }

        return Ok(result);
    }

    [HttpGet("templates")]
    [ProducesResponseType(typeof(IReadOnlyList<StrategyTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken)
    {
        var includeAll = await _adminAuthorizationService.IsAdminAsync(IdentityService.Identity.Email, cancellationToken);
        var templates = await Mediator.Send(new GetStrategyTemplatesQuery(IdentityService.Identity, includeAll), cancellationToken);
        return Ok(templates);
    }

    [HttpPatch("templates/{templateId:guid}/beginner-visibility")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetBeginnerVisibility(
        Guid templateId,
        [FromBody] SetBeginnerVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _adminAuthorizationService.IsAdminAsync(IdentityService.Identity.Email, cancellationToken))
        {
            throw new UnauthorizedAccessException("Administrator access is required.");
        }

        var template = await _strategyTemplateRepository.GetByIdForUpdateAsync(templateId, cancellationToken);
        if (template is null || !template.IsActive)
        {
            throw new NotFoundException(nameof(Domain.Entities.StrategyTemplate), templateId);
        }

        if (request.Visible)
        {
            var visibleCount = await _strategyTemplateRepository.CountBeginnerVisibleAsync(cancellationToken);
            if (!template.IsBeginnerVisible && visibleCount >= 2)
            {
                throw new DomainException("Beginner tier can only expose two strategy templates at a time.");
            }
        }

        template.SetBeginnerVisibility(request.Visible);
        await _strategyTemplateRepository.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("templates/{templateId:guid}/clone")]
    [ProducesResponseType(typeof(CreatedResultEnvelope), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloneTemplate(Guid templateId, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(
            new CreateStrategyFromTemplateCommand(templateId, IdentityService.Identity),
            cancellationToken);

        return CreatedAtAction(nameof(GetStrategy), new { id }, new CreatedResultEnvelope(id));
    }

    [HttpPost("{id:guid}/promote-template")]
    [ProducesResponseType(typeof(CreatedResultEnvelope), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PromoteTemplate(
        Guid id,
        [FromBody] PromoteStrategyTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var templateId = await Mediator.Send(
            new PromoteStrategyTemplateCommand(
                id,
                request.Name,
                request.Description,
                request.Tags,
                IdentityService.Identity),
            cancellationToken);

        return CreatedAtAction(nameof(GetTemplates), new { }, new CreatedResultEnvelope(templateId));
    }

    [HttpDelete("templates/{templateId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UnpublishTemplate(Guid templateId, CancellationToken cancellationToken)
    {
        await Mediator.Send(
            new UnpublishStrategyTemplateCommand(templateId, IdentityService.Identity),
            cancellationToken);

        return NoContent();
    }

    [HttpPatch("templates/{templateId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RenameTemplate(
        Guid templateId,
        [FromBody] RenameStrategyTemplateRequest request,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(
            new RenameStrategyTemplateCommand(
                templateId,
                request.Name,
                request.Description,
                IdentityService.Identity),
            cancellationToken);

        return NoContent();
    }

    [HttpPost("interpret")]
    [EnableRateLimiting("interpret-strategy")]
    [ProducesResponseType(typeof(StrategyIntentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> InterpretStrategy(
        [FromBody] InterpretStrategyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new InterpretStrategyCommand(request.Text),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<StrategySummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStrategies(CancellationToken cancellationToken)
    {
        var strategies = await Mediator.Send(new GetStrategiesQuery(IdentityService.Identity), cancellationToken);
        return Ok(strategies);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StrategyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStrategy(Guid id, CancellationToken cancellationToken)
    {
        var strategy = await Mediator.Send(new GetStrategyByIdQuery(id, IdentityService.Identity), cancellationToken);
        return Ok(strategy);
    }

    [HttpGet("{id:guid}/versions")]
    [ProducesResponseType(typeof(PagedResult<StrategyRevisionSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersions(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            throw new DomainException("page must be greater than or equal to 1");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            throw new DomainException("pageSize must be between 1 and 100");
        }

        var revisions = await Mediator.Send(
            new GetStrategyVersionsQuery(id, page, pageSize, IdentityService.Identity),
            cancellationToken);

        return Ok(revisions);
    }

    [HttpGet("{id:guid}/backtests")]
    [ProducesResponseType(typeof(PagedResult<BacktestSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBacktestsByStrategy(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            throw new DomainException("page must be greater than or equal to 1");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            throw new DomainException("pageSize must be between 1 and 100");
        }

        var result = await Mediator.Send(
            new GetBacktestsByStrategyQuery(id, page, pageSize, IdentityService.Identity),
            cancellationToken);
        var strategyNames = await GetStrategyNamesByIdAsync(result.Items.Select(summary => summary.StrategyId), cancellationToken);

        return Ok(new PagedResult<BacktestSummaryDto>
        {
            Items = result.Items
                .Select(summary => new BacktestSummaryDto
                {
                    Id = summary.Id,
                    Symbol = summary.Symbol,
                    Intervals = summary.Intervals,
                    StartDate = summary.StartDate,
                    EndDate = summary.EndDate,
                    TotalTrades = summary.TotalTrades,
                    WinRate = summary.WinRate,
                    TotalPnl = summary.TotalPnl,
                    MaxDrawdown = summary.MaxDrawdown,
                    CreatedAt = summary.CreatedAt,
                    StrategyId = summary.StrategyId,
                    StrategyRevisionId = summary.StrategyRevisionId,
                    StrategyName = summary.StrategyId.HasValue && strategyNames.TryGetValue(summary.StrategyId.Value, out var strategyName)
                        ? strategyName
                        : summary.StrategyName,
                })
                .ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        });
    }

    [HttpGet("{id:guid}/versions/{rev:int}")]
    [ProducesResponseType(typeof(StrategyRevisionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersion(Guid id, int rev, CancellationToken cancellationToken = default)
    {
        if (rev < 1)
        {
            throw new DomainException("rev must be greater than or equal to 1");
        }

        var revision = await Mediator.Send(
            new GetStrategyRevisionQuery(id, rev, IdentityService.Identity),
            cancellationToken);

        return Ok(revision);
    }

    [HttpPost("{id:guid}/versions/{rev:int}/review")]
    [EnableRateLimiting("review-strategy")]
    [ProducesResponseType(typeof(StrategyReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ReviewStrategy(
        Guid id,
        int rev,
        CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(IdentityService.Identity.UserId, out var userId)
            && !await _subscriptionFeatureService.CanAccessFeatureAsync(userId, Feature.AiReview, cancellationToken))
        {
            throw new UnauthorizedAccessException("This feature requires a Pro subscription.");
        }

        if (rev < 1)
        {
            throw new DomainException("rev must be greater than or equal to 1");
        }

        var review = await Mediator.Send(
            new RequestStrategyReviewCommand(id, rev, IdentityService.Identity),
            cancellationToken);

        return CreatedAtAction(nameof(GetReview), new { id, rev }, review);
    }

    [HttpGet("{id:guid}/versions/{rev:int}/review")]
    [ProducesResponseType(typeof(StrategyReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReview(
        Guid id,
        int rev,
        CancellationToken cancellationToken = default)
    {
        if (rev < 1)
        {
            throw new DomainException("rev must be greater than or equal to 1");
        }

        var review = await Mediator.Send(
            new GetStrategyReviewQuery(id, rev, IdentityService.Identity),
            cancellationToken);

        if (review is null)
        {
            return NotFound(new Envelope("No review found for this revision.", "not_found"));
        }

        return Ok(review);
    }

    [HttpGet("{id:guid}/diff")]
    [ProducesResponseType(typeof(StrategyDiffDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDiff(
        Guid id,
        [FromQuery] int from,
        [FromQuery] int to,
        CancellationToken cancellationToken = default)
    {
        if (from < 1)
        {
            throw new DomainException("from must be greater than or equal to 1");
        }

        if (to < 1)
        {
            throw new DomainException("to must be greater than or equal to 1");
        }

        var result = await Mediator.Send(
            new GetStrategyDiffQuery(id, from, to, IdentityService.Identity),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:guid}/versions/{rev:int}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RestoreVersion(
        Guid id,
        int rev,
        CancellationToken cancellationToken = default)
    {
        if (rev < 1)
        {
            throw new DomainException("rev must be greater than or equal to 1");
        }

        await Mediator.Send(
            new RestoreStrategyVersionCommand(id, rev, IdentityService.Identity),
            cancellationToken);

        return NoContent();
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatedResultEnvelope), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateStrategy([FromBody] StrategyConfig config, CancellationToken cancellationToken)
    {
        var id = await Mediator.Send(new CreateStrategyCommand(config, IdentityService.Identity), cancellationToken);
        return CreatedAtAction(nameof(GetStrategy), new { id }, new CreatedResultEnvelope(id));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStrategy(
        Guid id,
        [FromBody] StrategyConfig config,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new UpdateStrategyCommand(id, config, IdentityService.Identity), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Envelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStrategy(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteStrategyCommand(id, IdentityService.Identity), cancellationToken);
        return NoContent();
    }

    private async Task<Dictionary<Guid, string?>> GetStrategyNamesByIdAsync(
        IEnumerable<Guid?> strategyIds,
        CancellationToken cancellationToken)
    {
        var ids = strategyIds
            .Where(strategyId => strategyId.HasValue)
            .Select(strategyId => strategyId!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        var strategies = await _strategyRepository.GetByIdsAsync(ids, cancellationToken);

        return strategies.ToDictionary(
            strategy => strategy.Id,
            strategy => (string?)(strategy.IsActive
                ? strategy.Name
                : $"{strategy.Name} (deleted)"));
    }
}

public sealed record SetBeginnerVisibilityRequest(bool Visible);