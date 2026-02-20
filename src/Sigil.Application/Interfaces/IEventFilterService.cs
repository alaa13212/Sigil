using Sigil.Application.Models.Filters;
using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IEventFilterService
{
    // DTO access (UI/API + client)
    Task<List<EventFilterResponse>> GetFiltersAsync(int projectId);
    Task<EventFilterResponse> CreateFilterAsync(int projectId, CreateFilterRequest request);
    Task<EventFilterResponse?> UpdateFilterAsync(int filterId, UpdateFilterRequest request);
    Task<bool> DeleteFilterAsync(int filterId);

    // Server-only: used in digestion pipeline
    Task<List<EventFilter>> GetRawFiltersForProjectAsync(int projectId);
    bool ShouldRejectEvent(ParsedEvent parsedEvent, List<EventFilter> filters);
}
