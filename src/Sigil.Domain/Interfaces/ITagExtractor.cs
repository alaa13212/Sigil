using Sigil.Domain.Ingestion;

namespace Sigil.Domain.Interfaces;

public interface IEventEnricher
{
    Task Enrich(ParsedEvent parsedEvent, int projectId);
}