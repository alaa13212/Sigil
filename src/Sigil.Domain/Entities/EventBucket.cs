using System.ComponentModel.DataAnnotations.Schema;

namespace Sigil.Domain.Entities;

public class EventBucket
{
    [ForeignKey(nameof(Issue))]
    public int IssueId { get; set; }
    public Issue? Issue { get; set; }

    /// <summary>Truncated to the start of the hour (UTC).</summary>
    public DateTime BucketStart { get; set; }

    public int Count { get; set; }
}
