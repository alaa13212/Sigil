using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class ProjectRecommendation
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Project))]
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    [Required, MaxLength(100)]
    public required string AnalyzerId { get; set; }

    public RecommendationSeverity Severity { get; set; }

    [Required, MaxLength(500)]
    public required string Title { get; set; }

    [Required]
    public required string Description { get; set; }

    [MaxLength(500)]
    public string? ActionUrl { get; set; }

    public bool Dismissed { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime? DismissedAt { get; set; }
}
