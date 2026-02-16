using Sigil.Domain.Ingestion;

namespace Sigil.Domain.Interfaces;

public interface IEventParser
{
    List<ParsedEvent> Parse(string rawEnvelope);
}