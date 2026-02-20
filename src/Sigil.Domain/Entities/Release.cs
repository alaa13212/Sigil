using System.ComponentModel.DataAnnotations;

namespace Sigil.Domain.Entities;

public class Release
{
    public int Id { get; set; }
    
    [MaxLength(200)]
    public required string RawName { get; set; }
    
    public DateTime FirstSeenAt { get; set; }
    public DateTime? DeployedAt { get; set; }
    
    
    [MaxLength(200)]
    public string? Package { get; set; }
    
    [MaxLength(50)]
    public string? SemanticVersion { get; set; }
    
    public int? Build { get; set; }
    
    [StringLength(40)]
    public string? CommitSha { get; set; }
    
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public ICollection<CapturedEvent> Events { get; set; } = [];
}