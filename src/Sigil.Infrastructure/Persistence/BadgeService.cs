using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Persistence;

internal class BadgeService(SigilDbContext dbContext) : IBadgeService
{
    public async Task<Dictionary<int, ProjectBadgeCounts>> GetAllBadgeCountsAsync(Guid userId)
    {
        // One query: count open issues per project where LastSeen is newer than the user's last
        // Issues page view for that project (or all if never viewed).
        var unseenIssues = await dbContext.Issues
            .Where(i => i.Status == IssueStatus.Open)
            .Where(i => i.MergeSetId == null || i.MergeSet!.PrimaryIssueId == i.Id)
            .Where(i => !dbContext.UserPageViews.Any(
                v => v.UserId == userId
                     && v.ProjectId == i.ProjectId
                     && v.PageType == PageType.Issues
                     && v.LastViewedAt >= i.LastSeen))
            .GroupBy(i => i.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Count);

        // One query: count releases per project where FirstSeenAt is newer than the user's last
        // Releases page view for that project (or all if never viewed).
        var unseenReleases = await dbContext.Releases
            .Where(r => !dbContext.UserPageViews.Any(
                v => v.UserId == userId
                     && v.ProjectId == r.ProjectId
                     && v.PageType == PageType.Releases
                     && v.LastViewedAt >= r.FirstSeenAt))
            .GroupBy(r => r.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Count);

        var allProjectIds = unseenIssues.Keys.Union(unseenReleases.Keys);
        return allProjectIds.ToDictionary(
            id => id,
            id => new ProjectBadgeCounts(
                unseenIssues.GetValueOrDefault(id),
                unseenReleases.GetValueOrDefault(id)));
    }
}
