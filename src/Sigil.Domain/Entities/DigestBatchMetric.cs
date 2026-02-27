using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sigil.Domain.Entities;

public class DigestBatchMetric
{
    [Key]
    public long Id { get; set; }

    public DateTime RecordedAt { get; set; }

    [ForeignKey(nameof(Project))]
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int EventCount { get; set; }

    /// <summary>Total pending (unprocessed) envelopes at the moment this batch started.</summary>
    public int QueueDepthAtStart { get; set; }

    /// <summary>Total ms to parse all envelopes in the batch.</summary>
    public int ParseElapsedMs { get; set; }

    /// <summary>Total ms to run BulkDigestAsync for the batch.</summary>
    public int DigestElapsedMs { get; set; }
}
