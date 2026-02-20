using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Cache;

internal class NormalizationRuleCache(ICacheManager cacheManager) : INormalizationRuleCache
{
    private string Category => this.Category();

    public bool TryGet(int projectId, out List<TextNormalizationRule>? rules) =>
        cacheManager.TryGet(Category, projectId.ToString(), out rules);

    public void Set(int projectId, List<TextNormalizationRule> rules) =>
        cacheManager.Set(Category, projectId.ToString(), rules);

    public void Invalidate(int projectId) =>
        cacheManager.Invalidate<INormalizationRuleCache>(projectId.ToString());
}
