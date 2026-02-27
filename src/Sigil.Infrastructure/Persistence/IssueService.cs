using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Events;
using Sigil.Application.Models.Issues;
using Sigil.Application.Models.MergeSets;
using Sigil.Domain;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Extensions;
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
                        Title = representativeEvent.NormalizedMessage?.Truncate(8192),
                        ExceptionType = representativeEvent.ExceptionType,
                        Level = representativeEvent.Level,
                        Priority = Priority.Low,
                        Status = IssueStatus.Open,
                        Fingerprint = fingerprint,
                        FirstSeen = representativeEvent.Timestamp,
                        LastSeen = representativeEvent.Timestamp,
                        OccurrenceCount = 0,
                        Culprit = representativeEvent.Culprit?.Truncate(8192),
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
            q = q.Where(i => dbContext.UserIssueStates.Any(s => s.IssueId == i.Id && s.UserId == query.BookmarkedByUserId.Value && s.IsBookmarked));

        var (freeText, tagFilters) = ParseSearch(query.Search);
        bool hasFullTextSearch = !string.IsNullOrEmpty(freeText);
        if (hasFullTextSearch)
        {
            freeText = SearchService.BuildPrefixQuery(freeText!);
            q = q.Where(i => EF.Property<NpgsqlTsVector>(i, "SearchVector").Matches(EF.Functions.ToTsQuery("simple", freeText!)));
        }
        foreach (var (tagKey, tagValue) in tagFilters)
        {
            q = q.Where(i => i.Tags.Any(t => EF.Functions.ILike(t.TagValue!.TagKey!.Key, tagKey) && EF.Functions.ILike(t.TagValue.Value, tagValue)));
        }

        int totalCount = await q.CountAsync();

        if (hasFullTextSearch)
        {
            // ReSharper disable EntityFramework.ClientSideDbFunctionCall
            q = q.OrderByDescending(i => EF.Property<NpgsqlTsVector>(i, "SearchVector").Rank(EF.Functions.ToTsQuery("simple", freeText!)));
            // ReSharper enable EntityFramework.ClientSideDbFunctionCall
        }
        else
        {
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
        }

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
            issue.ResolvedInReleaseId = null;
        }
        else if (status == IssueStatus.ResolvedInFuture)
        {
            issue.ResolvedAt = dateTime.UtcNow;
            issue.ResolvedById = userId;
            var latestRelease = await dbContext.Releases
                .Where(r => r.ProjectId == issue.ProjectId)
                .OrderByDescending(r => r.FirstSeenAt)
                .FirstOrDefaultAsync();
            issue.ResolvedInReleaseId = latestRelease?.Id;
        }
        else if (status == IssueStatus.Open)
        {
            issue.ResolvedAt = null;
            issue.ResolvedById = null;
            issue.ResolvedInReleaseId = null;

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

    public async Task<Issue> UpdateIssuePriorityAsync(int issueId, Priority priority, Guid? userId = null)
    {
        var issue = await dbContext.Issues.AsTracking().FirstAsync(i => i.Id == issueId);
        var oldPriority = issue.Priority;
        issue.Priority = priority;
        await dbContext.SaveChangesAsync();

        if (issue.MergeSetId.HasValue)
            await dbContext.Issues
                .Where(i => i.MergeSetId == issue.MergeSetId && i.Id != issueId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Priority, priority));

        if (oldPriority != priority)
            await activityService.LogActivityAsync(issueId, IssueActivityAction.PriorityChanged, userId,
                extra: new Dictionary<string, string>
                {
                    ["previous"] = oldPriority.ToString(),
                    ["new"] = priority.ToString(),
                });

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

        var issueIds = items.Select(i => i.Id).ToList();
        Dictionary<int, HashSet<string>> systemTagsByIssue = [];
        if (issueIds.Count > 0)
        {
            var rows = await dbContext.IssueTags
                .Where(t => issueIds.Contains(t.IssueId) && t.TagValue!.TagKey!.Key.StartsWith(SystemTags.Prefix))
                .Select(t => new { t.IssueId, Key = t.TagValue!.TagKey!.Key })
                .ToListAsync();
            systemTagsByIssue = rows
                .GroupBy(r => r.IssueId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.Key).ToHashSet());
        }

        Dictionary<int, DateTime?> lastViewedByIssue = [];
        if (query.ViewerUserId.HasValue && issueIds.Count > 0)
        {
            lastViewedByIssue = await dbContext.UserIssueStates
                .Where(s => s.UserId == query.ViewerUserId.Value && issueIds.Contains(s.IssueId))
                .ToDictionaryAsync(s => s.IssueId, s => s.LastViewedAt);
        }

        var summaries = items.Select(i =>
        {
            var ms = i.MergeSet;
            systemTagsByIssue.TryGetValue(i.Id, out var stags);
            var lastSeen = ms?.LastSeen ?? i.LastSeen;
            var isUnviewed = query.ViewerUserId.HasValue &&
                             (!lastViewedByIssue.TryGetValue(i.Id, out var lv) || lv is null || lv < i.LastChangedAt);
            return new IssueSummary(
                i.Id, i.Title, i.ExceptionType, i.Culprit,
                i.Status, i.Priority,
                ms?.Level ?? i.Level,
                ms?.FirstSeen ?? i.FirstSeen,
                lastSeen,
                ms?.OccurrenceCount ?? i.OccurrenceCount,
                i.AssignedTo?.DisplayName,
                i.MergeSetId,
                i.MergeSetId.HasValue ? memberCounts.GetValueOrDefault(i.MergeSetId.Value, 0) : 0,
                stags,
                isUnviewed);
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
            issue.Fingerprint, issue.Status, issue.Priority, issue.MergeSet?.Level ?? issue.Level,
            issue.MergeSet?.FirstSeen ?? issue.FirstSeen, issue.MergeSet?.LastSeen ?? issue.LastSeen,
            issue.MergeSet?.OccurrenceCount ?? issue.OccurrenceCount,
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

    public async Task<List<int>> GetHistogramAsync(int issueId, int days = 14)
    {
        var since = DateTime.UtcNow.Date.AddDays(-days + 1);
        var buckets = await dbContext.EventBuckets
            .Where(b => b.IssueId == issueId && b.BucketStart >= since)
            .GroupBy(b => b.BucketStart.Date)
            .Select(g => new { Day = g.Key, Count = g.Sum(b => b.Count) })
            .ToListAsync();

        var result = new List<int>(days);
        for (int i = 0; i < days; i++)
        {
            var day = since.AddDays(i);
            result.Add(buckets.FirstOrDefault(b => b.Day == day)?.Count ?? 0);
        }
        return result;
    }

    public async Task<Dictionary<int, List<int>>> GetBulkHistogramsAsync(List<int> issueIds, int days = 14)
    {
        if (issueIds.Count == 0) return [];
        var since = DateTime.UtcNow.Date.AddDays(-days + 1);

        var buckets = await dbContext.EventBuckets
            .Where(b => issueIds.Contains(b.IssueId) && b.BucketStart >= since)
            .GroupBy(b => new { b.IssueId, Day = b.BucketStart.Date })
            .Select(g => new { g.Key.IssueId, g.Key.Day, Count = g.Sum(b => b.Count) })
            .ToListAsync();

        return issueIds.ToDictionary(id => id, id =>
        {
            var issueBuckets = buckets.Where(b => b.IssueId == id).ToList();
            var result = new List<int>(days);
            for (int i = 0; i < days; i++)
            {
                var day = since.AddDays(i);
                result.Add(issueBuckets.FirstOrDefault(b => b.Day == day)?.Count ?? 0);
            }
            return result;
        });
    }

    public async Task RecordPageViewAsync(Guid userId, int projectId, PageType pageType)
    {
        var view = await dbContext.UserPageViews.AsTracking()
            .FirstOrDefaultAsync(v => v.UserId == userId && v.ProjectId == projectId && v.PageType == pageType);

        if (view is not null)
        {
            view.LastViewedAt = dateTime.UtcNow;
        }
        else
        {
            dbContext.UserPageViews.Add(new UserPageView
            {
                UserId = userId,
                ProjectId = projectId,
                PageType = pageType,
                LastViewedAt = dateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
    }

    internal class IssueReleaseRange
    {
        public string? FirstRelease { get; set; }
        public string? LastRelease { get; set; }
    }

    private static (string? FreeText, List<(string Key, string Value)> TagFilters) ParseSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return (null, []);

        var parts = search.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tagFilters = new List<(string Key, string Value)>();
        var freeTextParts = new List<string>();

        foreach (var part in parts)
        {
            var colonIdx = part.IndexOf(':');
            if (colonIdx > 0 && colonIdx < part.Length - 1)
                tagFilters.Add((part[..colonIdx], part[(colonIdx + 1)..]));
            else
                freeTextParts.Add(part);
        }

        return (freeTextParts.Count > 0 ? string.Join(' ', freeTextParts) : null, tagFilters);
    }
}
