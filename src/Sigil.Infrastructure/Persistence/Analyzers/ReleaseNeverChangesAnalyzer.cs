using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Persistence.Analyzers;

internal class ReleaseNeverChangesAnalyzer(SigilDbContext dbContext, IDateTime dateTime) : IProjectAnalyzer
{
    public string AnalyzerId => "release-never-changes";
    public bool IsRepeatable => false;

    public async Task<ProjectRecommendation?> AnalyzeAsync(Project project)
    {
        var cutoff = dateTime.UtcNow.AddDays(-30);

        // Need at least 14 days of history
        var firstEvent = await dbContext.Events
            .Where(e => e.ProjectId == project.Id)
            .MinAsync(e => (DateTime?)e.Timestamp);
        if (firstEvent is null || (dateTime.UtcNow - firstEvent.Value).TotalDays < 14) return null;

        // Only 1 distinct release in the last 30 days
        var recentReleaseCount = await dbContext.Releases
            .CountAsync(r => r.ProjectId == project.Id && r.FirstSeenAt >= cutoff);
        if (recentReleaseCount != 1) return null;

        return new ProjectRecommendation
        {
            ProjectId = project.Id,
            AnalyzerId = AnalyzerId,
            Severity = RecommendationSeverity.Info,
            Title = "Release tag never changes",
            Description = "The same release version has been used for over 30 days. Use CI to inject a Git SHA or semantic version during build so you can track which releases introduce regressions.",
            ActionUrl = $"https://docs.sentry.io/platforms/{PlatformHelper.ToStringValue(project.Platform)}/configuration/releases/"
        };
    }
}
