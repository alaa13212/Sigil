using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IEventFilterCache : ICacheService
{
    // Stryker disable once String : cache category key constant; exact value is arbitrary, collision is an integration-level concern
    static string ICacheService.CategoryName => "event-filters";

    bool TryGet(int projectId, out List<EventFilter>? filters);
    void Set(int projectId, List<EventFilter> filters);
    void Invalidate(int projectId);
}
