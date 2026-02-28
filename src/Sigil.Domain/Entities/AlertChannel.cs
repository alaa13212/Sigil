using System.ComponentModel.DataAnnotations;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class AlertChannel
{
    [Key] public int Id { get; set; }

    [Required, MaxLength(100)]
    public required string Name { get; set; }

    public AlertChannelType Type { get; set; }

    [Required]
    public required string Config { get; set; }

    public DateTime CreatedAt { get; set; }
}
