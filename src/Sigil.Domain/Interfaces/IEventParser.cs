using Sigil.Domain.Ingestion;

namespace Sigil.Domain.Interfaces;

public interface IEventParser
{
    Task<List<ParsedEvent>> Parse(int projectId, string rawEnvelope, DateTime receivedAt);
}