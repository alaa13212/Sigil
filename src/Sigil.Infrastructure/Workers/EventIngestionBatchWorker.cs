using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sigil.Application.Interfaces;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Infrastructure.Workers;

internal class EventIngestionWorker(
    IServiceProvider services,
    IDigestionSignal signal,
    IOptions<BatchWorkersConfig> options,
    ILogger<EventIngestionWorker> logger)
    : BatchWorker<IngestionJobItem>(options.Value.GetOptions("EventIngestion"), logger), IEventIngestionWorker
{
    protected override async Task ProcessBatchAsync(List<IngestionJobItem> batch, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var rawEnvelopeService = scope.ServiceProvider.GetRequiredService<IRawEnvelopeService>();

        await rawEnvelopeService.BulkStoreAsync(
            batch.Select(item => (item.ProjectId, item.RawEnvelope, item.ReceivedAt)));

        signal.Signal();
    }
}
