using System.ComponentModel.DataAnnotations;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class ExternalIssueLink
{
    public int Id { get; set; }

    public int IssueId { get; set; }
    public Issue? Issue { get; set; }

    public TrackerType TrackerType { get; set; }

    [MaxLength(200)]
    public required string ExternalId { get; set; }

    [MaxLength(1000)]
    public required string ExternalUrl { get; set; }

    [MaxLength(100)]
    public string? ExternalStatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastSyncedAt { get; set; }
}
