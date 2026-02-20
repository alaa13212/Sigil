using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.infrastructure.Persistence.Analyzers;

internal class MissingEnvironmentAnalyzer(SigilDbContext dbContext) : IProjectAnalyzer
{
    public string AnalyzerId => "missing-environment";
    public bool IsRepeatable => false;

    public async Task<ProjectRecommendation?> AnalyzeAsync(Project project)
    {
        var hasEvents = await dbContext.Events.AnyAsync(e => e.ProjectId == project.Id);
        if (!hasEvents) return null;

        var hasEnvironment = await dbContext.Events
            .Where(e => e.ProjectId == project.Id)
            .AnyAsync(e => e.Tags.Any(tv => tv.TagKey!.Key == "environment"));
        if (hasEnvironment) return null;

        return new ProjectRecommendation
        {
            ProjectId = project.Id,
            AnalyzerId = AnalyzerId,
            Severity = RecommendationSeverity.Info,
            Title = "No environment set",
            Description = "Events don't have an environment tag (e.g., `production`, `staging`). Set the `environment` option in your SDK initialization to enable environment-specific filtering.",
            ActionUrl = $"https://docs.sentry.io/platforms/{PlatformHelper.ToStringValue(project.Platform)}/configuration/environments/"
        };
    }
}
