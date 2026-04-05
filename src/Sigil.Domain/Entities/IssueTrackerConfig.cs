using System.ComponentModel.DataAnnotations;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class IssueTrackerConfig
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public TrackerType TrackerType { get; set; }

    [MaxLength(4000)]
    public required string EncryptedConfig { get; set; }

    public bool Enabled { get; set; } = true;

    public bool TwoWaySync { get; set; }

    public DateTime CreatedAt { get; set; }
}
