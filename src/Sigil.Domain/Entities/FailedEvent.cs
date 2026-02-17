using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class FailedEvent
{
    [Key]
    public long Id { get; set; }

    [ForeignKey(nameof(Project))]
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public required string RawEnvelope { get; set; }

    [MaxLength(2000)]
    public required string ErrorMessage { get; set; }

    [MaxLength(500)]
    public string? ExceptionType { get; set; }

    public FailedEventStage Stage { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool Reprocessed { get; set; }
    public DateTime? ReprocessedAt { get; set; }
}
