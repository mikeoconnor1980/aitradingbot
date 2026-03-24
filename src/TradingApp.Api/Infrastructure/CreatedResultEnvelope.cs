namespace TradingApp.Api.Infrastructure;

public sealed class CreatedResultEnvelope
{
    public Guid Id { get; }

    public CreatedResultEnvelope(Guid id)
    {
        Id = id;
    }
}