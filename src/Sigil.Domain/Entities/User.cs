using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class User : IdentityUser<Guid>  
{
    [MaxLength(100)]
    public string? DisplayName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }

    public SamlUserStatus SamlStatus { get; set; } = SamlUserStatus.None;

    public Guid? SamlConnectionId { get; set; }
    public SamlConnection? SamlConnection { get; set; }

    [MaxLength(500)]
    public string? SamlNameId { get; set; }

    public ICollection<TeamMembership> TeamMemberships { get; set; } = [];
    public ICollection<UserPasskey> Passkeys { get; set; } = [];
}