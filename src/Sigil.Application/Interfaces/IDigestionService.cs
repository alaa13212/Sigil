using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IDigestionService
{
    Task BulkDigestAsync(EventParsingContext context, List<ParsedEvent> parsedEvents, CancellationToken ct = default);
}
