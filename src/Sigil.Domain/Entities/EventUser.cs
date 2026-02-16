using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sigil.Domain.Entities;

public class EventUser
{
    [Key, StringLength(64)]
    public required string UniqueIdentifier { get; set; }
    
    [MaxLength(200)]
    public string? Identifier { get; set; }

    [MaxLength(100)]
    public string? Username { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }
    
    [Column(TypeName = "jsonb")]
    public Dictionary<string, string>? Data { get; set; } = new();

    public ICollection<CapturedEvent> Events { get; set; } = [];
}