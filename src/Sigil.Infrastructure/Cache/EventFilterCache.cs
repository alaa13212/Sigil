using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Cache;

internal class EventFilterCache(ICacheManager cacheManager) : IEventFilterCache
{
    private string Category => this.Category();

    public bool TryGet(int projectId, out List<EventFilter>? filters) =>
        cacheManager.TryGet(Category, projectId.ToString(), out filters);

    public void Set(int projectId, List<EventFilter> filters) =>
        cacheManager.Set(Category, projectId.ToString(), filters);

    public void Invalidate(int projectId) =>
        cacheManager.Invalidate<IEventFilterCache>(projectId.ToString());
}
