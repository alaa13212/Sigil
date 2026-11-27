using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sigil.Domain.Entities;

/// <summary>An API access token owned by a user. Only the hash of the secret is stored.</summary>
public class ApiToken
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public required string Name { get; set; }

    /// <summary>Base64url encoded SHA-256 hash of the token secret.</summary>
    [Required, MaxLength(64)]
    public required string TokenHash { get; set; }

    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }

    public User? User { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }
}
