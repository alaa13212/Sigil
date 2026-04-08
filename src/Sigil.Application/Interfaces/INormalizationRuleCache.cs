using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface INormalizationRuleCache : ICacheService
{
    // Stryker disable once String : cache category key constant; exact value is arbitrary, collision is an integration-level concern
    static string ICacheService.CategoryName => "normalization-rules";
    bool TryGet(int projectId, out List<TextNormalizationRule>? rules);
    void Set(int projectId, List<TextNormalizationRule> rules);
    void Invalidate(int projectId);
}
