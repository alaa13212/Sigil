namespace Sigil.Core.Ingestion;

public interface IIngestionService
{
    Task Ingest(string projectId, Stream envelopeStream);
}