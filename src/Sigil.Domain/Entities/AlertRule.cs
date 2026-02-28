using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class AlertRule
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Project))]
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    [Required, MaxLength(200)]
    public required string Name { get; set; }

    public AlertTrigger Trigger { get; set; }

    [ForeignKey(nameof(AlertChannel))]
    public int AlertChannelId { get; set; }
    public AlertChannel? AlertChannel { get; set; }

    // Conditions
    public int? ThresholdCount { get; set; }
    public TimeSpan? ThresholdWindow { get; set; }
    public Severity? MinSeverity { get; set; }

    // Rate limiting
    public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromMinutes(30);

    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
