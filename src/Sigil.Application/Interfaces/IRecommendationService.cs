using Sigil.Application.Models.Recommendations;
using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IRecommendationService
{
    Task<int> GetRecommendationCountAsync(int projectId);
    Task<List<ProjectRecommendationResponse>> GetRecommendationsAsync(int projectId);
    Task RunAnalyzersAsync(Project project);
    Task DismissAsync(int recommendationId);
}
