using Sigil.Domain.Ingestion;

namespace Sigil.Domain.Interfaces;

public interface IEventEnricher
{
    void Enrich(ParsedEvent parsedEvent, EventParsingContext context);
}