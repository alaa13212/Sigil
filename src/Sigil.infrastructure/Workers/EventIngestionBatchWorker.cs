using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sigil.Application.Interfaces;
using Sigil.Domain.Enums;
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
        IFailedEventService failedEventService = scope.ServiceProvider.GetRequiredService<IFailedEventService>();
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
                        logger.LogError(ex, "Failed to parse envelope for ProjectId {ProjectId}. Storing as failed event", grouping.Key);
                        await StoreFailedEventSafe(failedEventService, grouping.Key, envelope, FailedEventStage.Parsing, ex);
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
                logger.LogError(ex, "Failed to ingest events for ProjectId {ProjectId}. {EventCount} events will be stored as failed",
                    grouping.Key, grouping.Count());

                foreach (var envelope in grouping)
                {
                    await StoreFailedEventSafe(failedEventService, grouping.Key, envelope, FailedEventStage.Ingestion, ex);
                }
            }
        }
    }

    private async Task StoreFailedEventSafe(
        IFailedEventService failedEventService, int projectId, string rawEnvelope,
        FailedEventStage stage, Exception exception)
    {
        try
        {
            await failedEventService.StoreAsync(projectId, rawEnvelope, stage, exception);
        }
        catch (Exception storeEx)
        {
            logger.LogError(storeEx, "Failed to store failed event for ProjectId {ProjectId}. Event data will be lost", projectId);
        }
    }
}
