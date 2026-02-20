using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class EventFilter
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Project))]
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    [Required, MaxLength(100)]
    public required string Field { get; set; }

    public FilterOperator Operator { get; set; }

    [Required, MaxLength(500)]
    public required string Value { get; set; }

    public FilterAction Action { get; set; }

    public bool Enabled { get; set; } = true;

    public int Priority { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}
