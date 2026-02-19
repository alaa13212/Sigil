using System.ComponentModel.DataAnnotations;

namespace Sigil.Domain.Entities;

public class UserPasskey
{
    public int Id { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public required byte[] CredentialId { get; set; }
    public required byte[] PublicKey { get; set; }
    public uint SignatureCounter { get; set; }

    [MaxLength(64)]
    public string? CredentialType { get; set; }

    public Guid AaGuid { get; set; }

    [MaxLength(128)]
    public required string DisplayName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsDiscoverable { get; set; }
}
