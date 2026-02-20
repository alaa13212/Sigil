using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.MergeSets;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.infrastructure.Persistence;

internal class MergeSetService(
    SigilDbContext dbContext,
    IIssueActivityService activityService,
    IIssueCache issueCache,
    IDateTime dateTime) : IMergeSetService
{
    public async Task<MergeSetResponse> CreateAsync(int projectId, List<int> issueIds, Guid userId)
    {
        if (issueIds.Count < 2)
            throw new InvalidOperationException("A merge set requires at least 2 issues.");

        var issues = await dbContext.Issues.AsTracking()
            .Where(i => issueIds.Contains(i.Id) && i.ProjectId == projectId)
            .ToListAsync();

        if (issues.Count != issueIds.Count)
            throw new InvalidOperationException("One or more issues not found in this project.");

        if (issues.Any(i => i.MergeSetId != null))
            throw new InvalidOperationException("One or more issues are already in a merge set.");

        var mergeSet = new MergeSet
        {
            ProjectId = projectId,
            PrimaryIssueId = issues.MaxBy(i => i.OccurrenceCount)!.Id,
            CreatedAt = dateTime.UtcNow,
            FirstSeen = issues.Min(i => i.FirstSeen),
            LastSeen = issues.Max(i => i.LastSeen),
            OccurrenceCount = issues.Sum(i => i.OccurrenceCount),
            Level = issues.Max(i => i.Level),
        };

        dbContext.MergeSets.Add(mergeSet);
        await dbContext.SaveChangesAsync();

        foreach (var issue in issues)
            issue.MergeSetId = mergeSet.Id;

        await dbContext.SaveChangesAsync();

        // Propagate primary's Status/Priority/AssignedTo to non-primary members
        var primary = issues.First(i => i.Id == issueIds[0]);
        await dbContext.Issues
            .Where(i => i.MergeSetId == mergeSet.Id && i.Id != primary.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, primary.Status)
                .SetProperty(i => i.Priority, primary.Priority)
                .SetProperty(i => i.AssignedToId, primary.AssignedToId));

        foreach (var issue in issues)
            await activityService.LogActivityAsync(issue.Id, userId, IssueActivityAction.Merged);

        issueCache.InvalidateAll();
        return BuildResponse(mergeSet, issues);
    }

    public async Task<MergeSetResponse> AddIssueAsync(int mergeSetId, int issueId, Guid userId)
    {
        var mergeSet = await dbContext.MergeSets.AsTracking()
            .Include(m => m.Issues)
            .FirstOrDefaultAsync(m => m.Id == mergeSetId)
            ?? throw new InvalidOperationException("Merge set not found.");

        var issue = await dbContext.Issues.AsTracking()
            .FirstOrDefaultAsync(i => i.Id == issueId && i.ProjectId == mergeSet.ProjectId)
            ?? throw new InvalidOperationException("Issue not found in this project.");

        if (issue.MergeSetId != null)
            throw new InvalidOperationException("Issue is already in a merge set.");

        issue.MergeSetId = mergeSetId;
        await dbContext.SaveChangesAsync();
        await RefreshAggregatesAsync([mergeSetId]);

        await activityService.LogActivityAsync(issueId, userId, IssueActivityAction.Merged);
        issueCache.InvalidateAll();

        return await GetByIdAsync(mergeSetId) ?? throw new InvalidOperationException("Merge set not found.");
    }

    public async Task RemoveIssueAsync(int mergeSetId, int issueId, Guid userId)
    {
        var mergeSet = await dbContext.MergeSets.AsTracking()
            .Include(m => m.Issues)
            .FirstOrDefaultAsync(m => m.Id == mergeSetId)
            ?? throw new InvalidOperationException("Merge set not found.");

        if (!mergeSet.Issues.Any(i => i.Id == issueId))
            throw new InvalidOperationException("Issue not found in merge set.");

        var trackedIssue = await dbContext.Issues.AsTracking().FirstAsync(i => i.Id == issueId);
        trackedIssue.MergeSetId = null;
        await dbContext.SaveChangesAsync();

        await activityService.LogActivityAsync(issueId, userId, IssueActivityAction.Unmerged);
        issueCache.InvalidateAll();

        var remaining = mergeSet.Issues.Where(i => i.Id != issueId).ToList();

        if (remaining.Count < 2)
        {
            // Dissolve: clear remaining issues then delete the merge set
            await dbContext.Issues
                .Where(i => i.MergeSetId == mergeSetId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.MergeSetId, (int?)null));
            await dbContext.MergeSets.Where(m => m.Id == mergeSetId).ExecuteDeleteAsync();
        }
        else
        {
            if (mergeSet.PrimaryIssueId == issueId)
            {
                var newPrimary = remaining.OrderBy(i => i.FirstSeen).First();
                mergeSet.PrimaryIssueId = newPrimary.Id;
                await dbContext.SaveChangesAsync();
            }
            await RefreshAggregatesAsync([mergeSetId]);
        }
    }

    public async Task SetPrimaryAsync(int mergeSetId, int issueId)
    {
        var mergeSet = await dbContext.MergeSets.AsTracking()
            .FirstOrDefaultAsync(m => m.Id == mergeSetId)
            ?? throw new InvalidOperationException("Merge set not found.");

        mergeSet.PrimaryIssueId = issueId;
        await dbContext.SaveChangesAsync();
    }

    public async Task<MergeSetResponse?> GetByIdAsync(int mergeSetId)
    {
        var mergeSet = await dbContext.MergeSets
            .FirstOrDefaultAsync(m => m.Id == mergeSetId);

        if (mergeSet is null) return null;

        // Load members separately to avoid the MergeSet→Issues→Issue→MergeSet cycle in no-tracking queries
        var members = await dbContext.Issues
            .Where(i => i.MergeSetId == mergeSetId)
            .ToListAsync();

        return BuildResponse(mergeSet, members);
    }

    public async Task RefreshAggregatesAsync(IEnumerable<int> mergeSetIds)
    {
        var ids = mergeSetIds.ToList();
        var mergeSets = await dbContext.MergeSets.AsTracking()
            .Include(m => m.Issues)
            .Where(m => ids.Contains(m.Id))
            .ToListAsync();

        foreach (var mergeSet in mergeSets)
        {
            var issues = mergeSet.Issues.ToList();
            if (issues.Count == 0) continue;

            mergeSet.FirstSeen = issues.Min(i => i.FirstSeen);
            mergeSet.LastSeen = issues.Max(i => i.LastSeen);
            mergeSet.OccurrenceCount = issues.Sum(i => i.OccurrenceCount);
            mergeSet.Level = issues.Max(i => i.Level);
        }

        await dbContext.SaveChangesAsync();
    }

    private static MergeSetResponse BuildResponse(MergeSet mergeSet, List<Issue> issues)
    {
        var members = issues
            .Select(i => new MergeSetMember(
                i.Id, i.Title, i.ExceptionType, i.Fingerprint,
                i.OccurrenceCount, i.FirstSeen, i.LastSeen,
                i.Id == mergeSet.PrimaryIssueId))
            .ToList();

        return new MergeSetResponse(mergeSet.Id, mergeSet.PrimaryIssueId, members, mergeSet.CreatedAt);
    }
}
