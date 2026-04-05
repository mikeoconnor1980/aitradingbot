namespace TradingApp.Application.Backtesting.Models;

public enum CancellationReason
{
    GridRedeployed,
    TakeProfitTriggered,
    StopLossTriggered,
    TrailingStopTriggered,
    ManualCancel
}