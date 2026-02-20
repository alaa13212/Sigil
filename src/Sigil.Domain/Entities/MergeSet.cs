using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class MergeSet
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Project))]
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int PrimaryIssueId { get; set; }
    public Issue? PrimaryIssue { get; set; }

    public DateTime CreatedAt { get; set; }

    // Denormalized aggregates — refreshed during ingestion and merge operations
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public int OccurrenceCount { get; set; }
    public Severity Level { get; set; }

    [InverseProperty(nameof(Issue.MergeSet))]
    public ICollection<Issue> Issues { get; set; } = [];
}
