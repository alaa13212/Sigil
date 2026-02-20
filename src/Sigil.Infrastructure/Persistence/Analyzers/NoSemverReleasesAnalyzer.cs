using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Persistence.Analyzers;

internal class NoSemverReleasesAnalyzer(SigilDbContext dbContext) : IProjectAnalyzer
{
    public string AnalyzerId => "no-semver-releases";
    public bool IsRepeatable => false;

    public async Task<ProjectRecommendation?> AnalyzeAsync(Project project)
    {
        var totalReleases = await dbContext.Releases
            .CountAsync(r => r.ProjectId == project.Id);
        if (totalReleases < 3) return null;

        var hasSemver = await dbContext.Releases
            .AnyAsync(r => r.ProjectId == project.Id && r.SemanticVersion != null);
        if (hasSemver) return null;

        return new ProjectRecommendation
        {
            ProjectId = project.Id,
            AnalyzerId = AnalyzerId,
            Severity = RecommendationSeverity.Warning,
            Title = "No semantic versioning detected",
            Description = "Your releases don't use semantic versioning. Use a format like `myapp@1.2.3` or `1.2.3` so Sigil can track release health and regression trends. `myapp@` prefix guarantees the version number uniqueness across projects.",
            ActionUrl = $"https://docs.sentry.io/platforms/{PlatformHelper.ToStringValue(project.Platform)}/configuration/releases/"
        };
    }
}
