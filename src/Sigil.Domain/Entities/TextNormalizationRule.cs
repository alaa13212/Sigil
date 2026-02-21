using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Sigil.Domain.Entities;

public class TextNormalizationRule
{
    [Key]
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    [Required, MaxLength(1000), StringSyntax("regex")]
    public required string Pattern { get; set; }

    [Required, MaxLength(200)]
    public required string Replacement { get; set; }

    public int Priority { get; set; }

    public bool Enabled { get; set; } = true;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}
