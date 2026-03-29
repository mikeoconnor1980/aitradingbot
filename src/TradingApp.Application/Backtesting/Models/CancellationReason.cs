namespace TradingApp.Application.Backtesting.Models;

public enum CancellationReason
{
    GridRedeployed,
    PositionOpened,
    StopLossTriggered,
    ManualCancel
}