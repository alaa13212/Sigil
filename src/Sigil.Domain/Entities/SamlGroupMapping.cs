using System.ComponentModel.DataAnnotations;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class SamlGroupMapping
{
    public Guid Id { get; set; }

    public Guid SamlConnectionId { get; set; }
    public SamlConnection? SamlConnection { get; set; }

    [Required, MaxLength(200)]
    public required string GroupName { get; set; }

    [MaxLength(200)]
    public string? TeamName { get; set; }

    public TeamRole Role { get; set; } = TeamRole.Member;

    public bool GrantsGlobalAdmin { get; set; }
}
