using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Extensions;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;
using Sigil.Infrastructure.Persistence;

namespace Sigil.Infrastructure.Workers;

internal class ReingestionWorker(
    IServiceProvider services,
    ILogger<ReingestionWorker> logger) : IWorker<ReingestionWork>
{
    private const int BatchSize = 500;

    private readonly Channel<ReingestionWork> _channel =
        Channel.CreateUnbounded<ReingestionWork>(new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = false
        });

    public bool TryEnqueue(ReingestionWork item) => _channel.Writer.TryWrite(item);

    public async Task RunAsync(CancellationToken stoppingToken = default)
    {
        await ResumeOrphanedJobsAsync(stoppingToken);

        await foreach (var work in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(work.JobId, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Re-ingestion job {JobId} failed unexpectedly", work.JobId);
            }
        }
    }

    private async Task ResumeOrphanedJobsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SigilDbContext>();

            var orphanedJobIds = await dbContext.ReingestionJobs
                .AsNoTracking()
                .Where(j => j.Status == ReingestionJobStatus.Pending || j.Status == ReingestionJobStatus.Running)
                .Select(j => j.Id)
                .ToListAsync(ct);

            foreach (var jobId in orphanedJobIds)
            {
                logger.LogInformation("Re-enqueueing orphaned re-ingestion job {JobId}", jobId);
                _channel.Writer.TryWrite(new ReingestionWork(jobId));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resume orphaned re-ingestion jobs");
        }
    }

    private async Task ProcessJobAsync(int jobId, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var dbContext = sp.GetRequiredService<SigilDbContext>();
        var dateTime = sp.GetRequiredService<IDateTime>();
        var compressionService = sp.GetRequiredService<ICompressionService>();
        var parser = sp.GetRequiredService<IEventParser>();
        var contextBuilder = sp.GetRequiredService<IEventParsingContextBuilder>();
        var eventFilterEngine = sp.GetRequiredService<IEventFilterEngine>();
        var issueIngestion = sp.GetRequiredService<IIssueIngestionService>();
        var projectAccess = sp.GetRequiredService<IProjectEntityAccess>();
        var eventRanker = sp.GetRequiredService<IEventRanker>();
        var mergeSetAggregator = sp.GetRequiredService<IMergeSetAggregator>();
        var issueCache = sp.GetRequiredService<IIssueCache>();

        var job = await dbContext.ReingestionJobs.AsTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null)
        {
            logger.LogWarning("Re-ingestion job {JobId} not found", jobId);
            return;
        }

        try
        {
            var isResume = job.Status == ReingestionJobStatus.Running;
            job.Status = ReingestionJobStatus.Running;
            job.StartedAt ??= dateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            if (isResume)
                logger.LogInformation("Resuming re-ingestion job {JobId} from event ID {LastId}",
                    jobId, job.LastProcessedEventId);

            var project = await projectAccess.GetProjectByIdAsync(job.ProjectId);
            if (project is null)
            {
                job.Status = ReingestionJobStatus.Failed;
                job.Error = "Project not found";
                job.CompletedAt = dateTime.UtcNow;
                await dbContext.SaveChangesAsync(ct);
                return;
            }

            // Resolve target issue IDs (expand merge set if applicable)
            var targetIssueIds = await ResolveTargetIssueIdsAsync(dbContext, job, ct);

            var context = await contextBuilder.BuildAsync(job.ProjectId);

            // Snapshot max event ID to avoid processing events added during re-ingestion
            long maxEventId = await GetMaxEventIdAsync(dbContext, job.ProjectId, targetIssueIds, ct);
            if (maxEventId == 0)
            {
                job.Status = ReingestionJobStatus.Completed;
                job.CompletedAt = dateTime.UtcNow;
                await dbContext.SaveChangesAsync(ct);
                return;
            }

            // Pre-load all issue fingerprints for this project into a lookup
            var fingerprintByIssueId = await dbContext.Issues
                .AsNoTracking()
                .Where(i => i.ProjectId == job.ProjectId)
                .ToDictionaryAsync(i => i.Id, i => i.Fingerprint, ct);

            // Reverse lookup: fingerprint → issueId
            var issueIdByFingerprint = fingerprintByIssueId.ToDictionary(kv => kv.Value, kv => kv.Key);

            // Pre-load SuggestedEventIds to avoid per-event queries during deletion
            var suggestedEventIds = await dbContext.Issues
                .AsNoTracking()
                .Where(i => i.ProjectId == job.ProjectId && i.SuggestedEventId != null)
                .ToDictionaryAsync(i => i.SuggestedEventId!.Value, i => i.Id, ct);

            var affectedIssueIds = new HashSet<int>();
            var affectedMergeSetIds = new HashSet<int>();
            long lastId = job.LastProcessedEventId;

            while (true)
            {
                // Check cancellation
                var currentStatus = await dbContext.ReingestionJobs
                    .AsNoTracking()
                    .Where(j => j.Id == jobId)
                    .Select(j => j.Status)
                    .FirstOrDefaultAsync(ct);

                if (currentStatus == ReingestionJobStatus.Cancelled)
                {
                    logger.LogInformation("Re-ingestion job {JobId} was cancelled", jobId);
                    return;
                }

                var batch = await LoadBatchAsync(dbContext, job.ProjectId, targetIssueIds, lastId, maxEventId, ct);
                if (batch.Count == 0) break;

                lastId = batch[^1].Id;

                await ProcessBatchAsync(
                    dbContext, compressionService, parser, eventFilterEngine,
                    issueIngestion, project, context, job, batch,
                    fingerprintByIssueId, issueIdByFingerprint, suggestedEventIds,
                    affectedIssueIds, affectedMergeSetIds, ct);

                job.LastProcessedEventId = lastId;
                await dbContext.SaveChangesAsync(ct);

                // Clear change tracker to prevent memory accumulation across batches.
                // Re-attach the job entity since we update it every batch.
                dbContext.ChangeTracker.Clear();
                dbContext.ReingestionJobs.Attach(job);
            }

            // Recalculate stats for all affected issues
            await RecalculateAffectedIssuesAsync(
                dbContext, eventRanker, mergeSetAggregator, issueCache,
                affectedIssueIds, affectedMergeSetIds, ct);

            job.Status = ReingestionJobStatus.Completed;
            job.CompletedAt = dateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Re-ingestion job {JobId} completed: {Processed}/{Total} events, {Moved} moved, {Deleted} deleted",
                jobId, job.ProcessedEvents, job.TotalEvents, job.MovedEvents, job.DeletedEvents);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Re-ingestion job {JobId} failed", jobId);
            job.Status = ReingestionJobStatus.Failed;
            job.Error = ex.Message.Truncate(2000);
            job.CompletedAt = dateTime.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Resolves which issue IDs to process. If the job targets a single issue that belongs
    /// to a merge set, expands to include all issues in that merge set.
    /// Returns null for project-wide reingestion (all issues).
    /// </summary>
    private static async Task<List<int>?> ResolveTargetIssueIdsAsync(
        SigilDbContext dbContext, ReingestionJob job, CancellationToken ct)
    {
        if (!job.IssueId.HasValue)
            return null; // project-wide

        var issue = await dbContext.Issues.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == job.IssueId.Value, ct);
        if (issue is null)
            return [job.IssueId.Value];

        if (!issue.MergeSetId.HasValue)
            return [job.IssueId.Value];

        // Expand to all issues in the merge set
        return await dbContext.Issues.AsNoTracking()
            .Where(i => i.MergeSetId == issue.MergeSetId.Value)
            .Select(i => i.Id)
            .ToListAsync(ct);
    }

    private static async Task<long> GetMaxEventIdAsync(
        SigilDbContext dbContext, int projectId, List<int>? targetIssueIds, CancellationToken ct)
    {
        var query = dbContext.Events.AsNoTracking();
        query = targetIssueIds is not null
            ? query.Where(e => targetIssueIds.Contains(e.IssueId))
            : query.Where(e => e.ProjectId == projectId);

        return await query.MaxAsync(e => (long?)e.Id, ct) ?? 0;
    }

    private static async Task<List<CapturedEvent>> LoadBatchAsync(
        SigilDbContext dbContext, int projectId, List<int>? targetIssueIds,
        long lastId, long maxEventId, CancellationToken ct)
    {
        var query = dbContext.Events.AsTracking()
            .Include(e => e.StackFrames)
            .Where(e => e.Id > lastId && e.Id <= maxEventId);

        query = targetIssueIds is not null
            ? query.Where(e => targetIssueIds.Contains(e.IssueId))
            : query.Where(e => e.ProjectId == projectId);

        return await query.OrderBy(e => e.Id).Take(BatchSize).ToListAsync(ct);
    }

    private async Task ProcessBatchAsync(
        SigilDbContext dbContext,
        ICompressionService compressionService,
        IEventParser parser,
        IEventFilterEngine eventFilterEngine,
        IIssueIngestionService issueIngestion,
        Project project,
        EventParsingContext context,
        ReingestionJob job,
        List<CapturedEvent> batch,
        Dictionary<int, string> fingerprintByIssueId,
        Dictionary<string, int> issueIdByFingerprint,
        Dictionary<long, int> suggestedEventIds,
        HashSet<int> affectedIssueIds,
        HashSet<int> affectedMergeSetIds,
        CancellationToken ct)
    {
        // Phase 1: Parse all events, determine actions
        var parsedBatch = new List<(CapturedEvent Evt, ParsedEvent Parsed)>();
        var toDelete = new List<CapturedEvent>();
        var newFingerprints = new Dictionary<string, List<ParsedEvent>>(); // fingerprint → parsed events needing new issues

        foreach (var evt in batch)
        {
            job.ProcessedEvents++;

            if (evt.RawCompressedJson is null)
            {
                logger.LogWarning("Event {EventId} has null RawCompressedJson, skipping", evt.Id);
                continue;
            }

            string rawJson;
            try
            {
                rawJson = compressionService.DecompressToString(evt.RawCompressedJson);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to decompress event {EventId}, skipping", evt.Id);
                continue;
            }

            var envelope = $"{{}}\n{{\"type\":\"event\"}}\n{rawJson}";

            List<ParsedEvent> parsedEvents;
            try
            {
                parsedEvents = await parser.Parse(context, envelope, evt.ReceivedAt);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to re-parse event {EventId}, skipping", evt.Id);
                continue;
            }

            if (parsedEvents.Count == 0) continue;
            var parsed = parsedEvents[0];

            affectedIssueIds.Add(evt.IssueId);

            // Check inbound filters
            if (context.InboundFilters.Count > 0 && eventFilterEngine.ShouldRejectEvent(parsed, context.InboundFilters))
            {
                toDelete.Add(evt);
                job.DeletedEvents++;
                continue;
            }

            // Track fingerprints that don't exist yet (for bulk creation)
            if (!string.IsNullOrEmpty(parsed.Fingerprint) && !issueIdByFingerprint.ContainsKey(parsed.Fingerprint))
            {
                if (!newFingerprints.ContainsKey(parsed.Fingerprint))
                    newFingerprints[parsed.Fingerprint] = [];
                newFingerprints[parsed.Fingerprint].Add(parsed);
            }

            parsedBatch.Add((evt, parsed));
        }

        // Phase 2: Bulk-create any new issues via the cached ingestion service
        if (newFingerprints.Count > 0)
        {
            var groupings = newFingerprints
                .Select(kv => kv.Value.GroupBy(_ => kv.Key).First())
                .ToList();

            var createdIssues = await issueIngestion.BulkGetOrCreateIssuesAsync(project, groupings);
            foreach (var issue in createdIssues)
            {
                fingerprintByIssueId[issue.Id] = issue.Fingerprint;
                issueIdByFingerprint[issue.Fingerprint] = issue.Id;
            }
        }

        // Phase 3: Delete filtered events
        foreach (var evt in toDelete)
            DeleteEvent(dbContext, evt, suggestedEventIds);

        // Phase 4: Apply moves and updates
        foreach (var (evt, parsed) in parsedBatch)
        {
            // Check fingerprint change
            var currentFingerprint = fingerprintByIssueId.GetValueOrDefault(evt.IssueId);
            if (!string.IsNullOrEmpty(parsed.Fingerprint) && parsed.Fingerprint != currentFingerprint)
            {
                if (issueIdByFingerprint.TryGetValue(parsed.Fingerprint, out var targetId) && targetId != evt.IssueId)
                {
                    evt.IssueId = targetId;
                    affectedIssueIds.Add(targetId);
                    job.MovedEvents++;
                }
            }

            // Update culprit if changed
            if (parsed.Culprit != evt.Culprit)
                evt.Culprit = parsed.Culprit;

            // Update stack frames if changed
            UpdateStackFramesIfChanged(dbContext, evt, parsed);
        }
    }

    private static void DeleteEvent(
        SigilDbContext dbContext, CapturedEvent evt, Dictionary<long, int> suggestedEventIds)
    {
        // Null out SuggestedEventId if this event is the suggested one (using pre-loaded map)
        if (suggestedEventIds.TryGetValue(evt.Id, out var referencingIssueId))
        {
            var issue = dbContext.Issues.Local.FirstOrDefault(i => i.Id == referencingIssueId);
            if (issue is not null)
                issue.SuggestedEventId = null;
            else
                dbContext.Database.ExecuteSql(
                    $"""UPDATE "Issues" SET "SuggestedEventId" = NULL WHERE "Id" = {referencingIssueId}""");
            suggestedEventIds.Remove(evt.Id);
        }

        dbContext.StackFrames.RemoveRange(evt.StackFrames);

        // Delete EventTags via raw SQL to avoid loading them
        dbContext.Database.ExecuteSql(
            $"""DELETE FROM "EventTags" WHERE "EventId" = {evt.Id}""");

        dbContext.Events.Remove(evt);
    }

    private static void UpdateStackFramesIfChanged(SigilDbContext dbContext, CapturedEvent evt, ParsedEvent parsed)
    {
        if (parsed.Stacktrace.Count == 0) return;

        var existingFrames = evt.StackFrames.ToList();
        bool changed = existingFrames.Count != parsed.Stacktrace.Count;

        if (!changed)
        {
            for (int i = 0; i < existingFrames.Count; i++)
            {
                var existing = existingFrames[i];
                var reparsed = parsed.Stacktrace[i];
                if (existing.InApp != reparsed.InApp ||
                    existing.Function != reparsed.Function ||
                    existing.Filename != reparsed.Filename ||
                    existing.Module != reparsed.Module)
                {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed) return;

        dbContext.StackFrames.RemoveRange(existingFrames);
        foreach (var frame in parsed.Stacktrace)
        {
            dbContext.StackFrames.Add(new StackFrame
            {
                EventId = evt.Id,
                Function = frame.Function,
                Filename = frame.Filename,
                Module = frame.Module,
                LineNumber = frame.LineNumber,
                ColumnNumber = frame.ColumnNumber,
                InApp = frame.InApp,
                ContextLine = frame.ContextLine,
                PreContext = frame.PreContext,
                PostContext = frame.PostContext,
            });
        }
    }

    private static async Task RecalculateAffectedIssuesAsync(
        SigilDbContext dbContext,
        IEventRanker eventRanker,
        IMergeSetAggregator mergeSetAggregator,
        IIssueCache issueCache,
        HashSet<int> affectedIssueIds,
        HashSet<int> affectedMergeSetIds,
        CancellationToken ct)
    {
        if (affectedIssueIds.Count == 0) return;

        // Batch-load all affected issues in one query
        var issues = await dbContext.Issues.AsTracking()
            .Where(i => affectedIssueIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        // Batch-load event counts + min/max timestamps + max severity per issue in one query
        var issueStats = await dbContext.Events
            .Where(e => affectedIssueIds.Contains(e.IssueId))
            .GroupBy(e => e.IssueId)
            .Select(g => new
            {
                IssueId = g.Key,
                Count = g.Count(),
                MinTimestamp = g.Min(e => e.Timestamp),
                MaxTimestamp = g.Max(e => e.Timestamp),
                MaxLevel = g.Max(e => e.Level),
            })
            .ToDictionaryAsync(s => s.IssueId, ct);

        var issuesToDelete = new List<int>();

        foreach (var issueId in affectedIssueIds)
        {
            if (!issues.TryGetValue(issueId, out var issue)) continue;

            if (!issueStats.TryGetValue(issueId, out var stats))
            {
                issuesToDelete.Add(issueId);
                continue;
            }

            issue.OccurrenceCount = stats.Count;
            issue.FirstSeen = stats.MinTimestamp;
            issue.LastSeen = stats.MaxTimestamp;
            issue.Level = stats.MaxLevel;

            if (issue.MergeSetId.HasValue)
                affectedMergeSetIds.Add(issue.MergeSetId.Value);
        }

        // Batch rebuild IssueTags: delete all then bulk insert
        var existingIssueTags = await dbContext.IssueTags.AsTracking()
            .Where(it => affectedIssueIds.Contains(it.IssueId))
            .ToListAsync(ct);
        dbContext.IssueTags.RemoveRange(existingIssueTags);

        var tagAggregates = await dbContext.EventTags
            .Where(et => dbContext.Events.Any(e => e.Id == et.EventId && affectedIssueIds.Contains(e.IssueId)))
            .GroupBy(et => new { et.TagValueId, dbContext.Events.First(e => e.Id == et.EventId).IssueId })
            .Select(g => new
            {
                g.Key.IssueId,
                g.Key.TagValueId,
                Count = g.Count(),
                FirstSeen = g.Min(et => dbContext.Events.Where(e => e.Id == et.EventId).Select(e => e.Timestamp).First()),
                LastSeen = g.Max(et => dbContext.Events.Where(e => e.Id == et.EventId).Select(e => e.Timestamp).First()),
            })
            .ToListAsync(ct);

        foreach (var agg in tagAggregates)
        {
            dbContext.IssueTags.Add(new IssueTag
            {
                IssueId = agg.IssueId,
                TagValueId = agg.TagValueId,
                OccurrenceCount = agg.Count,
                FirstSeen = agg.FirstSeen,
                LastSeen = agg.LastSeen,
            });
        }

        // Batch rebuild EventBuckets
        var existingBuckets = await dbContext.EventBuckets.AsTracking()
            .Where(eb => affectedIssueIds.Contains(eb.IssueId))
            .ToListAsync(ct);
        dbContext.EventBuckets.RemoveRange(existingBuckets);

        var bucketAggregates = await dbContext.Events
            .Where(e => affectedIssueIds.Contains(e.IssueId))
            .GroupBy(e => new
            {
                e.IssueId,
                BucketStart = new DateTime(e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, e.Timestamp.Hour, 0, 0, DateTimeKind.Utc)
            })
            .Select(g => new { g.Key.IssueId, g.Key.BucketStart, Count = g.Count() })
            .ToListAsync(ct);

        foreach (var bucket in bucketAggregates)
        {
            dbContext.EventBuckets.Add(new EventBucket
            {
                IssueId = bucket.IssueId,
                BucketStart = bucket.BucketStart,
                Count = bucket.Count,
            });
        }

        // Batch update SuggestedEvent + search columns for non-empty issues
        var liveIssueIds = affectedIssueIds.Except(issuesToDelete).ToList();
        if (liveIssueIds.Count > 0)
        {
            var allEvents = await dbContext.Events
                .Where(e => liveIssueIds.Contains(e.IssueId))
                .Include(e => e.StackFrames)
                .ToListAsync(ct);

            foreach (var group in allEvents.GroupBy(e => e.IssueId))
            {
                if (!issues.TryGetValue(group.Key, out var issue)) continue;
                var best = eventRanker.GetMostRelevantEvent(group);
                issue.SuggestedEventId = best.Id;
                issue.SuggestedEventMessage = best.Message?.Truncate(8192);
                issue.SuggestedFramesSummary = string.Join(" ",
                    best.StackFrames
                        .Where(f => f.InApp)
                        .Select(f => $"{f.Module}.{f.Function} {f.Filename} {(f.Module + " " + f.Function + " " + f.Filename).SplitPascal()}"))
                    .Truncate(8192);
            }
        }

        // Delete empty issues
        foreach (var issueId in issuesToDelete)
        {
            if (!issues.TryGetValue(issueId, out var issue)) continue;

            if (issue.MergeSetId.HasValue)
            {
                affectedMergeSetIds.Add(issue.MergeSetId.Value);
                issue.MergeSetId = null;
            }

            issue.SuggestedEventId = null;
        }
        await dbContext.SaveChangesAsync(ct);

        // Bulk delete related data for empty issues
        if (issuesToDelete.Count > 0)
        {
            var idsArray = issuesToDelete.ToArray();
            await dbContext.Database.ExecuteSqlAsync(
                $"""DELETE FROM "IssueActivities" WHERE "IssueId" = ANY({idsArray})""", ct);
            await dbContext.Database.ExecuteSqlAsync(
                $"""DELETE FROM "UserIssueStates" WHERE "IssueId" = ANY({idsArray})""", ct);
            await dbContext.Database.ExecuteSqlAsync(
                $"""DELETE FROM "SharedIssueLinks" WHERE "IssueId" = ANY({idsArray})""", ct);
            await dbContext.Database.ExecuteSqlAsync(
                $"""UPDATE "ReingestionJobs" SET "IssueId" = NULL WHERE "IssueId" = ANY({idsArray})""", ct);
            await dbContext.Database.ExecuteSqlAsync(
                $"""DELETE FROM "Issues" WHERE "Id" = ANY({idsArray})""", ct);
        }

        // Refresh merge set aggregates
        if (affectedMergeSetIds.Count > 0)
            await mergeSetAggregator.RefreshAggregatesAsync(affectedMergeSetIds);

        issueCache.InvalidateAll();
    }
}
