using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Services;

public class EventRanker : IEventRanker
{
    // Use the most recent event as the representative for the new issue
    // Other strategies could be implemented later
    public ParsedEvent GetMostRelevantEvent(IEnumerable<ParsedEvent> parsedEvents)
    {
        return parsedEvents.MaxBy(e => e.Timestamp)!;
    }

    public CapturedEvent GetMostRelevantEvent(IEnumerable<CapturedEvent> capturedEvents)
    {
        return capturedEvents.MaxBy(e => e.Timestamp)!;
    }
}