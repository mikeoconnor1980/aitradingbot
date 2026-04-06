using Microsoft.AspNetCore.Mvc;
using TradingApp.Application.Agent.Models;
using TradingApp.Application.Agent.Services;
using TradingApp.Application.StrategyAuthoring.Models;

namespace TradingApp.Api.Controllers;

/// <summary>
/// Dashboard endpoints for controlling trading on a specific agent.
/// </summary>
[ApiController]
[Route("api/trading")]
[Produces("application/json")]
public sealed class TradingController : ControllerBase
{
    private readonly AgentCommandStore _store;

    public TradingController(AgentCommandStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Start trading on a specific agent with the given strategy config.
    /// </summary>
    [HttpPost("{agentId}/start")]
    [ProducesResponseType(typeof(CommandAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Start(string agentId, [FromBody] StartTradingRequest request)
    {
        if (request.StrategyConfig is null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "StrategyConfig is required."
            });
        }

        var agent = _store.GetAgent(agentId);
        if (agent is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Agent not found",
                Detail = $"No agent registered with ID '{agentId}'. The agent must check in first."
            });
        }

        if (agent.State is AgentState.Running or AgentState.Starting)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Already trading",
                Detail = $"Agent '{agentId}' is already in state '{agent.State}'. Stop it first."
            });
        }

        var command = new AgentCommand
        {
            CommandId = Guid.NewGuid().ToString("N"),
            AgentId = agentId,
            Type = AgentCommandType.Start,
            StrategyConfig = request.StrategyConfig,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        _store.EnqueueCommand(command);

        return Accepted(new CommandAcceptedResponse(command.CommandId));
    }

    /// <summary>
    /// Stop trading on a specific agent (graceful shutdown).
    /// </summary>
    [HttpPost("{agentId}/stop")]
    [ProducesResponseType(typeof(CommandAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Stop(string agentId)
    {
        var agent = _store.GetAgent(agentId);
        if (agent is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Agent not found",
                Detail = $"No agent registered with ID '{agentId}'."
            });
        }

        var command = new AgentCommand
        {
            CommandId = Guid.NewGuid().ToString("N"),
            AgentId = agentId,
            Type = AgentCommandType.Stop,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        _store.EnqueueCommand(command);

        return Accepted(new CommandAcceptedResponse(command.CommandId));
    }

    /// <summary>
    /// Get the current trading status for a specific agent.
    /// </summary>
    [HttpGet("{agentId}/status")]
    [ProducesResponseType(typeof(AgentInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetStatus(string agentId)
    {
        var agent = _store.GetAgent(agentId);
        if (agent is null)
        {
            return NotFound();
        }

        return Ok(agent);
    }
}

public sealed record StartTradingRequest(StrategyConfig? StrategyConfig);

public sealed record CommandAcceptedResponse(string CommandId);
