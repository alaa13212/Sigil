using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;
using Sigil.infrastructure.Persistence;

namespace Sigil.infrastructure.Workers;

internal class EventIngestionWorker(
    IServiceProvider services,
    IOptions<BatchWorkersConfig> options,
    ILogger<EventIngestionWorker> logger)
    : BatchWorker<IngestionJobItem>(options.Value.GetOptions("EventIngestion"), logger), IEventIngestionWorker
{
    protected override async Task ProcessBatchAsync(List<IngestionJobItem> batch, CancellationToken cancellationToken)
    {
        using IServiceScope scope = services.CreateScope();
        IEventParser eventParser = scope.ServiceProvider.GetRequiredService<IEventParser>();
        IIngestionService ingestionService = scope.ServiceProvider.GetRequiredService<IIngestionService>();
        SigilDbContext dbContext = scope.ServiceProvider.GetRequiredService<SigilDbContext>();

        foreach (var grouping in batch.GroupBy(item => item.ProjectId, item => item.RawEnvelope))
        {
            try
            {
                List<ParsedEvent> parsedEvents = [];

                // Parse each envelope individually to isolate parsing errors
                foreach (var envelope in grouping)
                {
                    try
                    {
                        var events = eventParser.Parse(envelope);
                        parsedEvents.AddRange(events);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to parse envelope for ProjectId {ProjectId}. Envelope will be skipped", grouping.Key);
                    }
                }

                if (parsedEvents.Count > 0)
                {
                    await ingestionService.BulkIngest(grouping.Key, parsedEvents, cancellationToken);
                    dbContext.ChangeTracker.Clear();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ingest events for ProjectId {ProjectId}. {EventCount} events will be lost",
                    grouping.Key, grouping.Count());
            }
        }
    }
}