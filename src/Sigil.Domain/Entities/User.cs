using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Sigil.Domain.Entities;

public class User : IdentityUser<Guid>  
{
    [MaxLength(100)]
    public string? DisplayName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }

    public ICollection<TeamMembership> TeamMemberships { get; set; } = [];
    public ICollection<UserPasskey> Passkeys { get; set; } = [];
}