using System.ComponentModel.DataAnnotations;

namespace Sigil.Domain.Entities;

public class AppConfig
{
    [Key, MaxLength(100)]
    public required string Key { get; set; }

    [MaxLength(2000)]
    public string? Value { get; set; }

    public DateTime UpdatedAt { get; set; }
}
