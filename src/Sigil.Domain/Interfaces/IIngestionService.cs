using Sigil.Domain.Ingestion;

namespace Sigil.Domain.Interfaces;

public interface IIngestionService
{
    Task BulkIngest(int projectId, List<ParsedEvent> parsedEvents, CancellationToken cancellationToken = default);
}