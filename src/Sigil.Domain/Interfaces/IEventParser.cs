using Sigil.Domain.Ingestion;

namespace Sigil.Domain.Interfaces;

public interface IEventParser
{
    Task<List<ParsedEvent>> Parse(EventParsingContext context, string rawEnvelope, DateTime receivedAt);
}