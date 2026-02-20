using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Cache;

internal class AutoTagRuleCache(ICacheManager cacheManager) : IAutoTagRuleCache
{
    private string Category => this.Category();

    public bool TryGet(int projectId, out List<AutoTagRule>? rules) =>
        cacheManager.TryGet(Category, projectId.ToString(), out rules);

    public void Set(int projectId, List<AutoTagRule> rules) =>
        cacheManager.Set(Category, projectId.ToString(), rules);

    public void Invalidate(int projectId) =>
        cacheManager.Invalidate<IAutoTagRuleCache>(projectId.ToString());
}
