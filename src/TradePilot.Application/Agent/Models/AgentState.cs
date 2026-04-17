namespace TradePilot.Application.Agent.Models;

public enum AgentState
{
    Idle,
    Starting,
    Running,
    Stopping,
    Error,
    Disconnected,
    Killed,
}
