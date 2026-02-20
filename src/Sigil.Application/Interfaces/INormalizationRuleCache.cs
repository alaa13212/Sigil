using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface INormalizationRuleCache : ICacheService
{
    static string ICacheService.CategoryName => "normalization-rules";
    bool TryGet(int projectId, out List<TextNormalizationRule>? rules);
    void Set(int projectId, List<TextNormalizationRule> rules);
    void Invalidate(int projectId);
}
