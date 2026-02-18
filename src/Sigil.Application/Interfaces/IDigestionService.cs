using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IDigestionService
{
    Task BulkDigestAsync(int projectId, List<ParsedEvent> parsedEvents, CancellationToken ct = default);
}
