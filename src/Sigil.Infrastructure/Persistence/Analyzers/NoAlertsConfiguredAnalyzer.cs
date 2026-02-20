using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.infrastructure.Persistence.Analyzers;

internal class NoAlertsConfiguredAnalyzer(SigilDbContext dbContext) : IProjectAnalyzer
{
    public string AnalyzerId => "no-alerts-configured";
    public bool IsRepeatable => false;

    public async Task<ProjectRecommendation?> AnalyzeAsync(Project project)
    {
        var hasEvents = await dbContext.Events.AnyAsync(e => e.ProjectId == project.Id);
        if (!hasEvents) return null;

        var hasAlerts = await dbContext.AlertRules
            .AnyAsync(r => r.ProjectId == project.Id && r.Enabled);
        if (hasAlerts) return null;

        return new ProjectRecommendation
        {
            ProjectId = project.Id,
            AnalyzerId = AnalyzerId,
            Severity = RecommendationSeverity.Info,
            Title = "No alert rules configured",
            Description = "You have no active alert rules. Configure alerts to get notified when new issues appear, regressions occur, or event volumes spike.",
            ActionUrl = $"/projects/{project}/settings/alerts"
        };
    }
}
