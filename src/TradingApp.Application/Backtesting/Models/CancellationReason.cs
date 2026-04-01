namespace TradingApp.Application.Backtesting.Models;

public enum CancellationReason
{
    GridRedeployed,
    TakeProfitTriggered,
    StopLossTriggered,
    ManualCancel
}