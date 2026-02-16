using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IEventRanker
{
    ParsedEvent GetMostRelevantEvent(IEnumerable<ParsedEvent> parsedEvents);
    CapturedEvent GetMostRelevantEvent(IEnumerable<CapturedEvent> capturedEvents);
}