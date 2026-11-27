using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Interfaces;
using Sigil.Domain.Ingestion;
using Sigil.Infrastructure.Persistence;

namespace Sigil.Infrastructure.Workers;

internal class DigestionWorker(
    IServiceProvider services,
    IDigestionSignal signal,
    IOptions<BatchWorkersConfig> options,
    IDateTime dateTime,
    ISigilMetrics metrics,
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

        List<RawEnvelope> batch = await rawEnvelopeService.FetchUnprocessedAsync(_batchSize);
        while (batch.Count > 0)
        {
            await ProcessBatch(ct, batch);
            batch = await rawEnvelopeService.FetchUnprocessedAsync(_batchSize);
        }
    }

    private async Task ProcessBatch(CancellationToken ct, List<RawEnvelope> batch)
    {
        using var scope = services.CreateScope();
        var parser = scope.ServiceProvider.GetRequiredService<IEventParser>();
        var rawEnvelopeService = scope.ServiceProvider.GetRequiredService<IRawEnvelopeService>();
        var digestionService = scope.ServiceProvider.GetRequiredService<IDigestionService>();
        var contextBuilder = scope.ServiceProvider.GetRequiredService<IEventParsingContextBuilder>();
        var dbContext = scope.ServiceProvider.GetRequiredService<SigilDbContext>();

        // Snapshot the total pending queue depth before processing this batch
        var queueDepthAtStart = await dbContext.RawEnvelopes.CountAsync(e => e.Error == null, ct);

        foreach (var group in batch.GroupBy(r => r.ProjectId))
        {
            var sw = Stopwatch.StartNew();
            var successIds = new List<long>();
            var failures = new List<(long Id, string Error)>();
            var discarded = new List<(long Id, string Error)>();
            var parsedEvents = new List<ParsedEvent>();

            var context = await contextBuilder.BuildAsync(group.Key);

            sw.Restart();
            foreach (var raw in group)
            {
                // An envelope that carries no event item is discarded instead of parsed, so that it
                // leaves the digestion queue instead of waiting for a parser that will never use it.
                string? unsupportedType = EnvelopeItemTypes.DetectUnsupported(raw.RawData);
                if (unsupportedType is not null)
                {
                    discarded.Add((raw.Id, EnvelopeItemTypes.UnsupportedError(unsupportedType)));
                    metrics.RecordEnvelopeDiscarded(EnvelopeItemTypes.Normalize(unsupportedType));
                    continue;
                }

                try
                {
                    parsedEvents.AddRange(await parser.Parse(context, raw.RawData, raw.ReceivedAt));
                    successIds.Add(raw.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to parse raw envelope {Id} for project {ProjectId}", raw.Id, raw.ProjectId);
                    failures.Add((raw.Id, ex.Message));
                }
            }
            var parseElapsedMs = (int)sw.ElapsedMilliseconds;

            if (failures.Count > 0 || discarded.Count > 0)
            {
                await rawEnvelopeService.BulkMarkFailedAsync(failures.Concat(discarded));
                metrics.RecordEventsDropped(failures.Count);
            }

            if (parsedEvents.Count > 0)
            {
                try
                {
                    sw.Restart();
                    await digestionService.BulkDigestAsync(context, parsedEvents, ct);
                    var digestElapsedMs = (int)sw.ElapsedMilliseconds;
                    
                    await rawEnvelopeService.DeleteAsync(successIds);

                    dbContext.DigestBatchMetrics.Add(new DigestBatchMetric
                    {
                        RecordedAt = dateTime.UtcNow,
                        ProjectId = group.Key,
                        EventCount = parsedEvents.Count,
                        QueueDepthAtStart = queueDepthAtStart,
                        ParseElapsedMs = parseElapsedMs,
                        DigestElapsedMs = digestElapsedMs,
                    });
                    await dbContext.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Digestion failed for project {ProjectId}. {Count} envelopes left unprocessed",
                        group.Key, successIds.Count);

                    dbContext.ChangeTracker.Clear();
                    await rawEnvelopeService.BulkMarkFailedAsync(successIds.Select(id => (id, ex.Message)));
                    metrics.RecordEventsDropped(successIds.Count);
                }
                finally
                {
                    dbContext.ChangeTracker.Clear();
                }
            }
            else if (successIds.Count > 0)
            {
                // Envelopes that carried no event have been parsed and leave the queue as well.
                await rawEnvelopeService.DeleteAsync(successIds);
            }
        }
    }
}
