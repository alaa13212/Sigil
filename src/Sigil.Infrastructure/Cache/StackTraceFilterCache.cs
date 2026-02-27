using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Cache;

internal class StackTraceFilterCache(ICacheManager cacheManager) : IStackTraceFilterCache
{
    private string Category => this.Category();

    public bool TryGet(int projectId, out List<StackTraceFilter>? filters) =>
        cacheManager.TryGet(Category, projectId.ToString(), out filters);

    public void Set(int projectId, List<StackTraceFilter> filters) =>
        cacheManager.Set(Category, projectId.ToString(), filters);

    public void Invalidate(int projectId) =>
        cacheManager.Invalidate<IStackTraceFilterCache>(projectId.ToString());
}
