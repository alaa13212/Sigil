using System.ComponentModel.DataAnnotations;

namespace Sigil.Domain.Entities;

public class Team
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(500)]
    public required string Name { get; set; }

    public ICollection<Project> Projects { get; set; } = [];
    public ICollection<TeamMembership> Members { get; set; } = [];
}