using System.ComponentModel.DataAnnotations;

namespace Sigil.Domain.Entities;

public class SamlConnection
{
    public Guid Id { get; set; }

    [Required, MaxLength(200)]
    public required string Name { get; set; }

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    [Required, MaxLength(500)]
    public required string EntityId { get; set; }

    [MaxLength(1000)]
    public string? MetadataUrl { get; set; }

    public string? MetadataXml { get; set; }

    public DateTime? MetadataFetchedAt { get; set; }

    [MaxLength(1000)]
    public string? MetadataError { get; set; }

    public bool IsEnabled { get; set; } = true;

    [MaxLength(200)]
    public string EmailClaimType { get; set; } = "email";

    [MaxLength(200)]
    public string NameClaimType { get; set; } = "name";

    [MaxLength(200)]
    public string GroupsClaimType { get; set; } = "groups";

    [MaxLength(200)]
    public string? DefaultTeamName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<SamlGroupMapping> GroupMappings { get; set; } = [];

    public ICollection<User> Users { get; set; } = [];
}
