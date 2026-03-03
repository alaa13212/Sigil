using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

/// <summary>Server-side only. Provides raw event filters and rejection logic to the digestion pipeline.</summary>
public interface IEventFilterEngine
{
    Task<List<EventFilter>> GetRawFiltersForProjectAsync(int projectId);
    bool ShouldRejectEvent(ParsedEvent parsedEvent, List<EventFilter> filters);
}
