using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class AlertHistory
{
    [Key]
    public long Id { get; set; }

    [ForeignKey(nameof(AlertRule))]
    public int AlertRuleId { get; set; }
    public AlertRule? AlertRule { get; set; }

    [ForeignKey(nameof(Issue))]
    public int? IssueId { get; set; }
    public Issue? Issue { get; set; }

    public DateTime FiredAt { get; set; }
    public AlertDeliveryStatus Status { get; set; }

    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }
}
