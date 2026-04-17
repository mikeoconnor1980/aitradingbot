namespace TradePilot.Application.Backtesting.Models;

public enum CancellationReason
{
    GridRedeployed,
    TakeProfitTriggered,
    StopLossTriggered,
    LiquidationTriggered,
    TrailingStopTriggered,
    ManualCancel
}