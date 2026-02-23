using Sigil.Domain.Ingestion;

namespace Sigil.Domain.Interfaces;

public interface IEventIngestionWorker
{
    bool TryEnqueue(IngestionJobItem item);
}