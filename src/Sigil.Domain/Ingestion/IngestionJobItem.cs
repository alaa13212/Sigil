namespace Sigil.Domain.Ingestion;

public record IngestionJobItem(int ProjectId, string RawEnvelope, DateTime ReceivedAt);