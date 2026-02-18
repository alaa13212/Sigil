using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Events;
using Sigil.Application.Models.Issues;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.infrastructure.Persistence;

internal class IssueService(SigilDbContext dbContext, IEventRanker eventRanker, IDateTime dateTime, IIssueCache issueCache) : IIssueService
{
    public async Task<List<Issue>> BulkGetOrCreateIssuesAsync(Project project, IEnumerable<IGrouping<string, ParsedEvent>> eventsByFingerprint)
    {
        List<IGrouping<string, ParsedEvent>> groupings = eventsByFingerprint.ToList();

        var (results, misses) = issueCache.TryGetMany(groupings, g =>
        {
            issueCache.TryGet(project.Id, g.Key, out Issue? cached);
            if (cached is not null) dbContext.Attach(cached);
            return cached;
        });

        if (misses.Count > 0)
        {
            List<string> missFingerprints = misses.Select(g => g.Key).ToList();

            List<Issue> fromDb = await dbContext.Issues
                .AsTracking()
                .Include(i => i.Tags)
                    .ThenInclude(it => it.TagValue)
                    .ThenInclude(tv => tv!.TagKey)
                .Where(i => i.ProjectId == project.Id && missFingerprints.Contains(i.Fingerprint))
                .ToListAsync();

            List<string> existingFingerprints = fromDb.Select(i => i.Fingerprint).ToList();
            List<string> newFingerprints = missFingerprints.Except(existingFingerprints).ToList();

            if (newFingerprints.Any())
            {
                var newIssues = new List<Issue>();
                foreach (var fingerprint in newFingerprints)
                {
                    var eventsForFingerprint = misses.First(g => g.Key == fingerprint);
                    var representativeEvent = eventRanker.GetMostRelevantEvent(eventsForFingerprint);

                    newIssues.Add(new Issue
                    {
                        ProjectId = project.Id,
                        Title = representativeEvent.NormalizedMessage ?? "Unknown Error",
                        ExceptionType = representativeEvent.ExceptionType,
                        Level = representativeEvent.Level,
                        Priority = Priority.Low,
                        Status = IssueStatus.Open,
                        Fingerprint = fingerprint,
                        FirstSeen = representativeEvent.Timestamp,
                        LastSeen = representativeEvent.Timestamp,
                        OccurrenceCount = 0,
                        Culprit = representativeEvent.Culprit,
                    });
                }

                dbContext.Issues.AddRange(newIssues);
                await dbContext.SaveChangesAsync();
                fromDb.AddRange(newIssues);
            }

            foreach (Issue issue in fromDb)
                issueCache.Set(issue);

            results.AddRange(fromDb);
        }

        return results;
    }

    public async Task<Issue?> GetIssueByIdAsync(int issueId, bool includeTags = false, bool includeEvents = false)
    {
        IQueryable<Issue> query = dbContext.Issues;

        if (includeTags)
        {
            query = query
                .Include(i => i.SuggestedEvent!.Release)
                .Include(i => i.SuggestedEvent!.User)
                .Include(i => i.Tags)
                .ThenInclude(it => it.TagValue)
                .ThenInclude(tv => tv!.TagKey);
        }

        if (includeEvents)
        {
            query = query.Include(i => i.Events.OrderByDescending(e => e.Timestamp).Take(10));
        }

        return await query
            .Include(i => i.AssignedTo)
            .Include(i => i.ResolvedBy)
            .FirstOrDefaultAsync(i => i.Id == issueId);
    }

    public async Task<(List<Issue> Items, int TotalCount)> GetIssuesAsync(int projectId, IssueQueryParams query)
    {
        IQueryable<Issue> q = dbContext.Issues.Where(i => i.ProjectId == projectId);

        if (query.Status.HasValue)
            q = q.Where(i => i.Status == query.Status.Value);

        if (query.Priority.HasValue)
            q = q.Where(i => i.Priority == query.Priority.Value);

        if (query.Level.HasValue)
            q = q.Where(i => i.Level == query.Level.Value);

        if (query.AssignedToId.HasValue)
            q = q.Where(i => i.AssignedToId == query.AssignedToId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLower();
            q = q.Where(i =>
                i.Title.ToLower().Contains(search) ||
                (i.ExceptionType != null && i.ExceptionType.ToLower().Contains(search)) ||
                (i.Culprit != null && i.Culprit.ToLower().Contains(search)));
        }

        int totalCount = await q.CountAsync();

        q = query.SortBy switch
        {
            IssueSortBy.FirstSeen => query.SortDescending ? q.OrderByDescending(i => i.FirstSeen) : q.OrderBy(i => i.FirstSeen),
            IssueSortBy.OccurrenceCount => query.SortDescending ? q.OrderByDescending(i => i.OccurrenceCount) : q.OrderBy(i => i.OccurrenceCount),
            IssueSortBy.Priority => query.SortDescending ? q.OrderByDescending(i => i.Priority) : q.OrderBy(i => i.Priority),
            _ => query.SortDescending ? q.OrderByDescending(i => i.LastSeen) : q.OrderBy(i => i.LastSeen),
        };

        var items = await q
            .Include(i => i.AssignedTo)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Issue> UpdateIssueStatusAsync(int issueId, IssueStatus status, Guid? userId = null)
    {
        var issue = await dbContext.Issues.AsTracking().FirstAsync(i => i.Id == issueId);

        issue.Status = status;

        if (status == IssueStatus.Resolved)
        {
            issue.ResolvedAt = dateTime.UtcNow;
            issue.ResolvedById = userId;
        }
        else if (status == IssueStatus.Open)
        {
            issue.ResolvedAt = null;
            issue.ResolvedById = null;
        }

        await dbContext.SaveChangesAsync();
        issueCache.InvalidateAll();
        return issue;
    }

    public async Task<Issue> AssignIssueAsync(int issueId, Guid? assignToUserId, Guid? actionByUserId = null)
    {
        var issue = await dbContext.Issues.AsTracking().FirstAsync(i => i.Id == issueId);
        issue.AssignedToId = assignToUserId;
        await dbContext.SaveChangesAsync();
        issueCache.InvalidateAll();
        return issue;
    }

    public async Task<Issue> UpdateIssuePriorityAsync(int issueId, Priority priority)
    {
        var issue = await dbContext.Issues.AsTracking().FirstAsync(i => i.Id == issueId);
        issue.Priority = priority;
        await dbContext.SaveChangesAsync();
        issueCache.InvalidateAll();
        return issue;
    }

    public async Task<bool> DeleteIssueAsync(int issueId)
    {
        var deleted = await dbContext.Issues.Where(i => i.Id == issueId).ExecuteDeleteAsync() > 0;
        if (deleted)
            issueCache.InvalidateAll();
        return deleted;
    }

    public async Task<PagedResponse<IssueSummary>> GetIssueSummariesAsync(int projectId, IssueQueryParams query)
    {
        var (items, totalCount) = await GetIssuesAsync(projectId, query);

        var summaries = items.Select(i => new IssueSummary(
            i.Id, i.Title, i.ExceptionType, i.Culprit,
            i.Status, i.Priority, i.Level,
            i.FirstSeen, i.LastSeen, i.OccurrenceCount,
            i.AssignedTo?.DisplayName)).ToList();

        return new PagedResponse<IssueSummary>(summaries, totalCount, query.Page, query.PageSize);
    }

    public async Task<IssueDetailResponse?> GetIssueDetailAsync(int issueId)
    {
        var issue = await GetIssueByIdAsync(issueId, includeTags: true);
        if (issue is null) return null;

        var tagGroups = issue.Tags
            .Where(t => t.TagValue?.TagKey != null)
            .GroupBy(t => t.TagValue!.TagKey!.Key)
            .Select(g =>
            {
                int totalCount = g.Sum(t => t.OccurrenceCount);
                var values = g
                    .OrderByDescending(t => t.OccurrenceCount)
                    .Select(t => new IssueTagValue(t.TagValue!.Value, t.OccurrenceCount))
                    .ToList();
                return new IssueTagGroup(g.Key, values, totalCount);
            })
            .OrderBy(g => g.Key)
            .ToList();

        EventSummary? suggestedEvent = null;
        if (issue.SuggestedEvent is not null)
        {
            var e = issue.SuggestedEvent;
            suggestedEvent = new EventSummary(
                e.Id, e.EventId, e.Message, e.Level,
                e.Timestamp, e.Release?.RawName, e.User?.Identifier);
        }

        // Query first and last release names for this issue
        var releaseInfo = await dbContext.Events
            .Where(e => e.IssueId == issueId)
            .Select(e => new { e.Timestamp, ReleaseName = e.Release!.RawName })
            .GroupBy(_ => 1)
            .Select(g => new
            {
                FirstRelease = g.OrderBy(e => e.Timestamp).Select(e => e.ReleaseName).FirstOrDefault(),
                LastRelease = g.OrderByDescending(e => e.Timestamp).Select(e => e.ReleaseName).FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        return new IssueDetailResponse(
            issue.Id, issue.ProjectId, issue.Title, issue.ExceptionType, issue.Culprit,
            issue.Fingerprint, issue.Status, issue.Priority, issue.Level,
            issue.FirstSeen, issue.LastSeen, issue.OccurrenceCount,
            issue.AssignedTo?.DisplayName, issue.AssignedToId,
            issue.ResolvedBy?.DisplayName, issue.ResolvedAt,
            tagGroups, suggestedEvent,
            releaseInfo?.FirstRelease, releaseInfo?.LastRelease);
    }
}
