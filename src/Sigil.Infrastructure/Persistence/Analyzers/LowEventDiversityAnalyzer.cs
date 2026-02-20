using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Persistence.Analyzers;

internal class LowEventDiversityAnalyzer(SigilDbContext dbContext) : IProjectAnalyzer
{
    public string AnalyzerId => "low-event-diversity";
    public bool IsRepeatable => false;

    public async Task<ProjectRecommendation?> AnalyzeAsync(Project project)
    {
        var totalIssues = await dbContext.Issues.CountAsync(i => i.ProjectId == project.Id);
        if (totalIssues < 2) return null;

        var totalEvents = await dbContext.Events.CountAsync(e => e.ProjectId == project.Id);

        var eventsPerIssue = (double)totalEvents / totalIssues;
        if (eventsPerIssue < 5000) return null;

        return new ProjectRecommendation
        {
            ProjectId = project.Id,
            AnalyzerId = AnalyzerId,
            Severity = RecommendationSeverity.Info,
            Title = "Too many events per issue — possible over-grouping",
            Description = $"An average of {eventsPerIssue:N0} events per issue suggests aggressive fingerprinting is collapsing many distinct errors into a few issues. Review your fingerprint overrides to ensure distinct errors are tracked separately.",
            ActionUrl = $"https://docs.sentry.io/platforms/{PlatformHelper.ToStringValue(project.Platform)}/usage/sdk-fingerprinting/"
        };
    }
}
