using Sigil.Application.Models.Filters;

namespace Sigil.Application.Interfaces;

public interface IEventFilterService
{
    Task<List<EventFilterResponse>> GetFiltersAsync(int projectId);
    Task<EventFilterResponse> CreateFilterAsync(int projectId, CreateFilterRequest request);
    Task<EventFilterResponse?> UpdateFilterAsync(int filterId, UpdateFilterRequest request);
    Task<bool> DeleteFilterAsync(int filterId);
}
