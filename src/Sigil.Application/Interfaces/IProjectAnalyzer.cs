using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IProjectAnalyzer
{
    string AnalyzerId { get; }

    bool IsRepeatable { get; }

    Task<ProjectRecommendation?> AnalyzeAsync(Project project);
}
