using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

/// <summary>Server-side only. Used by the digestion pipeline to ingest raw events.</summary>
public interface IEventIngestionService
{
    Task<HashSet<string>> FindExistingEventIdsAsync(IEnumerable<string> eventIds);
    List<CapturedEvent> BulkCreateEventsEntities(IEnumerable<ParsedEvent> parsedEvents, Project project, Issue issue,
        Dictionary<string, Release> releases, Dictionary<string, EventUser> users,
        Dictionary<string, Dictionary<string, int>> tagValues);
    Task<bool> SaveEventsAsync();
}
