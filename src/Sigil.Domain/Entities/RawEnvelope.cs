namespace Sigil.Domain.Entities;

public class RawEnvelope
{
    public long Id { get; set; }
    public required int ProjectId { get; set; }
    public required string RawData { get; set; }
    public required DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
}
