using TradePilot.Application.Abstractions.Commands;
using TradePilot.Application.Abstractions.Repositories;
using TradePilot.Application.Abstractions.Services;
using TradePilot.Application.FearGreed.Models;

namespace TradePilot.Application.FearGreed.Commands;

public sealed record BackfillFearGreedCommand : Command<FearGreedBackfillResultDto>;

public sealed class BackfillFearGreedCommandHandler
    : CommandHandler<BackfillFearGreedCommand, FearGreedBackfillResultDto>
{
    private readonly IFearGreedClient _client;
    private readonly IFearGreedReadingRepository _repository;

    public BackfillFearGreedCommandHandler(
        IFearGreedClient client,
        IFearGreedReadingRepository repository)
    {
        _client = client;
        _repository = repository;
    }

    public override async Task<FearGreedBackfillResultDto> Handle(
        BackfillFearGreedCommand request,
        CancellationToken cancellationToken)
    {
        var readings = await _client.FetchAsync(limit: 0, cancellationToken);

        if (readings.Count == 0)
        {
            return new FearGreedBackfillResultDto(Fetched: 0, Inserted: 0);
        }

        var countBefore = await _repository.GetCountAsync(cancellationToken);
        await _repository.BulkUpsertAsync(readings, cancellationToken);
        var countAfter = await _repository.GetCountAsync(cancellationToken);

        return new FearGreedBackfillResultDto(
            Fetched: readings.Count,
            Inserted: countAfter - countBefore);
    }
}
