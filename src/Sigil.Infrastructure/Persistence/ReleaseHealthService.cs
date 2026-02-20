using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Releases;

namespace Sigil.infrastructure.Persistence;

internal class ReleaseHealthService(SigilDbContext dbContext) : IReleaseHealthService
{
    public async Task<PagedResponse<ReleaseHealthSummary>> GetReleaseHealthAsync(int projectId, int page = 1, int pageSize = 20)
    {
        var total = await dbContext.Releases.CountAsync(r => r.ProjectId == projectId);

        var rows = await dbContext.Releases
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.FirstSeenAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id, r.RawName, r.SemanticVersion, r.FirstSeenAt, r.DeployedAt,
                TotalEvents    = r.Events.Count(),
                AffectedIssues = r.Events.Select(e => e.IssueId).Distinct().Count(),
                NewIssues      = r.Events.Select(e => e.Issue!).Where(i => i.FirstSeen >= r.FirstSeenAt).Select(i => i.Id).Distinct().Count(),
                LastEventAt    = r.Events.Max(e => (DateTime?)e.Timestamp)
            })
            .ToListAsync();

        if (rows.Count == 0)
            return new PagedResponse<ReleaseHealthSummary>([], total, page, pageSize);

        var summaries = rows
            .Select(r => new ReleaseHealthSummary(
                r.Id, r.RawName, r.SemanticVersion, r.FirstSeenAt, r.DeployedAt,
                r.TotalEvents, r.NewIssues, 0, r.AffectedIssues, r.LastEventAt))
            .ToList();

        return new PagedResponse<ReleaseHealthSummary>(summaries, total, page, pageSize);
    }

    public async Task<ReleaseDetailResponse?> GetReleaseDetailAsync(int releaseId)
    {
        var data = await dbContext.Releases
            .Where(r => r.Id == releaseId)
            .Select(r => new
            {
                r.Id, r.RawName, r.SemanticVersion, r.Package, r.Build, r.CommitSha, r.FirstSeenAt, r.DeployedAt,
                TotalEvents    = r.Events.Count(),
                AffectedIssues = r.Events.Select(e => e.IssueId).Distinct().Count(),
                NewIssues      = r.Events.Select(e => e.Issue!).Where(i => i.FirstSeen >= r.FirstSeenAt).Select(i => i.Id).Distinct().Count(),
                TopIssueStats  = r.Events
                    .GroupBy(e => e.IssueId)
                    .Select(g => new { IssueId = g.Key, EventCount = g.Count() })
                    .OrderByDescending(x => x.EventCount)
                    .Take(20)
            })
            .FirstOrDefaultAsync();

        if (data is null) return null;

        var topIssueIds = data.TopIssueStats.Select(x => x.IssueId).ToList();
        var issueDict = await dbContext.Issues
            .Where(i => topIssueIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id);

        var topIssues = data.TopIssueStats
            .Where(x => issueDict.ContainsKey(x.IssueId))
            .Select(x =>
            {
                var issue = issueDict[x.IssueId];
                return new ReleaseIssueSummary(
                    issue.Id, issue.Title, issue.ExceptionType,
                    x.EventCount, issue.FirstSeen >= data.FirstSeenAt, issue.Level);
            })
            .ToList();

        return new ReleaseDetailResponse(
            data.Id, data.RawName, data.SemanticVersion, data.Package,
            data.Build?.ToString(), data.CommitSha,
            data.FirstSeenAt, data.DeployedAt,
            data.TotalEvents, data.NewIssues, 0, data.AffectedIssues, topIssues);
    }
}
