using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IAutoTagRuleCache : ICacheService
{
    static string ICacheService.CategoryName => "auto-tag-rules";
    bool TryGet(int projectId, out List<AutoTagRule>? rules);
    void Set(int projectId, List<AutoTagRule> rules);
    void Invalidate(int projectId);
}
