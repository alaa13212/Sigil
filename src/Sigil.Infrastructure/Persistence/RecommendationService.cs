using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Recommendations;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Persistence;

internal class RecommendationService(
    SigilDbContext dbContext,
    IEnumerable<IProjectAnalyzer> analyzers,
    IDateTime dateTime) : IRecommendationService
{
    public Task<int> GetRecommendationCountAsync(int projectId) =>
        dbContext.ProjectRecommendations
            .CountAsync(r => r.ProjectId == projectId && !r.Dismissed);

    public async Task<List<ProjectRecommendationResponse>> GetRecommendationsAsync(int projectId)
    {
        return await dbContext.ProjectRecommendations
            .Where(r => r.ProjectId == projectId && !r.Dismissed)
            .OrderByDescending(r => r.Severity)
            .ThenByDescending(r => r.DetectedAt)
            .Select(r => new ProjectRecommendationResponse(
                r.Id,
                r.AnalyzerId,
                r.Severity,
                r.Title,
                r.Description,
                r.ActionUrl,
                r.DetectedAt))
            .ToListAsync();
    }

    public async Task RunAnalyzersAsync(Project project)
    {
        var existing = await dbContext.ProjectRecommendations
            .AsTracking()
            .Where(r => r.ProjectId == project.Id)
            .ToListAsync();

        Dictionary<string, ProjectRecommendation> existingByAnalyzer = existing.ToDictionary(r => r.AnalyzerId);

        foreach (var analyzer in analyzers)
        {
            ProjectRecommendation? result = await analyzer.AnalyzeAsync(project);
            existingByAnalyzer.TryGetValue(analyzer.AnalyzerId, out var current);

            if (result is not null)
            {
                if (current is null)
                {
                    result.DetectedAt = dateTime.UtcNow;
                    dbContext.ProjectRecommendations.Add(result);
                }
                else if (!current.Dismissed || analyzer.IsRepeatable)
                {
                    current.Dismissed = false;
                    current.DismissedAt = null;
                    current.DetectedAt = dateTime.UtcNow;
                    current.Title = result.Title;
                    current.Description = result.Description;
                    current.Severity = result.Severity;
                    current.ActionUrl = result.ActionUrl;
                }
                // else: dismissed and !CanRetrigger — leave as-is
            }
            else if (current is not null)
            {
                // Condition no longer applies — remove the recommendation
                dbContext.ProjectRecommendations.Remove(current);
            }
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task DismissAsync(int recommendationId)
    {
        var rec = await dbContext.ProjectRecommendations
            .AsTracking()
            .FirstOrDefaultAsync(r => r.Id == recommendationId);
        if (rec is null) return;

        rec.Dismissed = true;
        rec.DismissedAt = dateTime.UtcNow;
        await dbContext.SaveChangesAsync();
    }
}
