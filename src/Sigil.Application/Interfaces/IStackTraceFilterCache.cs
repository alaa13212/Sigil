using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IStackTraceFilterCache : ICacheService
{
    static string ICacheService.CategoryName => "stack-trace-filters";

    bool TryGet(int projectId, out List<StackTraceFilter>? filters);
    void Set(int projectId, List<StackTraceFilter> filters);
    void Invalidate(int projectId);
}
