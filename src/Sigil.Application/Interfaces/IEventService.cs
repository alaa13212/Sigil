using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IEventService
{
    IEnumerable<CapturedEvent> BulkCreateEventsEntities(IEnumerable<ParsedEvent> capturedEvent, Issue issue, Dictionary<string, Release> releases, Dictionary<string, EventUser> users, Dictionary<string, TagValue> tagValues);
    Task<bool> SaveEventsAsync(IEnumerable<CapturedEvent> capturedEvents);
}