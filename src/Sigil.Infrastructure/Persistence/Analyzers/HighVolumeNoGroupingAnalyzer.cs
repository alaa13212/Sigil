using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.infrastructure.Persistence.Analyzers;

internal class HighVolumeNoGroupingAnalyzer(SigilDbContext dbContext) : IProjectAnalyzer
{
    public string AnalyzerId => "high-volume-no-grouping";
    public bool IsRepeatable => false;

    public async Task<ProjectRecommendation?> AnalyzeAsync(Project project)
    {
        var totalIssues = await dbContext.Issues.CountAsync(i => i.ProjectId == project.Id);
        if (totalIssues < 20)
            return null;

        var singleEventIssues = await dbContext.Issues
            .CountAsync(i => i.ProjectId == project.Id && i.OccurrenceCount == 1);

        if ((double)singleEventIssues / totalIssues < 0.6)
            return null;

        return new ProjectRecommendation
        {
            ProjectId = project.Id,
            AnalyzerId = AnalyzerId,
            Severity = RecommendationSeverity.Warning,
            Title = "Many single-event issues suggest poor grouping",
            Description = $"{singleEventIssues:N0} out of {totalIssues:N0} issues have only one event. This often means fingerprinting is too granular. Review your SDK's fingerprinting configuration or use custom message normalizers to group related errors.",
            ActionUrl = $"https://docs.sentry.io/platforms/{PlatformHelper.ToStringValue(project.Platform)}/usage/sdk-fingerprinting/"
        };
    }
}
