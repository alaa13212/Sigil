using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IStackTraceFilterCache : ICacheService
{
    // Stryker disable once String : cache category key constant; exact value is arbitrary, collision is an integration-level concern
    static string ICacheService.CategoryName => "stack-trace-filters";

    bool TryGet(int projectId, out List<StackTraceFilter>? filters);
    void Set(int projectId, List<StackTraceFilter> filters);
    void Invalidate(int projectId);
}
