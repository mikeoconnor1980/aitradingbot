using System.Collections.Concurrent;
using TradingApp.Application.Agent.Models;

namespace TradingApp.Application.Agent.Services;

/// <summary>
/// In-memory store that tracks connected agents, their heartbeats,
/// and queued commands. Singleton in the API process.
/// POC implementation — production would persist to DB.
/// </summary>
public sealed class AgentCommandStore
{
    private static readonly TimeSpan DisconnectedThreshold = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, AgentInfo> _agents = new();
    private readonly ConcurrentDictionary<string, AgentCommand> _pendingCommands = new();

    /// <summary>
    /// Queue a command for a specific agent.
    /// Overwrites any existing pending command (latest wins).
    /// </summary>
    public void EnqueueCommand(AgentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _pendingCommands[command.AgentId] = command;
    }

    /// <summary>
    /// Dequeue the pending command for an agent (called when the agent checks in).
    /// Returns null if no command is pending.
    /// </summary>
    public AgentCommand? DequeueCommand(string agentId)
    {
        _pendingCommands.TryRemove(agentId, out var command);
        return command;
    }

    /// <summary>
    /// Process a heartbeat from an agent: update or register the agent.
    /// </summary>
    public void ProcessHeartbeat(AgentHeartbeat heartbeat)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);

        _agents.AddOrUpdate(
            heartbeat.AgentId,
            _ => new AgentInfo
            {
                AgentId = heartbeat.AgentId,
                MachineName = heartbeat.MachineName,
                State = heartbeat.State,
                LastHeartbeat = heartbeat.TimestampUtc,
                ConnectedSince = heartbeat.TimestampUtc,
                WalletAddress = heartbeat.WalletAddress,
                ActiveStrategy = heartbeat.ActiveStrategy,
                LastError = heartbeat.LastError,
            },
            (_, existing) =>
            {
                existing.State = heartbeat.State;
                existing.LastHeartbeat = heartbeat.TimestampUtc;
                existing.WalletAddress = heartbeat.WalletAddress;
                existing.ActiveStrategy = heartbeat.ActiveStrategy;
                existing.LastError = heartbeat.LastError;
                return existing;
            });
    }

    /// <summary>
    /// Get all known agents, marking stale ones as Disconnected.
    /// </summary>
    public IReadOnlyList<AgentInfo> GetAllAgents()
    {
        var now = DateTimeOffset.UtcNow;
        var agents = _agents.Values.ToList();

        foreach (var agent in agents)
        {
            if (now - agent.LastHeartbeat > DisconnectedThreshold &&
                agent.State is not AgentState.Disconnected)
            {
                agent.State = AgentState.Disconnected;
            }
        }

        return agents;
    }

    /// <summary>
    /// Get a specific agent by ID.
    /// </summary>
    public AgentInfo? GetAgent(string agentId)
    {
        if (!_agents.TryGetValue(agentId, out var agent))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - agent.LastHeartbeat > DisconnectedThreshold &&
            agent.State is not AgentState.Disconnected)
        {
            agent.State = AgentState.Disconnected;
        }

        return agent;
    }
}
