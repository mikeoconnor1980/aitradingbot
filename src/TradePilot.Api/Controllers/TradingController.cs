using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradePilot.Application.Agent.Models;
using TradePilot.Application.Agent.Services;
using TradePilot.Application.StrategyAuthoring.Models;
using TradePilot.Application.Trading;

namespace TradePilot.Api.Controllers;

/// <summary>
/// Dashboard endpoints for controlling trading on a specific agent.
/// </summary>
[ApiController]
[Route("api/trading")]
[Produces("application/json")]
[Authorize]
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

        if (!LiveTradingSupport.TryValidate(request.StrategyConfig, out var unsupportedReason))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported live strategy",
                Detail = unsupportedReason,
            });
        }

        var agent = ValidateAgent(agentId);
        if (agent is null)
        {
            return NotFound(AgentNotFoundProblem(agentId));
        }

        if (!IsAgentReachable(agent, out var offline))
        {
            return offline!;
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
        var agent = ValidateAgent(agentId);
        if (agent is null)
        {
            return NotFound(AgentNotFoundProblem(agentId));
        }

        if (!IsAgentReachable(agent, out var offline))
        {
            return offline!;
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

    /// <summary>
    /// Route an order through to the agent for execution.
    /// The agent will execute it on the next heartbeat check-in.
    /// </summary>
    [HttpPost("{agentId}/order")]
    [ProducesResponseType(typeof(CommandAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult PlaceOrder(string agentId, [FromBody] OrderCommandPayload payload)
    {
        var agent = ValidateAgent(agentId);
        if (agent is null) return NotFound(AgentNotFoundProblem(agentId));
        if (!IsAgentReachable(agent, out var offline)) return offline!;

        var command = new AgentCommand
        {
            CommandId = Guid.NewGuid().ToString("N"),
            AgentId = agentId,
            Type = AgentCommandType.PlaceOrder,
            OrderPayload = payload,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        _store.EnqueueCommand(command);
        return Accepted(new CommandAcceptedResponse(command.CommandId));
    }

    /// <summary>
    /// Route a cancel-order command through to the agent.
    /// </summary>
    [HttpPost("{agentId}/cancel-order")]
    [ProducesResponseType(typeof(CommandAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult CancelOrder(string agentId, [FromBody] CancelOrderPayload payload)
    {
        var agent = ValidateAgent(agentId);
        if (agent is null) return NotFound(AgentNotFoundProblem(agentId));
        if (!IsAgentReachable(agent, out var offline)) return offline!;

        var command = new AgentCommand
        {
            CommandId = Guid.NewGuid().ToString("N"),
            AgentId = agentId,
            Type = AgentCommandType.CancelOrder,
            CancelPayload = payload,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        _store.EnqueueCommand(command);
        return Accepted(new CommandAcceptedResponse(command.CommandId));
    }

    /// <summary>
    /// Route a cancel-all-orders command through to the agent.
    /// </summary>
    [HttpPost("{agentId}/cancel-all-orders")]
    [ProducesResponseType(typeof(CommandAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult CancelAllOrders(string agentId, [FromBody] CancelAllOrdersPayload payload)
    {
        var agent = ValidateAgent(agentId);
        if (agent is null) return NotFound(AgentNotFoundProblem(agentId));
        if (!IsAgentReachable(agent, out var offline)) return offline!;

        var command = new AgentCommand
        {
            CommandId = Guid.NewGuid().ToString("N"),
            AgentId = agentId,
            Type = AgentCommandType.CancelAllOrders,
            CancelAllPayload = payload,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        _store.EnqueueCommand(command);
        return Accepted(new CommandAcceptedResponse(command.CommandId));
    }

    /// <summary>
    /// Route a set-leverage command through to the agent.
    /// </summary>
    [HttpPost("{agentId}/leverage")]
    [ProducesResponseType(typeof(CommandAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult SetLeverage(string agentId, [FromBody] SetLeveragePayload payload)
    {
        var agent = ValidateAgent(agentId);
        if (agent is null) return NotFound(AgentNotFoundProblem(agentId));
        if (!IsAgentReachable(agent, out var offline)) return offline!;

        var command = new AgentCommand
        {
            CommandId = Guid.NewGuid().ToString("N"),
            AgentId = agentId,
            Type = AgentCommandType.SetLeverage,
            LeveragePayload = payload,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        _store.EnqueueCommand(command);
        return Accepted(new CommandAcceptedResponse(command.CommandId));
    }

    /// <summary>
    /// Route a trigger order (SL/TP) through to the agent.
    /// </summary>
    [HttpPost("{agentId}/trigger-order")]
    [ProducesResponseType(typeof(CommandAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult PlaceTriggerOrder(string agentId, [FromBody] TriggerOrderPayload payload)
    {
        var agent = ValidateAgent(agentId);
        if (agent is null) return NotFound(AgentNotFoundProblem(agentId));
        if (!IsAgentReachable(agent, out var offline)) return offline!;

        var command = new AgentCommand
        {
            CommandId = Guid.NewGuid().ToString("N"),
            AgentId = agentId,
            Type = AgentCommandType.PlaceTriggerOrder,
            TriggerPayload = payload,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        _store.EnqueueCommand(command);
        return Accepted(new CommandAcceptedResponse(command.CommandId));
    }

    /// <summary>
    /// Route a modify-trigger-order command through to the agent.
    /// </summary>
    [HttpPost("{agentId}/modify-trigger-order")]
    [ProducesResponseType(typeof(CommandAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult ModifyTriggerOrder(string agentId, [FromBody] ModifyTriggerOrderPayload payload)
    {
        var agent = ValidateAgent(agentId);
        if (agent is null) return NotFound(AgentNotFoundProblem(agentId));
        if (!IsAgentReachable(agent, out var offline)) return offline!;

        var command = new AgentCommand
        {
            CommandId = Guid.NewGuid().ToString("N"),
            AgentId = agentId,
            Type = AgentCommandType.ModifyTriggerOrder,
            ModifyTriggerPayload = payload,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        _store.EnqueueCommand(command);
        return Accepted(new CommandAcceptedResponse(command.CommandId));
    }

    /// <summary>
    /// Route a cancel-trigger-order command through to the agent.
    /// Reuses CancelOrderPayload (orderId + asset).
    /// </summary>
    [HttpPost("{agentId}/cancel-trigger-order")]
    [ProducesResponseType(typeof(CommandAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult CancelTriggerOrder(string agentId, [FromBody] CancelOrderPayload payload)
    {
        var agent = ValidateAgent(agentId);
        if (agent is null) return NotFound(AgentNotFoundProblem(agentId));
        if (!IsAgentReachable(agent, out var offline)) return offline!;

        var command = new AgentCommand
        {
            CommandId = Guid.NewGuid().ToString("N"),
            AgentId = agentId,
            Type = AgentCommandType.CancelTriggerOrder,
            CancelPayload = payload,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        _store.EnqueueCommand(command);
        return Accepted(new CommandAcceptedResponse(command.CommandId));
    }

    private AgentInfo? ValidateAgent(string agentId) => _store.GetAgent(agentId);

    private bool IsAgentReachable(AgentInfo agent, out IActionResult? errorResult)
    {
        if (agent.State is AgentState.Disconnected)
        {
            errorResult = Conflict(new ProblemDetails
            {
                Title = "Agent offline",
                Detail = $"Agent '{agent.AgentId}' is disconnected. Commands cannot be delivered.",
                Status = StatusCodes.Status409Conflict,
            });
            return false;
        }

        errorResult = null;
        return true;
    }

    private static ProblemDetails AgentNotFoundProblem(string agentId) => new()
    {
        Title = "Agent not found",
        Detail = $"No agent registered with ID '{agentId}'. The agent must check in first."
    };
}

public sealed record StartTradingRequest(StrategyConfig? StrategyConfig);

public sealed record CommandAcceptedResponse(string CommandId);
