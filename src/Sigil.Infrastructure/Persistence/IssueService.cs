using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Events;
using Sigil.Application.Models.Issues;
using Sigil.Application.Models.MergeSets;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.Infrastructure.Persistence;

internal class IssueService(
    SigilDbContext dbContext,
    IEventRanker eventRanker,
    IDateTime dateTime,
    IIssueCache issueCache,
    IIssueActivityService activityService,
    IEventFilterService eventFilterService) : IIssueService
{
    public async Task<List<Issue>> BulkGetOrCreateIssuesAsync(Project project, IEnumerable<IGrouping<string, ParsedEvent>> eventsByFingerprint)
    {
        List<IGrouping<string, ParsedEvent>> groupings = eventsByFingerprint.ToList();

        var (results, misses) = issueCache.TryGetMany(groupings, 
            g => issueCache.TryGet(project.Id, g.Key, out Issue? cached) ? cached : null);
        
        dbContext.AttachRange(results);

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
                        Title = representativeEvent.NormalizedMessage ?? "{no message}",
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
                
                newIssues.ForEach(i => i.Activities.Add(new IssueActivity { Action = IssueActivityAction.Created, Timestamp = dateTime.UtcNow, Issue = i }));

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
                    .ThenInclude(tv => tv!.TagKey)
                .Include(i => i.MergeSet);
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
        IQueryable<Issue> q = dbContext.Issues
            .Where(i => i.ProjectId == projectId)
            .Where(i => i.MergeSetId == null || i.MergeSet!.PrimaryIssueId == i.Id);

        if (query.Status.HasValue)
            q = q.Where(i => i.Status == query.Status.Value);

        if (query.Priority.HasValue)
            q = q.Where(i => i.Priority == query.Priority.Value);

        if (query.Level.HasValue)
            q = q.Where(i => i.Level == query.Level.Value);

        if (query.AssignedToId.HasValue)
            q = q.Where(i => i.AssignedToId == query.AssignedToId.Value);

        if (query.BookmarkedByUserId.HasValue)
            q = q.Where(i => dbContext.IssueBookmarks.Any(b => b.IssueId == i.Id && b.UserId == query.BookmarkedByUserId.Value));

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
            IssueSortBy.FirstSeen => query.SortDescending
                ? q.OrderByDescending(i => i.MergeSet != null ? i.MergeSet.FirstSeen : i.FirstSeen)
                : q.OrderBy(i => i.MergeSet != null ? i.MergeSet.FirstSeen : i.FirstSeen),
            IssueSortBy.OccurrenceCount => query.SortDescending
                ? q.OrderByDescending(i => i.MergeSet != null ? i.MergeSet.OccurrenceCount : i.OccurrenceCount)
                : q.OrderBy(i => i.MergeSet != null ? i.MergeSet.OccurrenceCount : i.OccurrenceCount),
            IssueSortBy.Priority => query.SortDescending
                ? q.OrderByDescending(i => i.Priority)
                : q.OrderBy(i => i.Priority),
            _ => query.SortDescending
                ? q.OrderByDescending(i => i.MergeSet != null ? i.MergeSet.LastSeen : i.LastSeen)
                : q.OrderBy(i => i.MergeSet != null ? i.MergeSet.LastSeen : i.LastSeen),
        };

        var items = await q
            .Include(i => i.AssignedTo)
            .Include(i => i.MergeSet)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Issue> UpdateIssueStatusAsync(int issueId, IssueStatus status, Guid? userId = null, bool ignoreFutureEvents = false)
    {
        var issue = await dbContext.Issues.AsTracking().FirstAsync(i => i.Id == issueId);
        var previousStatus = issue.Status;

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

            // Remove ignore filter when reopening
            if (issue.IgnoreFilterId.HasValue)
            {
                await eventFilterService.DeleteFilterAsync(issue.IgnoreFilterId.Value);
                issue.IgnoreFilterId = null;
            }
        }

        // Create fingerprint filter when ignoring with "ignore future events"
        if (status == IssueStatus.Ignored && ignoreFutureEvents && !issue.IgnoreFilterId.HasValue)
        {
            var filter = await eventFilterService.CreateFilterAsync(issue.ProjectId, new Application.Models.Filters.CreateFilterRequest(
                Field: "fingerprint",
                Operator: FilterOperator.Equals,
                Value: issue.Fingerprint,
                Action: FilterAction.Reject,
                Description: $"Auto-created when issue #{issueId} was ignored"));
            issue.IgnoreFilterId = filter.Id;
        }

        await dbContext.SaveChangesAsync();

        if (issue.MergeSetId.HasValue)
            await dbContext.Issues
                .Where(i => i.MergeSetId == issue.MergeSetId && i.Id != issueId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, status));

        // Log activity
        if (userId.HasValue)
        {
            var action = status switch
            {
                IssueStatus.Resolved => IssueActivityAction.Resolved,
                IssueStatus.Ignored  => IssueActivityAction.Ignored,
                IssueStatus.Open     => IssueActivityAction.Unresolved,
                _                    => IssueActivityAction.Unresolved
            };
            await activityService.LogActivityAsync(issueId, action, userId.Value);
        }

        issueCache.InvalidateAll();
        return issue;
    }

    public async Task<Issue> AssignIssueAsync(int issueId, Guid? assignToUserId, Guid? actionByUserId = null)
    {
        var issue = await dbContext.Issues.AsTracking().FirstAsync(i => i.Id == issueId);
        issue.AssignedToId = assignToUserId;
        await dbContext.SaveChangesAsync();

        if (issue.MergeSetId.HasValue)
            await dbContext.Issues
                .Where(i => i.MergeSetId == issue.MergeSetId && i.Id != issueId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.AssignedToId, assignToUserId));

        if (actionByUserId.HasValue)
        {
            var action = assignToUserId.HasValue ? IssueActivityAction.Assigned : IssueActivityAction.Unassigned;
            await activityService.LogActivityAsync(issueId, action, actionByUserId.Value);
        }

        issueCache.InvalidateAll();
        return issue;
    }

    public async Task<Issue> UpdateIssuePriorityAsync(int issueId, Priority priority)
    {
        var issue = await dbContext.Issues.AsTracking().FirstAsync(i => i.Id == issueId);
        issue.Priority = priority;
        await dbContext.SaveChangesAsync();

        if (issue.MergeSetId.HasValue)
            await dbContext.Issues
                .Where(i => i.MergeSetId == issue.MergeSetId && i.Id != issueId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Priority, priority));

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

        // Load member counts for merge sets via a separate query to avoid the Issue→MergeSet→Issues cycle
        var mergeSetIds = items
            .Where(i => i.MergeSetId.HasValue)
            .Select(i => i.MergeSetId!.Value)
            .Distinct()
            .ToList();

        var memberCounts = mergeSetIds.Count > 0
            ? await dbContext.Issues
                .Where(i => i.MergeSetId.HasValue && mergeSetIds.Contains(i.MergeSetId!.Value))
                .GroupBy(i => i.MergeSetId!.Value)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.Count)
            : new Dictionary<int, int>();

        var summaries = items.Select(i =>
        {
            var ms = i.MergeSet;
            return new IssueSummary(
                i.Id, i.Title, i.ExceptionType, i.Culprit,
                i.Status, i.Priority,
                ms?.Level ?? i.Level,
                ms?.FirstSeen ?? i.FirstSeen,
                ms?.LastSeen ?? i.LastSeen,
                ms?.OccurrenceCount ?? i.OccurrenceCount,
                i.AssignedTo?.DisplayName,
                i.MergeSetId,
                i.MergeSetId.HasValue ? memberCounts.GetValueOrDefault(i.MergeSetId.Value, 0) : 0);
        }).ToList();

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

        var releaseInfo = await dbContext.Events
            .Where(e => e.IssueId == issueId)
            .Where(e => e.Release != null)
            .Select(e => new { e.Timestamp, ReleaseName = e.Release!.RawName })
            .GroupBy(_ => 1)
            .Select(g => new IssueReleaseRange { FirstRelease = g.OrderBy(e => e.Timestamp).Select(e => e.ReleaseName).FirstOrDefault(), LastRelease = g.OrderByDescending(e => e.Timestamp).Select(e => e.ReleaseName).FirstOrDefault() })
            .FirstOrDefaultAsync();


        MergeSetResponse? mergeSetResponse = null;
        if (issue.MergeSet is not null)
        {
            var primaryIssueId = issue.MergeSet.PrimaryIssueId;
            var members = await dbContext.Issues
                .Where(mi => mi.MergeSetId == issue.MergeSet.Id)
                .Select(mi => new MergeSetMember(
                    mi.Id, mi.Title, mi.ExceptionType, mi.Fingerprint,
                    mi.OccurrenceCount, mi.FirstSeen, mi.LastSeen,
                    mi.Id == primaryIssueId))
                .ToListAsync();
            mergeSetResponse = new MergeSetResponse(
                issue.MergeSet.Id, primaryIssueId, members, issue.MergeSet.CreatedAt);
        }

        return new IssueDetailResponse(
            issue.Id, issue.ProjectId, issue.Title, issue.ExceptionType, issue.Culprit,
            issue.Fingerprint, issue.Status, issue.Priority, issue.Level,
            issue.FirstSeen, issue.LastSeen, issue.OccurrenceCount,
            issue.AssignedTo?.DisplayName, issue.AssignedToId,
            issue.ResolvedBy?.DisplayName, issue.ResolvedAt,
            tagGroups, suggestedEvent,
            releaseInfo?.FirstRelease, releaseInfo?.LastRelease,
            mergeSetResponse);
    }

    public async Task<List<IssueSummary>> GetSimilarIssuesAsync(int issueId)
    {
        var issue = await dbContext.Issues.FirstOrDefaultAsync(i => i.Id == issueId);
        if (issue is null) return [];

        var q = dbContext.Issues.Where(i =>
            i.ProjectId == issue.ProjectId &&
            i.Id != issueId &&
            i.MergeSetId == null &&
            i.ExceptionType == issue.ExceptionType && (
                i.Title == issue.Title
                || (issue.Culprit != null && i.Culprit == issue.Culprit)
            )
        );

        var similar = await q
            .Include(i => i.AssignedTo)
            .OrderByDescending(i => i.LastSeen)
            .Take(20)
            .ToListAsync();

        return similar.Select(i => new IssueSummary(
            i.Id, i.Title, i.ExceptionType, i.Culprit,
            i.Status, i.Priority, i.Level,
            i.FirstSeen, i.LastSeen, i.OccurrenceCount,
            i.AssignedTo?.DisplayName)).ToList();
    }

    internal class IssueReleaseRange
    {
        public string? FirstRelease { get; set; }
        public string? LastRelease { get; set; }
    }
}
