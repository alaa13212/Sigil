using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Persistence.Analyzers;

internal class MissingUserContextAnalyzer(SigilDbContext dbContext, IDateTime dateTime) : IProjectAnalyzer
{
    public string AnalyzerId => "missing-user-context";
    public bool IsRepeatable => false;

    public async Task<ProjectRecommendation?> AnalyzeAsync(Project project)
    {
        var cutoff = dateTime.UtcNow.AddDays(-7);
        var totalCount = await dbContext.Events
            .CountAsync(e => e.ProjectId == project.Id && e.Timestamp >= cutoff);
        
        if (totalCount < 20)
            return null;

        var withUserCount = await dbContext.Events
            .CountAsync(e => e.ProjectId == project.Id && e.Timestamp >= cutoff && e.UserId != null);

        // Only recommend if fewer than 10% of events have user context
        if ((double)withUserCount / totalCount >= 0.1)
            return null;

        return new ProjectRecommendation
        {
            ProjectId = project.Id,
            AnalyzerId = AnalyzerId,
            Severity = RecommendationSeverity.Info,
            Title = "Missing user context",
            Description = "Most events have no user information attached. Set user context in your SDK (ID, email, username) to enable user impact tracking and better issue prioritization.",
            ActionUrl = $"https://docs.sentry.io/platforms/{PlatformHelper.ToStringValue(project.Platform)}/enriching-events/identify-user/"
        };
    }
}
