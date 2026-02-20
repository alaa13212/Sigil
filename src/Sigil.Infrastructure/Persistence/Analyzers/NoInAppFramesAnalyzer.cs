using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.infrastructure.Persistence.Analyzers;

internal class NoInAppFramesAnalyzer(SigilDbContext dbContext, IDateTime dateTime) : IProjectAnalyzer
{
    public string AnalyzerId => "no-in-app-frames";
    public bool IsRepeatable => false;

    public async Task<ProjectRecommendation?> AnalyzeAsync(Project project)
    {
        var cutoff = dateTime.UtcNow.AddDays(-7);

        var hasAnyFrames = await dbContext.StackFrames
            .AnyAsync(sf => sf.Event!.ProjectId == project.Id && sf.Event.Timestamp >= cutoff);
        if (!hasAnyFrames) return null;

        var hasInAppFrames = await dbContext.StackFrames
            .AnyAsync(sf => sf.Event!.ProjectId == project.Id && sf.Event.Timestamp >= cutoff && sf.InApp);
        if (hasInAppFrames) return null;

        return new ProjectRecommendation
        {
            ProjectId = project.Id,
            AnalyzerId = AnalyzerId,
            Severity = RecommendationSeverity.Warning,
            Title = "No in-app stack frames detected",
            Description = "All stack frames are marked as framework or library code. Configure `in app` frame filtering in your SDK to highlight your own application code in stack traces.",
            ActionUrl = $"https://docs.sentry.io/platforms/{PlatformHelper.ToStringValue(project.Platform)}/configuration/options/"
        };
    }
}
