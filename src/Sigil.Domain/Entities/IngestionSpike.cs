using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sigil.Domain.Entities;

public class IngestionSpike
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Project))]
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int DroppedCount { get; set; }
    public int AcceptedCount { get; set; }
}
