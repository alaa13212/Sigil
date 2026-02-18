using System.ComponentModel.DataAnnotations.Schema;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class TeamMembership
{
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }
    public User? User { get; set; }

    [ForeignKey(nameof(Team))]
    public int TeamId { get; set; }
    public Team? Team { get; set; }

    public TeamRole Role { get; set; }
}