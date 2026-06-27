using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Agent.Models;
using TradePilot.Application.Agent.Services;
using TradePilot.Application.Subscriptions.Services;
using TradePilot.Application.Webhooks.Models;
using TradePilot.Application.Webhooks.Services;
using TradePilot.Domain.Entities;
using TradePilot.Domain.Subscriptions;
using TradePilot.Persistence;

namespace TradePilot.Api.Controllers;

[ApiController]
[Route("api/webhooks/tradingview")]
[Produces("application/json")]
[EnableRateLimiting("tradingview-webhook")]
public sealed class WebhookController : ControllerBase
{
    private static readonly HashSet<string> TradingViewSourceIps =
    [
        "52.89.214.238",
        "34.212.75.30",
        "54.218.53.128",
        "52.32.178.7",
    ];

    private readonly IWebhookConfigRepository _webhookRepository;
    private readonly AgentCommandStore _commandStore;
    private readonly TradePilotDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly ISubscriptionFeatureService _subscriptionFeatureService;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IWebhookConfigRepository webhookRepository,
        AgentCommandStore commandStore,
        TradePilotDbContext db,
        IWebHostEnvironment environment,
        ISubscriptionFeatureService subscriptionFeatureService,
        ILogger<WebhookController> logger)
    {
        _webhookRepository = webhookRepository;
        _commandStore = commandStore;
        _db = db;
        _environment = environment;
        _subscriptionFeatureService = subscriptionFeatureService;
        _logger = logger;
    }

    [HttpPost("{token}")]
    [ProducesResponseType(typeof(CommandAcceptedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Receive(string token, [FromBody] TradingViewWebhookPayload payload, CancellationToken cancellationToken)
    {
        if (!IsAllowedSourceIp())
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var webhook = await _webhookRepository.GetByTokenAsync(token, cancellationToken);
        if (webhook is null || !webhook.IsEnabled)
        {
            return NotFound();
        }

        if (!await _subscriptionFeatureService.CanAccessFeatureAsync(webhook.UserId, Feature.Webhooks, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Pro subscription required",
                Detail = "TradingView webhooks require a Pro subscription."
            });
        }

        var agentId = await ResolveAgentIdAsync(webhook, cancellationToken);
        if (agentId is null)
        {
            _logger.LogWarning("TradingView webhook received for user {UserId} but no active agent was available.", webhook.UserId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Agent unavailable",
                Detail = "No connected execution agent is available for this webhook."
            });
        }

        AgentCommand command;
        try
        {
            command = WebhookCommandMapper.Map(payload, webhook, agentId);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid TradingView payload",
                Detail = ex.Message,
            });
        }

        _commandStore.EnqueueCommand(command);
        webhook.MarkTriggered();
        await _webhookRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Queued TradingView webhook command. UserId={UserId}, AgentId={AgentId}, Action={Action}, CommandId={CommandId}",
            webhook.UserId,
            agentId,
            payload.Action,
            command.CommandId);

        return Ok(new CommandAcceptedResponse(command.CommandId));
    }

    private bool IsAllowedSourceIp()
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (remoteIp is null)
        {
            return _environment.IsDevelopment();
        }

        var normalized = remoteIp.MapToIPv4();
        if (IPAddress.IsLoopback(normalized))
        {
            return _environment.IsDevelopment();
        }

        var ip = normalized.ToString();
        var forwarded = HttpContext.Request.Headers["X-Forwarded-For"].ToString();
        _logger.LogInformation("Webhook IP check: RemoteIp={RemoteIp}, X-Forwarded-For={ForwardedFor}", ip, forwarded);

        if (TradingViewSourceIps.Contains(ip))
        {
            return true;
        }

        if (_environment.IsDevelopment())
        {
            _logger.LogWarning("Allowing non-allowlisted TradingView webhook IP in development: {IpAddress}", ip);
            return true;
        }

        _logger.LogWarning("Rejected TradingView webhook from non-allowlisted IP: {IpAddress}", ip);
        return false;
    }

    private async Task<string?> ResolveAgentIdAsync(WebhookConfig webhook, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(webhook.TargetAgentId))
        {
            var pinnedAgent = _commandStore.GetAgent(webhook.TargetAgentId);
            if (pinnedAgent is not null && pinnedAgent.State is not AgentState.Disconnected and not AgentState.Killed)
            {
                return pinnedAgent.AgentId;
            }
        }

        var walletAddresses = await _db.UserWalletAddresses
            .Where(w => w.UserId == webhook.UserId && w.IsActive)
            .Select(w => w.WalletAddress)
            .ToListAsync(cancellationToken);

        if (walletAddresses.Count == 0)
        {
            return null;
        }

        var agents = _commandStore.GetAllAgents();
        var match = agents.FirstOrDefault(agent =>
            agent.State is not AgentState.Disconnected and not AgentState.Killed &&
            !string.IsNullOrWhiteSpace(agent.WalletAddress) &&
            walletAddresses.Any(wallet => string.Equals(wallet, agent.WalletAddress, StringComparison.OrdinalIgnoreCase)));

        return match?.AgentId;
    }
}