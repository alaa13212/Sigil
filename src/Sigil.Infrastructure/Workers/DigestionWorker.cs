using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Interfaces;
using Sigil.Domain.Ingestion;
using Sigil.infrastructure.Persistence;

namespace Sigil.infrastructure.Workers;

internal class DigestionWorker(
    IServiceProvider services,
    IDigestionSignal signal,
    IOptions<BatchWorkersConfig> options,
    ILogger<DigestionWorker> logger) : IWorker
{
    private readonly int _batchSize = options.Value.GetOptions(nameof(DigestionWorker)).BatchSize;
    private readonly TimeSpan _maxSignalWaitTime = options.Value.GetOptions(nameof(DigestionWorker)).FlushTimeout;

    public async Task RunAsync(CancellationToken stoppingToken = default)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DigestionWorker encountered an error. Retrying after delay");
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            await signal.WaitAsync(_maxSignalWaitTime, stoppingToken);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var rawEnvelopeService = scope.ServiceProvider.GetRequiredService<IRawEnvelopeService>();
        var parser = scope.ServiceProvider.GetRequiredService<IEventParser>();
        var digestionService = scope.ServiceProvider.GetRequiredService<IDigestionService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<SigilDbContext>();

        List<RawEnvelope> batch = await rawEnvelopeService.FetchUnprocessedAsync(_batchSize);
        while (batch.Count > 0)
        {
            await ProcessBatch(ct, batch, parser, rawEnvelopeService, digestionService, dbContext);
            batch = await rawEnvelopeService.FetchUnprocessedAsync(_batchSize);
        }
    }

    private async Task ProcessBatch(CancellationToken ct, List<RawEnvelope> batch, IEventParser parser, IRawEnvelopeService rawEnvelopeService, IDigestionService digestionService, SigilDbContext dbContext)
    {
        foreach (var group in batch.GroupBy(r => r.ProjectId))
        {
            var successIds = new List<long>();
            var failures = new List<(long Id, string Error)>();
            var parsedEvents = new List<ParsedEvent>();

            foreach (var raw in group)
            {
                try
                {
                    parsedEvents.AddRange(parser.Parse(raw.RawData, raw.ReceivedAt));
                    successIds.Add(raw.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to parse raw envelope {Id} for project {ProjectId}", raw.Id, raw.ProjectId);
                    failures.Add((raw.Id, ex.Message));
                }
            }

            if (failures.Count > 0)
                await rawEnvelopeService.BulkMarkFailedAsync(failures);

            if (parsedEvents.Count > 0)
            {
                try
                {
                    await digestionService.BulkDigestAsync(group.Key, parsedEvents, ct);
                    await rawEnvelopeService.DeleteAsync(successIds);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Digestion failed for project {ProjectId}. {Count} envelopes left unprocessed",
                        group.Key, successIds.Count);
                    
                    await rawEnvelopeService.BulkMarkFailedAsync(successIds.Select(id => (id, ex.Message)));
                    // Leave unprocessed — will be retried on next wake
                }
                finally
                {
                    dbContext.ChangeTracker.Clear();
                }
            }
        }
    }
}
