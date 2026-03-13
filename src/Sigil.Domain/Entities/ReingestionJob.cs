using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class ReingestionJob
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Project))]
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    [ForeignKey(nameof(Issue))]
    public int? IssueId { get; set; }
    public Issue? Issue { get; set; }

    public ReingestionJobStatus Status { get; set; }

    public int TotalEvents { get; set; }
    public int ProcessedEvents { get; set; }
    public int MovedEvents { get; set; }
    public int DeletedEvents { get; set; }
    public long LastProcessedEventId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [MaxLength(2000)]
    public string? Error { get; set; }

    public Guid? CreatedById { get; set; }
    [ForeignKey(nameof(CreatedById))]
    public User? CreatedBy { get; set; }
}
