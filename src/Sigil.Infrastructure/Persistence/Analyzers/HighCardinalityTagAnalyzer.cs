using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.infrastructure.Persistence.Analyzers;

internal class HighCardinalityTagAnalyzer(SigilDbContext dbContext) : IProjectAnalyzer
{
    private const int CardinalityThreshold = 200;

    public string AnalyzerId => "high-cardinality-tags";
    public bool IsRepeatable => true;

    public async Task<ProjectRecommendation?> AnalyzeAsync(Project project)
    {
        var highCardinalityKeys = await dbContext.TagKeys
            .Where(tk => tk.Values.Any(tv => tv.Events.Any(e => e.ProjectId == project.Id)))
            .Select(tk => new
            {
                tk.Key,
                ValueCount = tk.Values.Count(tv => tv.Events.Any(e => e.ProjectId == project.Id))
            })
            .Where(x => x.ValueCount > CardinalityThreshold)
            .OrderByDescending(x => x.ValueCount)
            .ToListAsync();

        if (highCardinalityKeys.Count == 0) return null;

        var keyList = string.Join(", ", highCardinalityKeys.Take(3)
            .Select(k => $"`{k.Key}` ({k.ValueCount:N0} values)"));

        return new ProjectRecommendation
        {
            ProjectId = project.Id,
            AnalyzerId = AnalyzerId,
            Severity = RecommendationSeverity.Warning,
            Title = "High-cardinality tags detected",
            Description = $"Some tag keys have an excessive number of unique values: {keyList}. High-cardinality tags (e.g., request IDs, session IDs) degrade query performance and should be stored in `context` instead.",
            ActionUrl = $"https://docs.sentry.io/platforms/{PlatformHelper.ToStringValue(project.Platform)}/enriching-events/context"
        };
    }
}
