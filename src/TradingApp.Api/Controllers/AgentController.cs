using Microsoft.AspNetCore.Mvc;
using TradingApp.Application.Agent.Models;
using TradingApp.Application.Agent.Services;

namespace TradingApp.Api.Controllers;

/// <summary>
/// Endpoints called by the Worker (execution agent) during check-in.
/// The Worker polls POST /heartbeat periodically and receives any pending command.
/// </summary>
[ApiController]
[Route("api/agent")]
[Produces("application/json")]
public sealed class AgentController : ControllerBase
{
    private readonly AgentCommandStore _store;

    public AgentController(AgentCommandStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Agent heartbeat. Worker posts its state; API returns any pending command.
    /// </summary>
    [HttpPost("heartbeat")]
    [ProducesResponseType(typeof(HeartbeatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult Heartbeat([FromBody] AgentHeartbeat heartbeat)
    {
        if (string.IsNullOrWhiteSpace(heartbeat.AgentId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid heartbeat",
                Detail = "AgentId is required."
            });
        }

        _store.ProcessHeartbeat(heartbeat);

        // Check kill switch — if killed, tell the agent to shut down
        var killReason = _store.GetKillReason(heartbeat.AgentId);
        if (killReason is not null)
        {
            return Ok(new HeartbeatResponse
            {
                PendingCommands = [],
                MustShutdown = true,
                ShutdownReason = killReason,
            });
        }

        var pendingCommands = _store.DrainCommands(heartbeat.AgentId);

        return Ok(new HeartbeatResponse { PendingCommands = pendingCommands });
    }

    /// <summary>
    /// List all connected agents (called by the dashboard).
    /// </summary>
    [HttpGet("list")]
    [ProducesResponseType(typeof(IReadOnlyList<AgentInfo>), StatusCodes.Status200OK)]
    public IActionResult ListAgents()
    {
        return Ok(_store.GetAllAgents());
    }

    /// <summary>
    /// Get a specific agent's details (called by the dashboard).
    /// </summary>
    [HttpGet("{agentId}")]
    [ProducesResponseType(typeof(AgentInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetAgent(string agentId)
    {
        var agent = _store.GetAgent(agentId);
        if (agent is null)
        {
            return NotFound();
        }

        return Ok(agent);
    }

    /// <summary>
    /// Get pending commands queued for a specific agent.
    /// </summary>
    [HttpGet("{agentId}/pending-commands")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingCommandDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetPendingCommands(string agentId)
    {
        var agent = _store.GetAgent(agentId);
        if (agent is null)
        {
            return NotFound();
        }

        var commands = _store.GetPendingCommands(agentId)
            .Select(c => new PendingCommandDto
            {
                CommandId = c.CommandId,
                Type = c.Type,
                CreatedAtUtc = c.CreatedAtUtc,
            })
            .ToList();

        return Ok(commands);
    }

    /// <summary>
    /// Kill an agent. Forces shutdown and prevents reconnection until reinstated.
    /// Optionally schedule the kill at a future date/time.
    /// </summary>
    [HttpPost("{agentId}/kill")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Kill(string agentId, [FromBody] KillAgentRequest request)
    {
        if (!_store.KillAgent(agentId, request.Reason, request.EffectiveAtUtc))
        {
            return NotFound(new ProblemDetails
            {
                Title = "Agent not found",
                Detail = $"No agent registered with ID '{agentId}'."
            });
        }

        return Ok(new { message = $"Agent '{agentId}' kill switch activated." });
    }

    /// <summary>
    /// Reinstate a killed agent, allowing it to reconnect.
    /// </summary>
    [HttpPost("{agentId}/reinstate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Reinstate(string agentId)
    {
        if (!_store.ReinstateAgent(agentId))
        {
            return NotFound(new ProblemDetails
            {
                Title = "Agent not found",
                Detail = $"No agent registered with ID '{agentId}'."
            });
        }

        return Ok(new { message = $"Agent '{agentId}' reinstated. It may reconnect." });
    }
}

public sealed class PendingCommandDto
{
    public required string CommandId { get; init; }
    public required AgentCommandType Type { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class KillAgentRequest
{
    /// <summary>Reason for killing the agent (shown to operator).</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// When to kill. Null = immediately. Set to a future UTC date/time
    /// to schedule (e.g. subscription expiry).
    /// </summary>
    public DateTimeOffset? EffectiveAtUtc { get; init; }
}
