using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class Project
{
    [Key] public int Id { get; set; }

    [Required, MaxLength(200)]
    public required string Name { get; set; }

    [StringLength(32)]
    public required string ApiKey { get; set; }

    public required Platform Platform { get; init; }
    
    
    [ForeignKey(nameof(Team))]
    public int? TeamId { get; set; }
    public Team? Team { get; set; }
    
    public ICollection<Issue> Issues { get; set; } = [];
    public ICollection<CapturedEvent> Events { get; set; } = [];
    
    public List<TextNormalizationRule> Rules { get; set; } = [];
}