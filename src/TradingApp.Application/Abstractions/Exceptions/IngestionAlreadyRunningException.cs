namespace TradingApp.Application.Abstractions.Exceptions;

public sealed class IngestionAlreadyRunningException : Exception
{
    public IngestionAlreadyRunningException()
        : base("Candle ingestion is already running.")
    {
    }
}