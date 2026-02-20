using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Recommendations;

public record ProjectRecommendationResponse(
    int Id,
    string AnalyzerId,
    RecommendationSeverity Severity,
    string Title,
    string Description,
    string? ActionUrl,
    DateTime DetectedAt);
