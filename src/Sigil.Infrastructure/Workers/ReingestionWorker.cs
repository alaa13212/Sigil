using System.Text;
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
            job.Status = ReingestionJobStatus.Running;
            job.StartedAt = dateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            var context = await contextBuilder.BuildAsync(job.ProjectId);

            // Snapshot max event ID to avoid processing events added during re-ingestion
            long maxEventId = await GetMaxEventIdAsync(dbContext, job, ct);
            if (maxEventId == 0)
            {
                job.Status = ReingestionJobStatus.Completed;
                job.CompletedAt = dateTime.UtcNow;
                await dbContext.SaveChangesAsync(ct);
                return;
            }

            var affectedIssueIds = new HashSet<int>();
            var affectedMergeSetIds = new HashSet<int>();
            long lastId = 0;

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

                var batch = await LoadBatchAsync(dbContext, job, lastId, maxEventId, ct);
                if (batch.Count == 0) break;

                lastId = batch[^1].Id;

                await ProcessBatchAsync(
                    dbContext, compressionService, parser, eventFilterEngine,
                    context, job, batch, affectedIssueIds, affectedMergeSetIds, ct);

                await dbContext.SaveChangesAsync(ct);
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

    private static async Task<long> GetMaxEventIdAsync(SigilDbContext dbContext, ReingestionJob job, CancellationToken ct)
    {
        var query = dbContext.Events.AsNoTracking();
        query = job.IssueId.HasValue
            ? query.Where(e => e.IssueId == job.IssueId.Value)
            : query.Where(e => e.ProjectId == job.ProjectId);

        return await query.MaxAsync(e => (long?)e.Id, ct) ?? 0;
    }

    private static async Task<List<CapturedEvent>> LoadBatchAsync(
        SigilDbContext dbContext, ReingestionJob job, long lastId, long maxEventId, CancellationToken ct)
    {
        var query = dbContext.Events.AsTracking()
            .Include(e => e.StackFrames)
            .Include(e => e.Tags)
            .Where(e => e.Id > lastId && e.Id <= maxEventId);

        query = job.IssueId.HasValue
            ? query.Where(e => e.IssueId == job.IssueId.Value)
            : query.Where(e => e.ProjectId == job.ProjectId);

        return await query.OrderBy(e => e.Id).Take(BatchSize).ToListAsync(ct);
    }

    private async Task ProcessBatchAsync(
        SigilDbContext dbContext,
        ICompressionService compressionService,
        IEventParser parser,
        IEventFilterEngine eventFilterEngine,
        EventParsingContext context,
        ReingestionJob job,
        List<CapturedEvent> batch,
        HashSet<int> affectedIssueIds,
        HashSet<int> affectedMergeSetIds,
        CancellationToken ct)
    {
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

            // Wrap in Sentry envelope format
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

            // Check inbound filters — should this event now be rejected?
            if (context.InboundFilters.Count > 0 && eventFilterEngine.ShouldRejectEvent(parsed, context.InboundFilters))
            {
                await DeleteEventAsync(dbContext, evt, ct);
                job.DeletedEvents++;
                continue;
            }

            // Check fingerprint change
            if (!string.IsNullOrEmpty(parsed.Fingerprint) && parsed.Fingerprint != GetIssueFingerprint(dbContext, evt.IssueId))
            {
                // Find or create target issue
                var targetIssue = await FindOrCreateIssueByFingerprintAsync(dbContext, evt.ProjectId, parsed, ct);
                if (targetIssue is not null && targetIssue.Id != evt.IssueId)
                {
                    evt.IssueId = targetIssue.Id;
                    affectedIssueIds.Add(targetIssue.Id);
                    if (targetIssue.MergeSetId.HasValue)
                        affectedMergeSetIds.Add(targetIssue.MergeSetId.Value);
                    job.MovedEvents++;
                }
            }

            // Update culprit if changed
            if (parsed.Culprit != evt.Culprit)
                evt.Culprit = parsed.Culprit;

            // Update stack frames if InApp flags changed
            UpdateStackFramesIfChanged(dbContext, evt, parsed);

            // Update event tags if auto-tag rules produced different tags
            UpdateEventTagsIfChanged(dbContext, evt, parsed);
        }
    }

    private static string? GetIssueFingerprint(SigilDbContext dbContext, int issueId)
    {
        var issue = dbContext.Issues.Local.FirstOrDefault(i => i.Id == issueId);
        if (issue is not null) return issue.Fingerprint;

        issue = dbContext.Issues.AsNoTracking().FirstOrDefault(i => i.Id == issueId);
        return issue?.Fingerprint;
    }

    private static async Task<Issue?> FindOrCreateIssueByFingerprintAsync(
        SigilDbContext dbContext, int projectId, ParsedEvent parsed, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(parsed.Fingerprint)) return null;

        var existing = await dbContext.Issues.AsTracking()
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.Fingerprint == parsed.Fingerprint, ct);

        if (existing is not null) return existing;

        var newIssue = new Issue
        {
            ProjectId = projectId,
            Fingerprint = parsed.Fingerprint,
            Title = parsed.Message?.Truncate(8192),
            ExceptionType = parsed.ExceptionType,
            Culprit = parsed.Culprit,
            Level = parsed.Level,
            FirstSeen = parsed.Timestamp,
            LastSeen = parsed.Timestamp,
            LastChangedAt = parsed.Timestamp,
            Status = IssueStatus.Open,
            Priority = Priority.Low,
        };

        dbContext.Issues.Add(newIssue);
        await dbContext.SaveChangesAsync(ct);
        return newIssue;
    }

    private static async Task DeleteEventAsync(SigilDbContext dbContext, CapturedEvent evt, CancellationToken ct)
    {
        // Null out SuggestedEventId if this event is the suggested one
        var issuesReferencing = await dbContext.Issues.AsTracking()
            .Where(i => i.SuggestedEventId == evt.Id)
            .ToListAsync(ct);
        foreach (var issue in issuesReferencing)
            issue.SuggestedEventId = null;

        dbContext.StackFrames.RemoveRange(evt.StackFrames);
        dbContext.EventTags.RemoveRange(
            dbContext.EventTags.Local.Where(et => et.EventId == evt.Id)
                .Union(dbContext.EventTags.Where(et => et.EventId == evt.Id)));
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

    private static void UpdateEventTagsIfChanged(SigilDbContext dbContext, CapturedEvent evt, ParsedEvent parsed)
    {
        // We can't easily resolve tag value IDs here without the tag service,
        // so we skip tag updates during re-ingestion. The fingerprint/filter/stackframe
        // changes are the primary concern.
        // Tag reconciliation happens via stat recalculation at the end.
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

        var issuesToDelete = new List<int>();

        foreach (var issueId in affectedIssueIds)
        {
            var issue = await dbContext.Issues.AsTracking()
                .FirstOrDefaultAsync(i => i.Id == issueId, ct);
            if (issue is null) continue;

            var eventCount = await dbContext.Events.CountAsync(e => e.IssueId == issueId, ct);

            if (eventCount == 0)
            {
                issuesToDelete.Add(issueId);
                continue;
            }

            // Recalculate occurrence count
            issue.OccurrenceCount = eventCount;

            // Recalculate first/last seen
            var timestamps = await dbContext.Events
                .Where(e => e.IssueId == issueId)
                .GroupBy(_ => 1)
                .Select(g => new { Min = g.Min(e => e.Timestamp), Max = g.Max(e => e.Timestamp) })
                .FirstAsync(ct);
            issue.FirstSeen = timestamps.Min;
            issue.LastSeen = timestamps.Max;

            // Recalculate max severity
            issue.Level = await dbContext.Events
                .Where(e => e.IssueId == issueId)
                .MaxAsync(e => e.Level, ct);

            // Rebuild IssueTags from EventTags
            var existingIssueTags = await dbContext.IssueTags.AsTracking()
                .Where(it => it.IssueId == issueId)
                .ToListAsync(ct);
            dbContext.IssueTags.RemoveRange(existingIssueTags);

            var tagAggregates = await dbContext.EventTags
                .Where(et => dbContext.Events.Any(e => e.Id == et.EventId && e.IssueId == issueId))
                .GroupBy(et => et.TagValueId)
                .Select(g => new
                {
                    TagValueId = g.Key,
                    Count = g.Count(),
                    FirstSeen = g.Min(et => dbContext.Events.Where(e => e.Id == et.EventId).Select(e => e.Timestamp).First()),
                    LastSeen = g.Max(et => dbContext.Events.Where(e => e.Id == et.EventId).Select(e => e.Timestamp).First()),
                })
                .ToListAsync(ct);

            foreach (var agg in tagAggregates)
            {
                dbContext.IssueTags.Add(new IssueTag
                {
                    IssueId = issueId,
                    TagValueId = agg.TagValueId,
                    OccurrenceCount = agg.Count,
                    FirstSeen = agg.FirstSeen,
                    LastSeen = agg.LastSeen,
                });
            }

            // Rebuild EventBuckets
            var existingBuckets = await dbContext.EventBuckets.AsTracking()
                .Where(eb => eb.IssueId == issueId)
                .ToListAsync(ct);
            dbContext.EventBuckets.RemoveRange(existingBuckets);

            var bucketAggregates = await dbContext.Events
                .Where(e => e.IssueId == issueId)
                .GroupBy(e => new DateTime(e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, e.Timestamp.Hour, 0, 0, DateTimeKind.Utc))
                .Select(g => new { BucketStart = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            foreach (var bucket in bucketAggregates)
            {
                dbContext.EventBuckets.Add(new EventBucket
                {
                    IssueId = issueId,
                    BucketStart = bucket.BucketStart,
                    Count = bucket.Count,
                });
            }

            // Update SuggestedEvent
            var events = await dbContext.Events
                .Where(e => e.IssueId == issueId)
                .Include(e => e.StackFrames)
                .ToListAsync(ct);
            if (events.Count > 0)
            {
                var best = eventRanker.GetMostRelevantEvent(events);
                issue.SuggestedEventId = best.Id;

                // Update search columns
                issue.SuggestedEventMessage = best.Message?.Truncate(8192);
                issue.SuggestedFramesSummary = string.Join(" ",
                    best.StackFrames
                        .Where(f => f.InApp)
                        .Select(f => $"{f.Module}.{f.Function} {f.Filename} {(f.Module + " " + f.Function + " " + f.Filename).SplitPascal()}"))
                    .Truncate(8192);
            }

            if (issue.MergeSetId.HasValue)
                affectedMergeSetIds.Add(issue.MergeSetId.Value);
        }

        // Delete empty issues
        foreach (var issueId in issuesToDelete)
        {
            var issue = await dbContext.Issues.AsTracking()
                .FirstOrDefaultAsync(i => i.Id == issueId, ct);
            if (issue is null) continue;

            // Remove from merge set first
            if (issue.MergeSetId.HasValue)
            {
                affectedMergeSetIds.Add(issue.MergeSetId.Value);
                issue.MergeSetId = null;
            }

            // Null out SuggestedEventId
            issue.SuggestedEventId = null;

            // Remove IssueTags
            var tags = await dbContext.IssueTags.AsTracking()
                .Where(it => it.IssueId == issueId)
                .ToListAsync(ct);
            dbContext.IssueTags.RemoveRange(tags);

            // Remove EventBuckets
            var buckets = await dbContext.EventBuckets.AsTracking()
                .Where(eb => eb.IssueId == issueId)
                .ToListAsync(ct);
            dbContext.EventBuckets.RemoveRange(buckets);

            // Remove activities
            var activities = await dbContext.IssueActivities.AsTracking()
                .Where(a => a.IssueId == issueId)
                .ToListAsync(ct);
            dbContext.IssueActivities.RemoveRange(activities);

            // Remove user states
            var userStates = await dbContext.UserIssueStates.AsTracking()
                .Where(s => s.IssueId == issueId)
                .ToListAsync(ct);
            dbContext.UserIssueStates.RemoveRange(userStates);

            // Remove shared links
            var sharedLinks = await dbContext.SharedIssueLinks.AsTracking()
                .Where(sl => sl.IssueId == issueId)
                .ToListAsync(ct);
            dbContext.SharedIssueLinks.RemoveRange(sharedLinks);

            // Remove reingestion jobs referencing this issue
            var reingestionJobs = await dbContext.ReingestionJobs.AsTracking()
                .Where(rj => rj.IssueId == issueId)
                .ToListAsync(ct);
            foreach (var rj in reingestionJobs)
                rj.IssueId = null;

            dbContext.Issues.Remove(issue);
        }

        await dbContext.SaveChangesAsync(ct);

        // Refresh merge set aggregates
        if (affectedMergeSetIds.Count > 0)
            await mergeSetAggregator.RefreshAggregatesAsync(affectedMergeSetIds);

        // Invalidate issue cache
        issueCache.InvalidateAll();
    }
}
