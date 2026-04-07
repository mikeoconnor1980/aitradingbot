namespace TradingApp.Application.Trading.Models;

/// <summary>
/// Tracks exchange-native TP/SL trigger orders placed as position protection.
/// In-memory only — not persisted to DB. Rebuilt from exchange on worker restart.
/// </summary>
public sealed class ProtectionOrderState
{
    public string? StopLossOrderId { get; set; }
    public decimal? StopLossTriggerPrice { get; set; }
    public string? TakeProfitOrderId { get; set; }
    public decimal? TakeProfitTriggerPrice { get; set; }
    public DateTime? LastUpdatedAtUtc { get; set; }

    public bool HasStopLoss => !string.IsNullOrEmpty(StopLossOrderId);
    public bool HasTakeProfit => !string.IsNullOrEmpty(TakeProfitOrderId);
    public bool HasAny => HasStopLoss || HasTakeProfit;

    public bool IsProtectionOrderId(string orderId)
    {
        return string.Equals(orderId, StopLossOrderId, StringComparison.Ordinal)
            || string.Equals(orderId, TakeProfitOrderId, StringComparison.Ordinal);
    }

    public void Clear()
    {
        StopLossOrderId = null;
        StopLossTriggerPrice = null;
        TakeProfitOrderId = null;
        TakeProfitTriggerPrice = null;
        LastUpdatedAtUtc = null;
    }
}
