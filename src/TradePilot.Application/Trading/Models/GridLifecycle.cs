namespace TradePilot.Application.Trading.Models;

public enum GridLifecycle
{
    Inactive,
    Planning,
    Deploying,
    Active,
    PartiallyFilled,
    FullyFilled,
    Closing,
    Closed
}
