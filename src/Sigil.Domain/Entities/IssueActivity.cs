using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class IssueActivity
{
    [Key]
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public IssueActivityAction Action { get; set; }
    public string? Message { get; set; }
    
    [Column(TypeName = "jsonb")]
    public Dictionary<string, string>? Extra { get; set; }
    
    [ForeignKey(nameof(Issue))]
    public int IssueId { get; set; }
    public Issue? Issue { get; set; }
    
    [ForeignKey(nameof(User))]
    public int UserId { get; set; }
    public User? User { get; set; }
}