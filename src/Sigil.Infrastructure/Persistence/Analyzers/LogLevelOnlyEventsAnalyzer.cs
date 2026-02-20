using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.infrastructure.Persistence.Analyzers;

internal class LogLevelOnlyEventsAnalyzer(SigilDbContext dbContext, IDateTime dateTime) : IProjectAnalyzer
{
    public string AnalyzerId => "log-level-only-events";
    public bool IsRepeatable => false;

    public async Task<ProjectRecommendation?> AnalyzeAsync(Project project)
    {
        var cutoff = dateTime.UtcNow.AddDays(-7);
        var totalCount = await dbContext.Events
            .CountAsync(e => e.ProjectId == project.Id && e.Timestamp >= cutoff);
        if (totalCount < 20) return null;

        var hasRealErrors = await dbContext.Events
            .AnyAsync(e => e.ProjectId == project.Id && e.Timestamp >= cutoff && e.Level >= Severity.Error);
        if (hasRealErrors) return null;

        return new ProjectRecommendation
        {
            ProjectId = project.Id,
            AnalyzerId = AnalyzerId,
            Severity = RecommendationSeverity.Info,
            Title = "No real exceptions — only log-level events",
            Description = "All recent events are logs, warnings, or info messages with no actual exceptions. Error tracking is most valuable for uncaught exceptions. Consider configuring your SDK to capture exception events or reducing log-level noise.",
            ActionUrl = null
        };
    }
}
